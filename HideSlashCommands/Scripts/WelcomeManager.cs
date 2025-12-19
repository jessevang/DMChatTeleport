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

            string name = GetPlayerName(cInfo) ?? playerId;
            int rp = pd.RewardPoints;

            SendPrivate(entityId, $"Welcome back, {name}. Your current Reward Points (RP): {rp}");

            var cfg = ConfigManager.Config;
            if (cfg != null && cfg.TurnOnStarterKits && !pd.HasPickedStarterKit)
            {
                SendPrivate(entityId, "Don’t forget: /liststarterkits and /pick <starterkitname> to get starting items. Choose /pick random get starterkit items and tier 1 quest tickets x2");
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
