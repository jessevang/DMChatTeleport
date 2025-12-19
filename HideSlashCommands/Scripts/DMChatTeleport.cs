using HarmonyLib;
using UnityEngine;

namespace DMChatTeleport
{
    public class DMChatTeleportMod : IModApi
    {
        private const string HarmonyId = "DMChatTeleport.Mod";

        public void InitMod(Mod modInstance)
        {
            try
            {
                PlayerDataStore.PlayerStorage.Load();
                StarterKitManager.Load();
                ConfigManager.Load();

                var harmony = new Harmony(HarmonyId);
                harmony.PatchAll();

                Debug.Log("[DMChatTeleport] InitMod complete: data loaded, config loaded, Harmony patches applied.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DMChatTeleport] InitMod failed: {ex}");
            }

            // Player joined/spawned
            ModEvents.PlayerSpawnedInWorld.RegisterHandler((ref ModEvents.SPlayerSpawnedInWorldData data) =>
            {
                var cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
                if (cm == null || !cm.IsServer)
                    return;

                // IMPORTANT: WelcomeManager must resolve persistent id consistently (Steam_ or EOS_).
                WelcomeManager.OnPlayerSpawned(data.ClientInfo, data.EntityId);
            });

            // Player disconnected
            ModEvents.PlayerDisconnected.RegisterHandler((ref ModEvents.SPlayerDisconnectedData data) =>
            {
                WelcomeManager.OnPlayerDisconnected(data.ClientInfo);
            });
        }
    }
}
