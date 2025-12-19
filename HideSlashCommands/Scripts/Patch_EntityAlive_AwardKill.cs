using HarmonyLib;

namespace DMChatTeleport
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

            if (__instance.entityType != EntityType.Zombie)
                return;

            if (!(killer is EntityPlayer))
                return;

            var cInfo = ConnectionManager.Instance?.Clients?.ForEntityId(killer.entityId);
            if (cInfo == null)
                return;

            string playerId = PlayerIdUtil.GetPersistentIdOrNull(cInfo);
            if (string.IsNullOrWhiteSpace(playerId))
                return;

            string playerName = cInfo.playerName;

            // Real partyId if in party, otherwise unique solo "party-of-1" id.
            int realPartyId = PartyUtil.TryGetPartyIdForEntity(killer); // 0 for solo
            int effectivePartyId = (realPartyId > 0)
                ? realPartyId
                : -cInfo.entityId; // unique per solo player (negative avoids collisions)

            BloodMoonKillTracker.AddKill(playerId, playerName, effectivePartyId);
        }
    }
}
