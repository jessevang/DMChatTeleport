using HarmonyLib;
using static DMChatTeleport.DataModel;
using DMChatTeleport;

public class DMChatTeleportMod : IModApi
{
    public void InitMod(Mod modInstance)
    {
        Harmony harmony = new Harmony("DMTeleportCommands");
        harmony.PatchAll();
        PlayerStorage.Load();
        StarterKitManager.Load();
        ConfigManager.Load();
        // StatsTracker.Init();  <Can revisit code in the future>
    }
}
