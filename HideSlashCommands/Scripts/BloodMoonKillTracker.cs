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
            // Make sure we include current online snapshot as "present"
            // (helps when someone is online but got 0 kills)
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
                    Broadcast(string.Format("{0}. {1} - {2} kills", rank, name, kv.Value));
                }

                Broadcast(string.Format("Total kills: {0}", orderedKills.Sum(x => x.Value)));

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

                        string partyTitle = (partyId == 0) ? "Team Solo" : ("Party " + partyId);
                        Broadcast(partyTitle);

                        if (_partyMembers.TryGetValue(partyId, out HashSet<string> members) && members != null && members.Count > 0)
                        {
                            string memberNames = string.Join(", ", members.Select(ResolveName));
                            Broadcast("Members: " + memberNames);
                        }

                        Broadcast("Party Total Kills: " + partyTotal);
                    }
                }
            }

            // ✅ award RP before clearing data
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

            // We still respect this flag, but we send private messages instead of global spam.
            bool announce = rewards.AnnounceRewardMessages;

            bool RequirePresent(string id)
            {
                if (!rewards.RequirePresenceForRankRewards) return true;
                return !string.IsNullOrWhiteSpace(id) && _presentThisBloodMoon.Contains(id);
            }

            // Track what each player earned so we can send ONE private message per player
            var earnedRp = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var earnedReasons = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            void AddEarned(string playerId, int rp, string reason)
            {
                if (string.IsNullOrWhiteSpace(playerId) || rp <= 0) return;

                if (!earnedRp.TryGetValue(playerId, out int cur))
                    earnedRp[playerId] = rp;
                else
                    earnedRp[playerId] = cur + rp;

                if (!earnedReasons.TryGetValue(playerId, out var list) || list == null)
                {
                    list = new List<string>();
                    earnedReasons[playerId] = list;
                }

                if (!string.IsNullOrWhiteSpace(reason))
                    list.Add(reason);
            }

            // ------------------------------------------------------------
            // 1) Presence rewards
            // ------------------------------------------------------------
            int presenceRp = Math.Max(0, rewards.PresenceRewardRP);
            if (presenceRp > 0 && _presentThisBloodMoon.Count > 0)
            {
                foreach (var id in _presentThisBloodMoon)
                {
                    if (string.IsNullOrWhiteSpace(id)) continue;

                    PlayerDataStore.PlayerStorage.AddRP(id, presenceRp);
                    AddEarned(id, presenceRp, $"Presence +{presenceRp}");
                }
            }

            // ------------------------------------------------------------
            // 2) Party rank rewards (includes Solo partyId=0)
            // ------------------------------------------------------------
            var orderedPartyIds = _partyKills
                .OrderByDescending(p => p.Value)
                .Select(p => p.Key)
                .ToList();

            void GrantPartyRank(int partyId, int rp, string label)
            {
                if (rp <= 0) return;

                if (!_partyMembers.TryGetValue(partyId, out var members) || members == null || members.Count == 0)
                    return;

                var targets = rewards.RequirePresenceForRankRewards
                    ? members.Where(m => _presentThisBloodMoon.Contains(m)).ToList()
                    : members.ToList();

                if (targets.Count == 0) return;

                string partyTitle = (partyId == 0) ? "Team Solo" : ("Party " + partyId);

                foreach (var id in targets)
                {
                    PlayerDataStore.PlayerStorage.AddRP(id, rp);
                    AddEarned(id, rp, $"{label} {partyTitle} +{rp}");
                }
            }

            if (rewards.PartyRankRewards != null && orderedPartyIds.Count > 0)
                GrantPartyRank(orderedPartyIds[0], Math.Max(0, rewards.PartyRankRewards.FirstPlaceRP), "Party 1st");

            if (rewards.PartyRankRewards != null && orderedPartyIds.Count > 1)
                GrantPartyRank(orderedPartyIds[1], Math.Max(0, rewards.PartyRankRewards.SecondPlaceRP), "Party 2nd");

            // ------------------------------------------------------------
            // 3) MVP / Top kills rewards (across all players)
            // Reuse SoloRankRewards as "TopKillsRewards" (no config changes needed).
            // ------------------------------------------------------------
            var orderedPlayers = _kills
                .OrderByDescending(k => k.Value)
                .Select(k => k.Key)
                .ToList();

            if (rewards.SoloRankRewards != null && orderedPlayers.Count > 0)
            {
                string p1 = orderedPlayers[0];
                int rp1 = Math.Max(0, rewards.SoloRankRewards.FirstPlaceRP);

                if (rp1 > 0 && RequirePresent(p1))
                {
                    PlayerDataStore.PlayerStorage.AddRP(p1, rp1);
                    AddEarned(p1, rp1, $"Top Kills 1st +{rp1}");
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

            // Persist RP updates
            PlayerDataStore.PlayerStorage.Save();

            // ------------------------------------------------------------
            // Private reward messages (ONE message per player)
            // ------------------------------------------------------------
            if (announce && earnedRp.Count > 0)
            {
                foreach (var kv in earnedRp)
                {
                    string playerId = kv.Key;
                    int total = kv.Value;

                    if (total <= 0) continue;

                    string name = ResolveName(playerId);

                    earnedReasons.TryGetValue(playerId, out var reasons);
                    string reasonText = (reasons != null && reasons.Count > 0)
                        ? string.Join(", ", reasons)
                        : "Rewards";

                    SendPrivateMessageToPlayerId(playerId, $"Blood Moon Rewards: {reasonText} = +{total} RP");
                }
            }
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
            // 1) Try saved/stored entityId
            try
            {
                var pd = PlayerDataStore.PlayerStorage.Get(playerId);
                if (pd != null && pd.entityId > 0)
                    return pd.entityId;
            }
            catch { }

            // 2) Fallback: live client list match
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
