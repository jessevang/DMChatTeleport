using System;

namespace DMChatTeleport
{
    internal static class PlayerIdUtil
    {
        /// <summary>
        /// Preferred persistent ID for crossplay.
        /// In this 7DTD build:
        /// - ClientInfo.CrossplatformId is EOS ("EOS_...")
        /// - ClientInfo.PlatformId is Steam/Console ("Steam_...", etc)
        /// We prefer EOS when available, otherwise fall back to Platform.
        /// </summary>
        public static string GetPersistentIdOrNull(ClientInfo cInfo)
        {
            if (cInfo == null)
                return null;

            try
            {
                // BEST: EOS crossplay id (your log prints this as CrossId)
                var cross = cInfo.CrossplatformId;
                if (cross != null)
                {
                    string s = cross.CombinedString;
                    if (!string.IsNullOrWhiteSpace(s))
                        return s.Trim();
                }

                // Fallback: platform id (Steam_..., etc)
                var plat = cInfo.PlatformId;
                if (plat != null)
                {
                    string s = plat.CombinedString;
                    if (!string.IsNullOrWhiteSpace(s))
                        return s.Trim();
                }

                // Last resort (already does Cross ?? Platform, but still use CombinedString)
                var internalId = cInfo.InternalId;
                if (internalId != null)
                {
                    string s = internalId.CombinedString;
                    if (!string.IsNullOrWhiteSpace(s))
                        return s.Trim();
                }
            }
            catch { }

            return null;
        }

        public static bool IsEosId(string id)
        {
            return !string.IsNullOrWhiteSpace(id) &&
                   id.StartsWith("EOS_", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSteamId(string id)
        {
            return !string.IsNullOrWhiteSpace(id) &&
                   id.StartsWith("Steam_", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Convenience if you specifically want EOS when present, else null.
        /// </summary>
        public static string GetEosIdOrNull(ClientInfo cInfo)
        {
            string id = GetPersistentIdOrNull(cInfo);
            return IsEosId(id) ? id : null;
        }
    }
}
