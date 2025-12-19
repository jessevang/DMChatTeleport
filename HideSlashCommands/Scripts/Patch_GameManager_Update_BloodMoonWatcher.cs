using HarmonyLib;

namespace DMChatTeleports
{
    [HarmonyPatch(typeof(GameManager), "Update")]
    public class Patch_GameManager_Update_BloodMoonWatcher
    {
        private static int _lastDay = -1;
        private static int _lastHour = -1;
        private static bool _wasBloodMoon = false;

        static void Postfix()
        {
            var cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (cm == null || !cm.IsServer)
                return;

            var gm = GameManager.Instance;
            var world = gm != null ? gm.World : null;
            if (world == null)
                return;

            if (gm.gameStateManager == null || !gm.gameStateManager.IsGameStarted())
                return;

            // RP tick system
            DMChatTeleport.RewardPointsManager.Update();

            bool isBloodMoon = DMChatTeleport.BloodMoonUtil.IsActiveNow();

            if (isBloodMoon && DMChatTeleport.BloodMoonKillTracker.IsCounting)
                DMChatTeleport.BloodMoonKillTracker.MarkOnlinePresenceFromRewardSystem();

            var info = DMChatTeleport.BloodMoonUtil.GetDebugInfo();
            int day = info.day;
            int hour = info.hour;

            if (day == _lastDay && hour == _lastHour)
                return;

            _lastDay = day;
            _lastHour = hour;

            if (!_wasBloodMoon && isBloodMoon)
            {
                _wasBloodMoon = true;
                DMChatTeleport.BloodMoonKillTracker.StartTracking();
                DMChatTeleport.BloodMoonKillTracker.Broadcast("Blood Moon started! Kill tracking is ON.");
                DMChatTeleport.BloodMoonKillTracker.MarkOnlinePresenceFromRewardSystem();
                return;
            }

            if (_wasBloodMoon && !isBloodMoon)
            {
                _wasBloodMoon = false;
                DMChatTeleport.BloodMoonKillTracker.BroadcastResultsAndReset();
                return;
            }
        }
    }
}
