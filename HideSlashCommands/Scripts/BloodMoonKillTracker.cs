using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DMChatTeleport
{
    public static class BloodMoonKillTracker
    {
        private static readonly HashSet<string> _presentThisBloodMoon =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<string> GetPresence() => _presentThisBloodMoon;

        private static readonly Dictionary<string, int> _kills =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<int, int> _partyKills =
            new Dictionary<int, int>();

        private static readonly Dictionary<int, HashSet<string>> _partyMembers =
            new Dictionary<int, HashSet<string>>();

        private static readonly Dictionary<string, string> _names =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static bool IsCounting { get; private set; }

        public static void AddKill(string playerId, string playerName, int partyId)
        {
            if (!IsCounting) return;
            if (string.IsNullOrWhiteSpace(playerId)) return;

            if (!string.IsNullOrWhiteSpace(playerName))
                _names[playerId] = playerName;

            _presentThisBloodMoon.Add(playerId);

            if (_kills.TryGetValue(playerId, out int v))
                _kills[playerId] = v + 1;
            else
                _kills[playerId] = 1;

            if (_partyKills.TryGetValue(partyId, out int pv))
                _partyKills[partyId] = pv + 1;
            else
                _partyKills[partyId] = 1;

            if (!_partyMembers.TryGetValue(partyId, out HashSet<string> members) || members == null)
            {
                members = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _partyMembers[partyId] = members;
            }
            members.Add(playerId);
        }

        public static void BroadcastResultsAndReset()
        {
            MarkOnlinePresenceFromRewardSystem();

            var orderedKills = _kills.OrderByDescending(k => k.Value).ToList();

            if (orderedKills.Count == 0)
            {
                Broadcast("Blood Moon ended! No kills were recorded.");
            }
            else
            {
                Broadcast("Blood Moon ended!");
                Broadcast("Total Kills");

                int rank = 0;
                foreach (var kv in orderedKills)
                {
                    rank++;
                    string name = ResolveName(kv.Key);
                    Broadcast($"{rank}. {name} - {kv.Value} kills");
                }

                Broadcast($"Total kills: {orderedKills.Sum(x => x.Value)}");

                Broadcast("Party Results");

                var orderedParties = _partyKills.OrderByDescending(p => p.Value).ToList();
                if (orderedParties.Count == 0)
                {
                    Broadcast("No party kills recorded.");
                }
                else
                {
                    foreach (var p in orderedParties)
                    {
                        int partyId = p.Key;
                        int partyTotal = p.Value;

                        Broadcast(GetPartyDisplayTitle(partyId));

                        if (_partyMembers.TryGetValue(partyId, out var members) && members != null && members.Count > 0)
                        {
                            string memberNames = string.Join(", ", members.Select(ResolveName));
                            Broadcast("Members: " + memberNames);
                        }

                        Broadcast("Party Total Kills: " + partyTotal);
                    }
                }
            }

            try
            {
                AwardBloodMoonRewards();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DMChatTeleport] AwardBloodMoonRewards failed: {ex}");
            }

            _presentThisBloodMoon.Clear();
            _kills.Clear();
            _partyKills.Clear();
            _partyMembers.Clear();
            _names.Clear();
            IsCounting = false;
        }

        private static void AwardBloodMoonRewards()
        {
            var cfg = ConfigManager.Config;
            var rewards = cfg?.BloodMoonRewards;
            if (rewards == null || !rewards.Enabled)
                return;

            bool announce = rewards.AnnounceRewardMessages;

            bool RequirePresent(string id)
            {
                if (!rewards.RequirePresenceForRankRewards) return true;
                return !string.IsNullOrWhiteSpace(id) && _presentThisBloodMoon.Contains(id);
            }

            // ONE message per player
            var earnedRp = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var earnedReasons = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            void AddEarned(string playerId, int rp, string reason)
            {
                if (string.IsNullOrWhiteSpace(playerId) || rp <= 0) return;

                earnedRp[playerId] = earnedRp.TryGetValue(playerId, out var cur) ? (cur + rp) : rp;

                if (!earnedReasons.TryGetValue(playerId, out var list) || list == null)
                {
                    list = new List<string>();
                    earnedReasons[playerId] = list;
                }

                if (!string.IsNullOrWhiteSpace(reason))
                    list.Add(reason);
            }

            // Helper: grant RP to all members of a party id
            void GrantPartyMembers(int partyId, int rp, string reason)
            {
                if (rp <= 0) return;

                if (!_partyMembers.TryGetValue(partyId, out var members) || members == null || members.Count == 0)
                    return;

                IEnumerable<string> targets = members;

                if (rewards.RequirePresenceForRankRewards)
                    targets = targets.Where(m => _presentThisBloodMoon.Contains(m));

                var list = targets.ToList();
                if (list.Count == 0) return;

                foreach (var id in list)
                {
                    PlayerDataStore.PlayerStorage.AddRP(id, rp);
                    AddEarned(id, rp, reason);
                }
            }

            // ------------------------------------------------------------
            // 1) Presence rewards
            // ------------------------------------------------------------
            if (rewards.Presence != null && rewards.Presence.Enabled)
            {
                int presenceRp = Math.Max(0, rewards.Presence.RP);
                if (presenceRp > 0 && _presentThisBloodMoon.Count > 0)
                {
                    foreach (var id in _presentThisBloodMoon)
                    {
                        if (string.IsNullOrWhiteSpace(id)) continue;

                        PlayerDataStore.PlayerStorage.AddRP(id, presenceRp);
                        AddEarned(id, presenceRp, $"Presence +{presenceRp}");
                    }
                }
            }

            // ------------------------------------------------------------
            // 2) Party rank rewards (solo players are party-of-1; no special "solo party" category)
            // ------------------------------------------------------------
            if (rewards.PartyRankRewards != null && rewards.PartyRankRewards.Enabled && _partyKills.Count > 0)
            {
                var orderedPartyIds = _partyKills
                    .OrderByDescending(p => p.Value)
                    .Select(p => p.Key)
                    .ToList();

                if (orderedPartyIds.Count > 0)
                {
                    int partyId = orderedPartyIds[0];
                    int rp = Math.Max(0, rewards.PartyRankRewards.FirstPlaceRP);
                    if (rp > 0)
                        GrantPartyMembers(partyId, rp, $"Party 1st ({GetPartyDisplayTitle(partyId)}) +{rp}");
                }

                if (orderedPartyIds.Count > 1)
                {
                    int partyId = orderedPartyIds[1];
                    int rp = Math.Max(0, rewards.PartyRankRewards.SecondPlaceRP);
                    if (rp > 0)
                        GrantPartyMembers(partyId, rp, $"Party 2nd ({GetPartyDisplayTitle(partyId)}) +{rp}");
                }
            }

            // ------------------------------------------------------------
            // 3) Top kills rewards (individual)
            // ------------------------------------------------------------
            if (rewards.SoloRankRewards != null && rewards.SoloRankRewards.Enabled && _kills.Count > 0)
            {
                var orderedPlayers = _kills
                    .OrderByDescending(k => k.Value)
                    .Select(k => k.Key)
                    .ToList();

                if (orderedPlayers.Count > 0)
                {
                    string p1 = orderedPlayers[0];
                    int rp1 = Math.Max(0, rewards.SoloRankRewards.FirstPlaceRP);

                    if (rp1 > 0 && RequirePresent(p1))
                    {
                        PlayerDataStore.PlayerStorage.AddRP(p1, rp1);
                        AddEarned(p1, rp1, $"Top Kills 1st +{rp1}");
                    }
                }

                if (orderedPlayers.Count > 1)
                {
                    string p2 = orderedPlayers[1];
                    int rp2 = Math.Max(0, rewards.SoloRankRewards.SecondPlaceRP);

                    if (rp2 > 0 && RequirePresent(p2))
                    {
                        PlayerDataStore.PlayerStorage.AddRP(p2, rp2);
                        AddEarned(p2, rp2, $"Top Kills 2nd +{rp2}");
                    }
                }
            }

            // ------------------------------------------------------------
            // 4) Bonuses (only KillStep remains)
            // ------------------------------------------------------------
            var bonuses = rewards.Bonuses;

            if (bonuses?.KillStep != null && bonuses.KillStep.Enabled)
            {
                int every = Math.Max(1, bonuses.KillStep.EveryKills);
                int perStep = Math.Max(0, bonuses.KillStep.RPPerStep);
                int maxRp = Math.Max(0, bonuses.KillStep.MaxRP);

                if (perStep > 0 && _kills.Count > 0)
                {
                    foreach (var kv in _kills)
                    {
                        string pid = kv.Key;
                        int killCount = kv.Value;

                        if (!RequirePresent(pid)) continue;

                        int steps = killCount / every;
                        int rp = steps * perStep;

                        if (maxRp > 0)
                            rp = Math.Min(rp, maxRp);

                        if (rp > 0)
                        {
                            PlayerDataStore.PlayerStorage.AddRP(pid, rp);
                            AddEarned(pid, rp, $"Kill Bonus ({killCount} kills) +{rp}");
                        }
                    }
                }
            }

            PlayerDataStore.PlayerStorage.Save();

            if (announce && earnedRp.Count > 0)
            {
                foreach (var kv in earnedRp)
                {
                    string playerId = kv.Key;
                    int total = kv.Value;
                    if (total <= 0) continue;

                    earnedReasons.TryGetValue(playerId, out var reasons);
                    string reasonText = (reasons != null && reasons.Count > 0)
                        ? string.Join(", ", reasons)
                        : "Rewards";

                    SendPrivateMessageToPlayerId(playerId, $"Blood Moon Rewards: {reasonText} = +{total} RP");
                }
            }
        }

        private static string GetPartyDisplayTitle(int partyId)
        {
            if (_partyMembers.TryGetValue(partyId, out var members) && members != null)
            {
                if (members.Count == 1)
                {
                    string soloName = ResolveName(members.First());
                    return $"Solo: {soloName}";
                }

                return $"Party ({members.Count} players)";
            }

            return "Group";
        }

        private static void SendPrivateMessageToPlayerId(string playerId, string message)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(message))
                return;

            int entityId = ResolveEntityIdForPlayerId(playerId);
            if (entityId <= 0)
                return;

            SdtdConsole.Instance.ExecuteSync($"sayplayer {entityId} \"{message}\"", null);
        }

        private static int ResolveEntityIdForPlayerId(string playerId)
        {
            // 1) Stored entityId
            try
            {
                var pd = PlayerDataStore.PlayerStorage.Get(playerId);
                if (pd != null && pd.entityId > 0)
                    return pd.entityId;
            }
            catch { }

            // 2) Live client match
            try
            {
                var clients = ConnectionManager.Instance?.Clients?.List;
                if (clients != null)
                {
                    foreach (var c in clients)
                    {
                        if (c == null || c.entityId <= 0) continue;

                        string id = PlayerIdUtil.GetPersistentIdOrNull(c);
                        if (string.Equals(id, playerId, StringComparison.OrdinalIgnoreCase))
                            return c.entityId;
                    }
                }
            }
            catch { }

            return 0;
        }

        private static string ResolveName(string playerId)
        {
            if (!string.IsNullOrWhiteSpace(playerId) &&
                _names.TryGetValue(playerId, out string n) &&
                !string.IsNullOrWhiteSpace(n))
                return n;

            return playerId ?? "Unknown";
        }

        public static void Broadcast(string msg)
        {
            SdtdConsole.Instance.ExecuteSync("say \"" + msg + "\"", null);
        }

        public static void StartTracking()
        {
            _presentThisBloodMoon.Clear();
            _kills.Clear();
            _partyKills.Clear();
            _partyMembers.Clear();
            _names.Clear();
            IsCounting = true;
        }

        public static void MarkOnlinePresenceFromRewardSystem()
        {
            if (!IsCounting) return;

            var online = RewardPointsManager.GetOnlinePlayerIdsSnapshot();
            if (online == null) return;

            foreach (var id in online)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    _presentThisBloodMoon.Add(id);
            }
        }
    }
}
