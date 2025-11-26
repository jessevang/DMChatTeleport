using DMChatTeleport;
using System;
using System.Collections.Generic;
using UnityEngine;
using DataPlayer = DMChatTeleport.DataModel.PlayerData;
using PlayerStorage = DMChatTeleport.DataModel.PlayerStorage;

public static class CommandHandler
{
    public static void ProcessCommand(string steamId, int entityId, string cmd)
    {
        PlayerStorage.Load();

        DataPlayer player = PlayerStorage.Get(steamId);
        player.entityId = entityId;

        World world = GameManager.Instance.World;


        // --------------------------------------------------------------------
        // Reloads Config Options
        // --------------------------------------------------------------------
        if (cmd == "/reloadconfig")
        {
            ConfigManager.Load();
            SendServerMessage(entityId, "Config reloaded.");
            return;
        }

        // --------------------------------------------------------------------
        // TELEPORT COMMANDS DISABLED?
        // --------------------------------------------------------------------
        bool teleportsEnabled = ConfigManager.Config.TurnOnTeleportCommands;
        bool kitsEnabled = ConfigManager.Config.TurnOnStarterKits;





        // ====================================================================
        // TELEPORT: SETBASE
        // ====================================================================
        if (cmd == "/setbase")
        {
            if (!teleportsEnabled)
            {
                SendServerMessage(entityId, "Teleport commands are disabled on this server.");
                return;
            }

            EntityPlayer ep = world.GetEntity(entityId) as EntityPlayer;
            if (ep != null)
            {
                player.baseX = ep.position.x;
                player.baseY = ep.position.y;
                player.baseZ = ep.position.z;

                player.hasBase = true;

                SendServerMessage(entityId, "Base set!");
                PlayerStorage.Save();
            }
            return;
        }

        // ====================================================================
        // TELEPORT: /base
        // ====================================================================
        if (cmd == "/base")
        {
            if (!teleportsEnabled)
            {
                SendServerMessage(entityId, "Teleport commands are disabled on this server.");
                return;
            }

            if (!player.hasBase)
            {
                SendServerMessage(entityId, "No base defined. Use /setbase first.");
                return;
            }

            EntityPlayer ep = world.GetEntity(entityId) as EntityPlayer;
            if (ep != null)
            {
                player.returnX = ep.position.x;
                player.returnY = ep.position.y;
                player.returnZ = ep.position.z;

                player.hasReturn = true;

                Teleport(entityId, new Vector3(player.baseX, player.baseY, player.baseZ));

                SendServerMessage(entityId, "Teleported to base.");
                PlayerStorage.Save();
            }
            return;
        }

        // ====================================================================
        // TELEPORT: /return
        // ====================================================================
        if (cmd == "/return")
        {
            if (!teleportsEnabled)
            {
                SendServerMessage(entityId, "Teleport commands are disabled on this server.");
                return;
            }

            if (!player.hasReturn)
            {
                SendServerMessage(entityId, "No return location saved.");
                return;
            }

            Teleport(entityId, new Vector3(player.returnX, player.returnY, player.returnZ));

            SendServerMessage(entityId, "Returned.");
            player.hasReturn = false;
            PlayerStorage.Save();
            return;
        }

        // ====================================================================
        // HELP COMMAND (Dynamic)
        // ====================================================================
        if (cmd == "/help")
        {
            SendServerMessage(entityId, "Commands:");

            if (teleportsEnabled)
            {
                SendServerMessage(entityId, "/setbase - sets teleport home");
                SendServerMessage(entityId, "/base - teleports to home");
                SendServerMessage(entityId, "/return - goes back to previous location");
            }

            if (kitsEnabled)
            {
                SendServerMessage(entityId, "/liststarterkits - lists all starter kits");
                SendServerMessage(entityId, "/pick <name> - Pick a starter kit");
                SendServerMessage(entityId, "/pick Random - Pick a random kit (gives bonus item)");
            }

            return;
        }

        // ====================================================================
        // STARTER KITS DISABLED?
        // ====================================================================
        if (!kitsEnabled && (cmd.StartsWith("/pick") || cmd.StartsWith("/choose") || cmd == "/liststarterkits"))
        {
            SendServerMessage(entityId, "Starter kits are disabled on this server.");
            return;
        }

        // ====================================================================
        // LIST STARTER KITS
        // ====================================================================
        if (cmd == "/liststarterkits")
        {
            if (StarterKitManager.Kits.Count == 0)
            {
                SendServerMessage(entityId, "No starter kits available.");
                return;
            }

            SendServerMessage(entityId, "Available Starter Kits:");

            foreach (var kv in StarterKitManager.Kits)
            {
                var kit = kv.Value;
                SendServerMessage(entityId, $"- {kit.Name}: {kit.Description}");
            }

            return;
        }

        // ====================================================================
        // PICK STARTER KIT
        // ====================================================================
        if (cmd.StartsWith("/pick") || cmd.StartsWith("/choose"))
        {
            string[] split = cmd.Split(' ');
            if (split.Length < 2)
            {
                SendServerMessage(entityId, "Usage: /pick <name>");
                return;
            }

            string kitName = split[1];

            if (player.HasPickedStarterKit)
            {
                SendServerMessage(entityId, $"You already picked a starter kit: {player.PickedStarterKit}");
                return;
            }

            StarterKit kit;

            if (kitName.Equals("Random", StringComparison.OrdinalIgnoreCase))
            {
                var list = new List<StarterKit>(StarterKitManager.Kits.Values);
                if (list.Count == 0)
                {
                    SendServerMessage(entityId, "No starter kits available.");
                    return;
                }

                System.Random rnd = new System.Random();
                kit = list[rnd.Next(list.Count)];

                SendServerMessage(entityId, $"Random starter kit selected: {kit.Name}");

                // Bonus item for RANDOM pick
                GiveItemToPlayer(entityId, "adminT1QuestTicket", 2, 1);
            }
            else
            {
                // Normal selection
                if (!StarterKitManager.TryGetKit(kitName, out kit))
                {
                    SendServerMessage(entityId, "Starter kit not found.");
                    return;
                }
            }

            EntityPlayer ep = world.GetEntity(entityId) as EntityPlayer;
            if (ep == null)
                return;

            SendServerMessage(entityId, $"Starter kit '{kit.Name}' applied! Items dropped:");

            int i = 0;
            foreach (var item in kit.Items)
            {
                i++;
                int q = item.Quality <= 0 ? 1 : item.Quality;
                GiveItemToPlayer(entityId, item.ItemName, item.Count, q);
                SendServerMessage(entityId, $"{i}. {item.ItemName}, Qty:{item.Count}, Q:{q}");
            }

            player.HasPickedStarterKit = true;
            player.PickedStarterKit = kit.Name;
            PlayerStorage.Save();

            return;
        }
    }

    private static void Teleport(int entityId, Vector3 pos)
    {
        SdtdConsole.Instance.ExecuteSync(
            $"teleportplayer {entityId} {Mathf.RoundToInt(pos.x)} {Mathf.RoundToInt(pos.y)} {Mathf.RoundToInt(pos.z)}",
            null
        );
    }

    private static void SendServerMessage(int entityId, string msg)
    {
        SdtdConsole.Instance.ExecuteSync(
            $"sayplayer {entityId} \"{msg}\"",
            null
        );
    }

    public static void GiveItemToPlayer(int entityId, string itemName, int count, int quality = 1)
    {
        if (quality < 1) quality = 1;
        if (quality > 6) quality = 6;

        SdtdConsole.Instance.ExecuteSync(
            $"give {entityId} {itemName} {count} {quality}",
            null
        );

        Debug.Log($"[StarterKit] Gave {itemName} x{count} (Q{quality}) to player {entityId}");
    }
}
