using HarmonyLib;

namespace DMChatTeleports
{
    [HarmonyPatch(typeof(EntityAlive), "AwardKill")]
    public class Patch_EntityAlive_AwardKill
    {
        static void Postfix(EntityAlive __instance, EntityAlive killer)
        {
            if (!BloodMoonKillTracker.IsCounting)
                return;

            if (__instance == null || killer == null)
                return;

            // Count zombie kills
            if (__instance.entityType != EntityType.Zombie)
                return;

            // Credit player kills
            if (!(killer is EntityPlayer))
                return;

            var cInfo = ConnectionManager.Instance?.Clients?.ForEntityId(killer.entityId);
            if (cInfo == null)
                return;

            string playerId = cInfo.InternalId.ToString();   // Steam_... or EOS_...
            string playerName = cInfo.playerName;

            BloodMoonKillTracker.AddKill(playerId, playerName);
        }
    }
}
