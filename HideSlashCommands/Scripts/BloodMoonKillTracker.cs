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

        private readonly struct ReasonLine
        {
            public readonly string Key;
            public readonly (string name, object value)[] Args;

            public ReasonLine(string key, (string name, object value)[] args)
            {
                Key = key;
                Args = args ?? Array.Empty<(string, object)>();
            }
        }

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
            var orderedParties = _partyKills.OrderByDescending(p => p.Value).ToList();

            try
            {
                // Most 7DTD builds expose connected clients here
                var clients = ConnectionManager.Instance?.Clients?.List;
                if (clients != null)
                {
                    foreach (var ci in clients)
                    {
                        if (ci == null)
                            continue;

                        int entityId = ci.entityId;

                        string pid = PlayerIdUtil.GetPersistentIdOrNull(ci);
                        string lang = PlayerDataStore.PlayerStorage.GetLanguage(pid, "en");

                        if (orderedKills.Count == 0)
                        {
                            SendToEntity(entityId, L.Get(lang, "bloodmoon.end.no_kills"));
                        }
                        else
                        {
                            SendToEntity(entityId, L.Get(lang, "bloodmoon.end.title"));
                            SendToEntity(entityId, L.Get(lang, "bloodmoon.total_kills.header"));

                            int rank = 0;
                            foreach (var kv in orderedKills)
                            {
                                rank++;
                                string name = ResolveName(kv.Key);

                                SendToEntity(entityId, L.Format(lang, "bloodmoon.total_kills.rank_line",
                                    ("rank", rank),
                                    ("name", name),
                                    ("kills", kv.Value)
                                ));
                            }

                            SendToEntity(entityId, L.Format(lang, "bloodmoon.total_kills.footer",
                                ("total", orderedKills.Sum(x => x.Value))
                            ));

                            SendToEntity(entityId, L.Get(lang, "bloodmoon.party.header"));

                            if (orderedParties.Count == 0)
                            {
                                SendToEntity(entityId, L.Get(lang, "bloodmoon.party.none"));
                            }
                            else
                            {
                                foreach (var p in orderedParties)
                                {
                                    int partyId = p.Key;
                                    int partyTotal = p.Value;

                                    string partyTitle = GetPartyDisplayTitle(lang, partyId);

                                    SendToEntity(entityId, L.Format(lang, "bloodmoon.party.title",
                                        ("partyTitle", partyTitle)
                                    ));

                                    if (_partyMembers.TryGetValue(partyId, out var members) && members != null && members.Count > 0)
                                    {
                                        string memberNames = string.Join(", ", members.Select(ResolveName));
                                        SendToEntity(entityId, L.Format(lang, "bloodmoon.party.members",
                                            ("members", memberNames)
                                        ));
                                    }

                                    SendToEntity(entityId, L.Format(lang, "bloodmoon.party.total",
                                        ("kills", partyTotal)
                                    ));
                                }
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception("ConnectionManager.Instance.Clients.List was null (cannot enumerate clients).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DMChatTeleport] Localized Blood Moon output failed, falling back to English. Error: {ex}");

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

                            Broadcast(GetPartyDisplayTitle("en", partyId));

                            if (_partyMembers.TryGetValue(partyId, out var members) && members != null && members.Count > 0)
                            {
                                string memberNames = string.Join(", ", members.Select(ResolveName));
                                Broadcast("Members: " + memberNames);
                            }

                            Broadcast("Party Total Kills: " + partyTotal);
                        }
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

        private static void SendToEntity(int entityId, string msg)
        {
            if (string.IsNullOrEmpty(msg))
                return;

            msg = msg.Replace("\"", "\\\"");

            SdtdConsole.Instance.ExecuteSync(
                $"sayplayer {entityId} \"{msg}\"",
                null
            );
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

            var earnedRp = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var earnedReasons = new Dictionary<string, List<ReasonLine>>(StringComparer.OrdinalIgnoreCase);

            void AddEarned(string playerId, int rp, string reasonKey, params (string name, object value)[] args)
            {
                if (string.IsNullOrWhiteSpace(playerId) || rp <= 0) return;

                earnedRp[playerId] = earnedRp.TryGetValue(playerId, out var cur) ? (cur + rp) : rp;

                if (!earnedReasons.TryGetValue(playerId, out var list) || list == null)
                {
                    list = new List<ReasonLine>();
                    earnedReasons[playerId] = list;
                }

                if (!string.IsNullOrWhiteSpace(reasonKey))
                    list.Add(new ReasonLine(reasonKey, args));
            }

            void GrantPartyMembers(int partyId, int rp, string reasonKey, params (string name, object value)[] args)
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
                    AddEarned(id, rp, reasonKey, args);
                }
            }

            // 1) Presence rewards
            if (rewards.Presence != null && rewards.Presence.Enabled)
            {
                int presenceRp = Math.Max(0, rewards.Presence.RP);
                if (presenceRp > 0 && _presentThisBloodMoon.Count > 0)
                {
                    foreach (var id in _presentThisBloodMoon)
                    {
                        if (string.IsNullOrWhiteSpace(id)) continue;

                        PlayerDataStore.PlayerStorage.AddRP(id, presenceRp);
                        AddEarned(id, presenceRp, "bloodmoon.rewards.reason.presence",
                            ("rp", presenceRp)
                        );
                    }
                }
            }

            // 2) Party rank rewards
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
                        GrantPartyMembers(partyId, rp, "bloodmoon.rewards.reason.party_first",
                            ("partyId", partyId),
                            ("rp", rp)
                        );
                }

                if (orderedPartyIds.Count > 1)
                {
                    int partyId = orderedPartyIds[1];
                    int rp = Math.Max(0, rewards.PartyRankRewards.SecondPlaceRP);
                    if (rp > 0)
                        GrantPartyMembers(partyId, rp, "bloodmoon.rewards.reason.party_second",
                            ("partyId", partyId),
                            ("rp", rp)
                        );
                }
            }

            // 3) Top kills rewards (individual)
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
                        AddEarned(p1, rp1, "bloodmoon.rewards.reason.topkills_first",
                            ("rp", rp1)
                        );
                    }
                }

                if (orderedPlayers.Count > 1)
                {
                    string p2 = orderedPlayers[1];
                    int rp2 = Math.Max(0, rewards.SoloRankRewards.SecondPlaceRP);

                    if (rp2 > 0 && RequirePresent(p2))
                    {
                        PlayerDataStore.PlayerStorage.AddRP(p2, rp2);
                        AddEarned(p2, rp2, "bloodmoon.rewards.reason.topkills_second",
                            ("rp", rp2)
                        );
                    }
                }
            }

            // 4) Bonuses (only KillStep remains)
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
                            AddEarned(pid, rp, "bloodmoon.rewards.reason.killbonus",
                                ("kills", killCount),
                                ("rp", rp)
                            );
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

                    string lang = PlayerDataStore.PlayerStorage.GetLanguage(playerId, "en");

                    earnedReasons.TryGetValue(playerId, out var reasons);

                    string reasonText;
                    if (reasons != null && reasons.Count > 0)
                    {
                        var parts = new List<string>(reasons.Count);

                        foreach (var r in reasons)
                        {
                            if ((r.Key == "bloodmoon.rewards.reason.party_first" || r.Key == "bloodmoon.rewards.reason.party_second")
                                && TryGetArgInt(r.Args, "partyId", out int partyId))
                            {
                                string partyTitle = GetPartyDisplayTitle(lang, partyId);
                                parts.Add(L.Format(lang, r.Key,
                                    ("partyTitle", partyTitle),
                                    ("rp", GetArg(r.Args, "rp"))
                                ));
                            }
                            else
                            {
                                parts.Add(L.Format(lang, r.Key, r.Args));
                            }
                        }

                        reasonText = string.Join(", ", parts);
                    }
                    else
                    {
                        reasonText = L.Get(lang, "bloodmoon.rewards.default_reason");
                    }

                    SendPrivateMessageToPlayerId(playerId,
                        L.Format(lang, "bloodmoon.rewards.message",
                            ("reasons", reasonText),
                            ("total", total)
                        )
                    );
                }
            }
        }

        private static string GetPartyDisplayTitle(string lang, int partyId)
        {
            if (_partyMembers.TryGetValue(partyId, out var members) && members != null)
            {
                if (members.Count == 1)
                {
                    string soloName = ResolveName(members.First());
                    return L.Format(lang, "bloodmoon.party.solo_title",
                        ("name", soloName)
                    );
                }

                return L.Format(lang, "bloodmoon.party.party_title",
                    ("count", members.Count)
                );
            }

            return L.Get(lang, "bloodmoon.party.group_title");
        }

        private static bool TryGetArgInt((string name, object value)[] args, string name, out int value)
        {
            value = 0;
            if (args == null) return false;

            foreach (var a in args)
            {
                if (!string.Equals(a.name, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    if (a.value is int i) { value = i; return true; }
                    if (a.value is long l) { value = (int)l; return true; }
                    if (a.value is string s && int.TryParse(s, out int parsed)) { value = parsed; return true; }
                }
                catch { }

                return false;
            }

            return false;
        }

        private static object GetArg((string name, object value)[] args, string name)
        {
            if (args == null) return null;

            foreach (var a in args)
            {
                if (string.Equals(a.name, name, StringComparison.OrdinalIgnoreCase))
                    return a.value;
            }

            return null;
        }

        private static void SendPrivateMessageToPlayerId(string playerId, string message)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(message))
                return;

            int entityId = ResolveEntityIdForPlayerId(playerId);
            if (entityId <= 0)
                return;

            message = message.Replace("\"", "\\\"");
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
