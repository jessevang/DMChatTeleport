using System;
using System.Collections.Generic;
using UnityEngine;

namespace DMChatTeleport
{
    internal static class WelcomeManager
    {
        private static readonly HashSet<string> _welcomedThisSession =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void OnPlayerSpawned(ClientInfo cInfo, int entityId)
        {
            if (cInfo == null || entityId <= 0)
                return;

            string playerId = PlayerIdUtil.GetPersistentIdOrNull(cInfo);
            if (string.IsNullOrWhiteSpace(playerId))
            {
                Debug.Log("[DMChatTeleport] WelcomeManager could not resolve persistent playerId (EOS/Steam).");
                return;
            }

            if (_welcomedThisSession.Contains(playerId))
                return;

            _welcomedThisSession.Add(playerId);

            // Ensure player exists in storage
            bool created;
            var pd = PlayerDataStore.PlayerStorage.GetOrCreate(playerId, out created);
            pd.entityId = entityId;
            if (created) PlayerDataStore.PlayerStorage.Save();

            // Language preference (default en)
            string lang = PlayerDataStore.PlayerStorage.GetLanguage(playerId, "en");

            string name = GetPlayerName(cInfo) ?? playerId;
            int rp = pd.RewardPoints;

            SendPrivate(entityId, L.Format(lang, "welcome.back",
                ("name", name),
                ("rp", rp)
            ));

            var cfg = ConfigManager.Config;
            if (cfg != null && cfg.TurnOnStarterKits && !pd.HasPickedStarterKit)
            {
                SendPrivate(entityId, L.Get(lang, "welcome.kits_hint"));
            }
        }

        public static void OnPlayerDisconnected(ClientInfo cInfo)
        {
            if (cInfo == null) return;

            string playerId = PlayerIdUtil.GetPersistentIdOrNull(cInfo);
            if (!string.IsNullOrWhiteSpace(playerId))
                _welcomedThisSession.Remove(playerId);
        }

        private static void SendPrivate(int entityId, string msg)
        {
            if (entityId <= 0 || string.IsNullOrWhiteSpace(msg))
                return;

            // Escape quotes to avoid breaking console command
            msg = msg.Replace("\"", "\\\"");

            SdtdConsole.Instance.ExecuteSync($"sayplayer {entityId} \"{msg}\"", null);
        }

        private static string GetPlayerName(ClientInfo cInfo)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(cInfo.playerName))
                    return cInfo.playerName;
            }
            catch { }
            return null;
        }
    }
}
