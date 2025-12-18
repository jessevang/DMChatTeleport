using System;
using System.Collections.Generic;
using System.Linq;

namespace DMChatTeleports
{
    public static class BloodMoonKillTracker
    {
        private static readonly Dictionary<string, int> _kills =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> _names =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static bool IsCounting { get; private set; }

        public static void UpdateFromTimeTick()
        {
            bool active = BloodMoonUtil.IsActiveNow();

            if (!IsCounting && active)
            {
                _kills.Clear();
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

        public static void AddKill(string playerId, string playerName)
        {
            if (!IsCounting) return;
            if (string.IsNullOrWhiteSpace(playerId)) return;

            if (!string.IsNullOrWhiteSpace(playerName))
                _names[playerId] = playerName;

            int v;
            if (_kills.TryGetValue(playerId, out v))
                _kills[playerId] = v + 1;
            else
                _kills[playerId] = 1;
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
                Broadcast("Total Kills");

                int rank = 0;
                foreach (var kv in orderedKills.Take(10))
                {
                    rank++;
                    string name = ResolveName(kv.Key);
                    Broadcast(string.Format("{0}. {1} - {2} kills", rank, name, kv.Value));
                }

                Broadcast(string.Format("Total kills: {0}", orderedKills.Sum(x => x.Value)));
            }

            _kills.Clear();
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
            _names.Clear();
            IsCounting = true;
        }
    }
}
