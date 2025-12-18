using HarmonyLib;
using DMChatTeleport;
using System.Collections.Generic;
using DMChatTeleports;

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

        bool hideSlash = ConfigManager.Config.TurnOnHideCommandsWithSlashes;

        // Only parse slash commands if they START with '/'
        if (_msg.StartsWith("/"))
        {
            // ---------------------------------------------------------
            // 1) If command hiding is OFF, allow other Harmony patches
            //    AND the original game code to run normally
            // ---------------------------------------------------------
            if (!hideSlash)
            {
                return true; // DO NOT BLOCK anything
            }

            // ---------------------------------------------------------
            // 2) Extract steamId from ClientInfo.InternalId
            // ---------------------------------------------------------
            string id = _cInfo.InternalId.ToString();  // Example: "Steam_76561198350062436"
            string steamId = id.Replace("Steam_", "").Trim();

            string command = _msg.Trim().ToLower();

            // ---------------------------------------------------------
            // 3) Check teleport commands toggle
            // ---------------------------------------------------------
            if (command.StartsWith("/setbase") ||
                command.StartsWith("/base") ||
                command.StartsWith("/return"))
            {
                if (!ConfigManager.Config.TurnOnTeleportCommands)
                {
                    // Teleport commands disabled – DO NOT EXECUTE our code
                    // DO NOT block the message, because user may need feedback
                    return true;
                }
            }

            // ---------------------------------------------------------
            // 4) Starter kits toggle
            // ---------------------------------------------------------
            if (command.StartsWith("/starter") ||
                command.StartsWith("/kit") ||
                command.StartsWith("/pick"))
            {
                if (!ConfigManager.Config.TurnOnStarterKits)
                {
                    // Starter kits disabled — let original run
                    return true;
                }
            }

            // ---------------------------------------------------------
            // 5) Execute our commands (command logic already checks validity)
            // ---------------------------------------------------------
            CommandHandler.ProcessCommand(steamId, _senderEntityId, command);

            // ---------------------------------------------------------
            // 6) IMPORTANT:
            //    Return FALSE because hiding is ON, so we block message
            // ---------------------------------------------------------
            return false;
        }

        // Not a slash command → process normally
        return true;
    }
}
