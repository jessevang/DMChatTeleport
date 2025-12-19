using HarmonyLib;
using DMChatTeleport;
using System.Collections.Generic;

[HarmonyPatch(typeof(GameManager))]
[HarmonyPatch("ChatMessageServer")]
[HarmonyPatch(new[] {
    typeof(ClientInfo),
    typeof(EChatType),
    typeof(int),
    typeof(string),
    typeof(List<int>),
    typeof(EMessageSender),
    typeof(GeneratedTextManager.BbCodeSupportMode)
})]
public class Patch_ChatMessageServer
{
    static bool Prefix(
        ClientInfo _cInfo,
        EChatType _chatType,
        int _senderEntityId,
        ref string _msg,
        List<int> _recipientEntityIds,
        EMessageSender _msgSender,
        GeneratedTextManager.BbCodeSupportMode _bbMode
    )
    {
        if (string.IsNullOrEmpty(_msg))
            return true;

        bool hideSlash = ConfigManager.Config != null && ConfigManager.Config.TurnOnHideCommandsWithSlashes;

        // Only parse slash commands if they START with '/'
        if (!_msg.StartsWith("/"))
            return true;

        // If hiding is OFF, let vanilla + other mods handle it normally
        if (!hideSlash)
            return true;

        // Build persistent id (EOS_... preferred, else Steam_..., etc)
        string playerId = PlayerIdUtil.GetPersistentIdOrNull(_cInfo);
        if (string.IsNullOrWhiteSpace(playerId))
        {
            // Can't identify player -> let original run so at least they see something
            return true;
        }

        string command = _msg.Trim();

        // Feature toggles
        if (command.StartsWith("/setbase") || command.StartsWith("/base") || command.StartsWith("/return"))
        {
            if (ConfigManager.Config == null || !ConfigManager.Config.TurnOnTeleportCommands)
                return true; // allow original
        }

        if (command.StartsWith("/starter") || command.StartsWith("/kit") || command.StartsWith("/pick") || command.StartsWith("/liststarterkits"))
        {
            if (ConfigManager.Config == null || !ConfigManager.Config.TurnOnStarterKits)
                return true; // allow original
        }

        // Execute our commands
        // IMPORTANT: playerId is now "EOS_..." or "Steam_..." (full string, no stripping)
        try
        {
            CommandHandler.ProcessCommand(playerId, _senderEntityId, command);
        }
        catch (System.Exception ex)
        {
            SdtdConsole.Instance.ExecuteSync($"sayplayer {_senderEntityId} \"Command error: {ex.Message}\"", null);
        }

        // Hiding is ON so block the chat message from showing globally
        return false;
    }
}
