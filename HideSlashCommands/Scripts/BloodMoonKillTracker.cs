using System;
using System.Collections.Generic;
using System.Linq;

namespace DMChatTeleports
{
    public static class BloodMoonKillTracker
    {
        // per-player total kills (for the night)
        private static readonly Dictionary<string, int> _kills =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // per-party total kills (party at time of kill)
        // partyId == 0 => Solo
        private static readonly Dictionary<int, int> _partyKills =
            new Dictionary<int, int>();

        // partyId -> set of playerIds who contributed kills to that party during the night
        private static readonly Dictionary<int, HashSet<string>> _partyMembers =
            new Dictionary<int, HashSet<string>>();

        // playerId -> most recent name seen
        private static readonly Dictionary<string, string> _names =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static bool IsCounting { get; private set; }

        public static void UpdateFromTimeTick()
        {
            bool active = BloodMoonUtil.IsActiveNow();

            if (!IsCounting && active)
            {
                _kills.Clear();
                _partyKills.Clear();
                _partyMembers.Clear();
                _names.Clear();
                IsCounting = true;
                return;
            }

            if (IsCounting && !active)
            {
                BroadcastResultsAndReset();
                return;
            }
        }

        // NEW: add partyId snapshot at time of kill (partyId 0 = Solo)
        public static void AddKill(string playerId, string playerName, int partyId)
        {
            if (!IsCounting) return;
            if (string.IsNullOrWhiteSpace(playerId)) return;

            if (!string.IsNullOrWhiteSpace(playerName))
                _names[playerId] = playerName;

            // player total
            int v;
            if (_kills.TryGetValue(playerId, out v))
                _kills[playerId] = v + 1;
            else
                _kills[playerId] = 1;

            // party total
            int pv;
            if (_partyKills.TryGetValue(partyId, out pv))
                _partyKills[partyId] = pv + 1;
            else
                _partyKills[partyId] = 1;

            // party member list
            HashSet<string> members;
            if (!_partyMembers.TryGetValue(partyId, out members))
            {
                members = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _partyMembers[partyId] = members;
            }
            members.Add(playerId);
        }

        public static void BroadcastResultsAndReset()
        {
            var orderedKills = _kills.OrderByDescending(k => k.Value).ToList();

            if (orderedKills.Count == 0)
            {
                Broadcast("Blood Moon ended! No kills were recorded.");
            }
            else
            {
                Broadcast("Blood Moon ended!");

                // --- Player leaderboard (top 10) ---
                Broadcast("Total Kills");

                int rank = 0;
                foreach (var kv in orderedKills)
                {
                    rank++;
                    string name = ResolveName(kv.Key);
                    Broadcast(string.Format("{0}. {1} - {2} kills", rank, name, kv.Value));
                }

                Broadcast(string.Format("Total kills: {0}", orderedKills.Sum(x => x.Value)));

                // --- Party leaderboard ---
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

                        string partyTitle = (partyId == 0) ? "Solo" : ("Party " + partyId);
                        Broadcast(partyTitle);

                        HashSet<string> members;
                        if (_partyMembers.TryGetValue(partyId, out members) && members.Count > 0)
                        {
                            string memberNames = string.Join(", ", members.Select(ResolveName));
                            Broadcast("Members: " + memberNames);
                        }

                        Broadcast("Party Total Kills: " + partyTotal);
                    }
                }
            }

            _kills.Clear();
            _partyKills.Clear();
            _partyMembers.Clear();
            _names.Clear();
            IsCounting = false;
        }

        private static string ResolveName(string playerId)
        {
            string n;
            if (!string.IsNullOrWhiteSpace(playerId) &&
                _names.TryGetValue(playerId, out n) &&
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
            _kills.Clear();
            _partyKills.Clear();
            _partyMembers.Clear();
            _names.Clear();
            IsCounting = true;
        }
    }
}
