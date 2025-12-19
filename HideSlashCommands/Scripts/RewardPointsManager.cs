using System;
using System.Collections.Generic;
using UnityEngine;

namespace DMChatTeleport
{
    internal static class RewardPointsManager
    {
        private static float _nextTickTime;
        private static float _nextSaveTime;

        private static readonly HashSet<string> _onlineNow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _onlinePrev = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _lock = new object();
        private static string _lastDebugSample = null;

        public static void Update()
        {
            var cfg = ConfigManager.Config;
            if (cfg?.RewardPoints == null) return;
            if (!cfg.RewardPoints.Enabled) return;

            int tickSeconds = Math.Max(1, cfg.RewardPoints.TickSeconds);
            float now = Time.realtimeSinceStartup;

            if (now < _nextTickTime)
                return;

            _nextTickTime = now + tickSeconds;

            try
            {
                Tick(cfg);
            }
            catch (Exception ex)
            {
                Debug.Log($"[DMChatTeleport] RewardPointsManager.Tick failed: {ex}");
            }
        }

        private static void Tick(ModConfig cfg)
        {
            int minutesPerPoint = Math.Max(1, cfg.RewardPoints.MinutesPerPoint);
            int secondsPerRP = minutesPerPoint * 60;

            _onlineNow.Clear();

            var clients = ConnectionManager.Instance?.Clients?.List;
            if (clients == null)
                return;

            long nowTicks = DateTime.UtcNow.Ticks;

            foreach (var c in clients)
            {
                if (c == null || c.entityId <= 0)
                    continue;

                // Your util should prefer CrossplatformId (EOS_...) then fallback to PlatformId (Steam_...)
                string playerId = PlayerIdUtil.GetPersistentIdOrNull(c);
                if (string.IsNullOrWhiteSpace(playerId))
                    continue;

                _onlineNow.Add(playerId);

                var pd = PlayerDataStore.PlayerStorage.Get(playerId);

                pd.entityId = c.entityId;

                if (pd.OnlineSessionStartUtcTicks == 0)
                    pd.OnlineSessionStartUtcTicks = nowTicks;

                if (pd.LastOnlineSeenUtcTicks != 0)
                {
                    int deltaSeconds = (int)Math.Max(
                        0,
                        TimeSpan.FromTicks(nowTicks - pd.LastOnlineSeenUtcTicks).TotalSeconds
                    );

                    if (deltaSeconds > 0)
                    {
                        pd.AccumulatedOnlineSeconds += deltaSeconds;
                        pd.TotalOnlineSecondsLifetime += deltaSeconds;
                    }
                }

                pd.LastOnlineSeenUtcTicks = nowTicks;

                if (pd.AccumulatedOnlineSeconds >= secondsPerRP)
                {
                    int rpToGrant = pd.AccumulatedOnlineSeconds / secondsPerRP;
                    pd.AccumulatedOnlineSeconds %= secondsPerRP;

                    PlayerDataStore.PlayerStorage.AddRP(playerId, rpToGrant);
                }
            }

            // disconnect handling (carry partial progress; reset session markers)
            foreach (var playerId in _onlinePrev)
            {
                if (_onlineNow.Contains(playerId))
                    continue;

                var pd = PlayerDataStore.PlayerStorage.Get(playerId);
                pd.OnlineSessionStartUtcTicks = 0;
                pd.LastOnlineSeenUtcTicks = 0;
            }

            // publish snapshot (copy into _onlinePrev)
            _onlinePrev.Clear();
            foreach (var id in _onlineNow)
                _onlinePrev.Add(id);

            // optional debug sample (helps confirm EOS vs Steam IDs quickly)
            if (_onlinePrev.Count > 0)
            {
                // log only when it changes (reduces spam)
                string sample = string.Join(", ", _onlinePrev);
                if (!string.Equals(sample, _lastDebugSample, StringComparison.Ordinal))
                {
                    _lastDebugSample = sample;
                    // Uncomment if you want:
                    // Debug.Log($"[DMChatTeleport] RewardPoints online snapshot: {sample}");
                }
            }

            // Persist periodically (NOT every tick)
            int saveInterval = Math.Max(5, cfg.RewardPoints.SaveIntervalSeconds);
            float now = Time.realtimeSinceStartup;
            if (now >= _nextSaveTime)
            {
                _nextSaveTime = now + saveInterval;
                PlayerDataStore.PlayerStorage.Save();
            }
        }

        /// <summary>
        /// Returns a safe copy of the last computed snapshot of online player IDs.
        /// IDs may be "EOS_..." when CrossplatformId is available, else "Steam_..." etc.
        /// </summary>
        public static IReadOnlyCollection<string> GetOnlinePlayerIdsSnapshot()
        {
            // Return a copy so callers can't mutate our internal set
            // and so callers don't crash if Tick modifies it.
            return new List<string>(_onlinePrev);
        }
    }
}
