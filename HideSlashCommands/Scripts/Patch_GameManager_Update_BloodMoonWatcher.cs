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
            if (SingletonMonoBehaviour<ConnectionManager>.Instance == null ||
                !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                return;

            var gm = GameManager.Instance;
            var world = gm != null ? gm.World : null;
            if (world == null)
                return;

            if (gm.gameStateManager == null || !gm.gameStateManager.IsGameStarted())
                return;

            var info = BloodMoonUtil.GetDebugInfo();
            int day = info.day;
            int hour = info.hour;

            if (day == _lastDay && hour == _lastHour)
                return;

            _lastDay = day;
            _lastHour = hour;

            bool isBloodMoon = BloodMoonUtil.IsActiveNow();

            if (!_wasBloodMoon && isBloodMoon)
            {
                _wasBloodMoon = true;
                BloodMoonKillTracker.StartTracking();
                BloodMoonKillTracker.Broadcast("Blood Moon started! Kill tracking is ON.");
                return;
            }

            if (_wasBloodMoon && !isBloodMoon)
            {
                _wasBloodMoon = false;
                BloodMoonKillTracker.BroadcastResultsAndReset();
                return;
            }
        }
    }
}
