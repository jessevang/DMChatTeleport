using HarmonyLib;

namespace DMChatTeleports
{
    [HarmonyPatch(typeof(EntityAlive), "AwardKill")]
    public class Patch_EntityAlive_AwardKill
    {
        static void Postfix(EntityAlive __instance, EntityAlive killer)
        {
            if (!DMChatTeleport.BloodMoonKillTracker.IsCounting)
                return;

            if (__instance == null || killer == null)
                return;

            if (__instance.entityType != EntityType.Zombie)
                return;

            if (!(killer is EntityPlayer))
                return;

            var cInfo = ConnectionManager.Instance?.Clients?.ForEntityId(killer.entityId);
            if (cInfo == null)
                return;

            string playerId = DMChatTeleport.PlayerIdUtil.GetPersistentIdOrNull(cInfo);
            if (string.IsNullOrWhiteSpace(playerId))
                return;

            string playerName = cInfo.playerName;

            int partyId = PartyUtil.TryGetPartyIdForEntity(killer); // 0 = Solo
            DMChatTeleport.BloodMoonKillTracker.AddKill(playerId, playerName, partyId);
        }
    }
}
