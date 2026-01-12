using System;
using System.Collections.Generic;

namespace DMChatTeleport
{

    public class ConsoleCmdAddrp : ConsoleCmdAbstract
    {
        public override string getDescription() => "[DMChatTeleport] Add Reward Points to a player";
        public override string getHelp() =>
            "Usage:\n" +
            "  addrp <playerName|entityId|playerId> <amount>\n" +
            "Example:\n" +
            "  addrp jvang 100\n";
        public override string[] getCommands() => new[] { "addrp" };

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try
            {
                string cmd = "/addrp";
                if (_params != null && _params.Count > 0)
                    cmd += " " + string.Join(" ", _params);

                if (!CommandHandlerAdmin.TryHandle(null, 0, cmd))
                    SdtdConsole.Instance.Output(getHelp());
            }
            catch (Exception e)
            {
                SdtdConsole.Instance.Output("[DMChatTeleport] addrp error: " + e);
            }
        }
    }

    public class ConsoleCmdSetrp : ConsoleCmdAbstract
    {
        public override string getDescription() => "[DMChatTeleport] Set Reward Points for a player";
        public override string getHelp() =>
            "Usage:\n" +
            "  setrp <playerName|entityId|playerId> <amount>\n" +
            "Example:\n" +
            "  setrp jvang 500\n";
        public override string[] getCommands() => new[] { "setrp" };

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try
            {
                string cmd = "/setrp";
                if (_params != null && _params.Count > 0)
                    cmd += " " + string.Join(" ", _params);

                if (!CommandHandlerAdmin.TryHandle(null, 0, cmd))
                    SdtdConsole.Instance.Output(getHelp());
            }
            catch (Exception e)
            {
                SdtdConsole.Instance.Output("[DMChatTeleport] setrp error: " + e);
            }
        }
    }

    public class ConsoleCmdGetrp : ConsoleCmdAbstract
    {
        public override string getDescription() => "[DMChatTeleport] Get Reward Points for a player";
        public override string getHelp() =>
            "Usage:\n" +
            "  getrp <playerName|entityId|playerId>\n" +
            "Example:\n" +
            "  getrp jvang\n";
        public override string[] getCommands() => new[] { "getrp" };

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try
            {
                string cmd = "/getrp";
                if (_params != null && _params.Count > 0)
                    cmd += " " + string.Join(" ", _params);

                if (!CommandHandlerAdmin.TryHandle(null, 0, cmd))
                    SdtdConsole.Instance.Output(getHelp());
            }
            catch (Exception e)
            {
                SdtdConsole.Instance.Output("[DMChatTeleport] getrp error: " + e);
            }
        }
    }

    public class ConsoleCmdReloadconfig : ConsoleCmdAbstract
    {
        public override string getDescription() => "[DMChatTeleport] Reload DMChatTeleport config";
        public override string getHelp() => "Usage:\n  reloadconfig\n";
        public override string[] getCommands() => new[] { "reloadconfig" };

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try
            {
                // no args expected, but harmless
                string cmd = "/reloadconfig";
                if (_params != null && _params.Count > 0)
                    cmd += " " + string.Join(" ", _params);

                if (!CommandHandlerAdmin.TryHandle(null, 0, cmd))
                    SdtdConsole.Instance.Output(getHelp());
            }
            catch (Exception e)
            {
                SdtdConsole.Instance.Output("[DMChatTeleport] reloadconfig error: " + e);
            }
        }
    }

    public class ConsoleCmdPlayers : ConsoleCmdAbstract
    {
        public override string getDescription() => "[DMChatTeleport] List online players";
        public override string getHelp() =>
            "Usage:\n" +
            "  players\n" +
            "  listplayers\n";
        public override string[] getCommands() => new[] { "players", "listplayers" };

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try
            {
                // Route both aliases to the same admin handler verb
                string cmd = "/players";
                if (!CommandHandlerAdmin.TryHandle(null, 0, cmd))
                    SdtdConsole.Instance.Output(getHelp());
            }
            catch (Exception e)
            {
                SdtdConsole.Instance.Output("[DMChatTeleport] players error: " + e);
            }
        }
    }
}
