using DMChatTeleport;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using DataPlayer = DMChatTeleport.DataModel.PlayerData;
using PlayerStorage = DMChatTeleport.DataModel.PlayerStorage;

public static class CommandHandler
{
    // Small delay between item grants (prevents client getting hammered)
    private const int StarterKitGiveDelayMs = 50; // tweak: 50–250ms

    public static void ProcessCommand(string steamId, int entityId, string cmd)
    {
        PlayerStorage.Load();

        DataPlayer player = PlayerStorage.Get(steamId);
        player.entityId = entityId;

        // Ensure player record exists and is persisted even if this is the first command they ever use
        // used to prevent players from getting infinite starterkits
        PlayerStorage.Save();

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

                if (!TryConsumeTeleportCooldown(entityId, player))
                    return;

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

            if (!TryConsumeTeleportCooldown(entityId, player))
                return;

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
        // STARTER KITS DISABLED
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

                // Bonus item for RANDOM pick (skip if invalid)
                GiveItemToPlayer(entityId, "adminT1QuestTicket", 2, 1);
                Thread.Sleep(StarterKitGiveDelayMs);
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

                if (item == null || string.IsNullOrWhiteSpace(item.ItemName) || item.Count <= 0)
                {
                    SendServerMessage(entityId, $"{i}. (skipped invalid item entry)");
                    Thread.Sleep(StarterKitGiveDelayMs);
                    continue;
                }

                int q = item.Quality <= 0 ? 1 : item.Quality;

                bool given = GiveItemToPlayer(entityId, item.ItemName, item.Count, q);

                if (given)
                    SendServerMessage(entityId, $"{i}. {item.ItemName}, Qty:{item.Count}, Q:{q}");
                else
                    SendServerMessage(entityId, $"{i}. (skipped) {item.ItemName} - item not found / invalid");

                Thread.Sleep(StarterKitGiveDelayMs);
            }

            player.HasPickedStarterKit = true;
            player.PickedStarterKit = kit.Name;
            PlayerStorage.Save();

            return;
        }

        /*
        // ====================================================================
        // Usage: /testkit <name>
        // COMMENT OUT AFTER TESTING
        // ====================================================================
        if (cmd.StartsWith("/testkit ", StringComparison.OrdinalIgnoreCase))
        {
            StarterKitManager.Load();

            string[] split = cmd.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2 || string.IsNullOrWhiteSpace(split[1]))
            {
                SendServerMessage(entityId, "Usage: /testkit <name>");
                return;
            }

            string kitName = split[1].Trim();

            if (!StarterKitManager.TryGetKit(kitName, out var kit) || kit == null)
            {
                SendServerMessage(entityId, $"Kit not found: {kitName}");
                return;
            }

            if (kit.Items == null || kit.Items.Count == 0)
            {
                SendServerMessage(entityId, $"Kit '{kit.Name}' has no items.");
                return;
            }

            SendServerMessage(entityId, $"[TEST] Giving kit: {kit.Name}");

            int i = 0;
            foreach (var item in kit.Items)
            {
                i++;

                if (item == null || string.IsNullOrWhiteSpace(item.ItemName) || item.Count <= 0)
                {
                    SendServerMessage(entityId, $"{i}. (skipped invalid item entry)");
                    Thread.Sleep(StarterKitGiveDelayMs);
                    continue;
                }

                int q = item.Quality <= 0 ? 1 : item.Quality;
                bool given = GiveItemToPlayer(entityId, item.ItemName, item.Count, q);

                if (given)
                    SendServerMessage(entityId, $"{i}. {item.ItemName} x{item.Count} (Q{q})");
                else
                    SendServerMessage(entityId, $"{i}. (skipped) {item.ItemName} - item not found / invalid");

                Thread.Sleep(StarterKitGiveDelayMs);
            }

            SendServerMessage(entityId, $"[TEST] Done: {kit.Name}");
            return;
        }
        

        
        // ====================================================================
        // TEMP TEST: GIVE ALL KITS (NO ADMIN CHECK)
        // Usage: /testkits
        // COMMENT OUT AFTER TESTING
        // ====================================================================
        if (cmd.Equals("/testkits", StringComparison.OrdinalIgnoreCase))
        {
            StarterKitManager.Load();

            if (StarterKitManager.Kits.Count == 0)
            {
                SendServerMessage(entityId, "[TEST] No starter kits available.");
                return;
            }

            SendServerMessage(entityId, $"[TEST] Giving ALL kits: {StarterKitManager.Kits.Count}");

            foreach (var kv in StarterKitManager.Kits)
            {
                var kit = kv.Value;
                if (kit?.Items == null || kit.Items.Count == 0)
                    continue;

                SendServerMessage(entityId, $"[TEST] Kit: {kit.Name}");

                foreach (var item in kit.Items)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.ItemName) || item.Count <= 0)
                        continue;

                    int q = item.Quality <= 0 ? 1 : item.Quality;

                    GiveItemToPlayer(entityId, item.ItemName, item.Count, q);

                    Thread.Sleep(StarterKitGiveDelayMs);
                }
            }

            SendServerMessage(entityId, "[TEST] Done giving all kits.");
            return;
        }

        */
        
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


    public static bool GiveItemToPlayer(int entityId, string itemName, int count, int quality = 1)
    {
        if (string.IsNullOrWhiteSpace(itemName) || count <= 0)
            return false;

        if (quality < 1) quality = 1;
        if (quality > 6) quality = 6;

        if (!TryResolveItem(itemName, out ItemValue resolved, out string failReason))
        {
            Debug.LogWarning($"[StarterKit] Skipping '{itemName}' for player {entityId}: {failReason}");
            return false;
        }

        int maxStack = 5000; 
        try
        {
            if (resolved?.ItemClass != null)
                maxStack = Math.Max(1, resolved.ItemClass.Stacknumber.Value);
        }
        catch
        {

        }

        int remaining = count;
        while (remaining > 0)
        {
            int giveNow = Math.Min(remaining, maxStack);

            bool ok = GiveItemViaForcedPickup(entityId, itemName, resolved, giveNow, quality);


            if (!ok)
            {
                SdtdConsole.Instance.ExecuteSync($"give {entityId} {itemName} {giveNow} {quality}", null);
                Debug.Log($"[StarterKit] Fallback give: {itemName} x{giveNow} (Q{quality}) to player {entityId}");
            }

            remaining -= giveNow;

            if (remaining > 0)
                Thread.Sleep(Math.Min(StarterKitGiveDelayMs, 75));
        }

        return true;
    }

    private static bool TryResolveItem(string itemName, out ItemValue itemValue, out string reason)
    {
        itemValue = null;
        reason = null;

        try
        {
            // resolve without changing the original string, but lookup case-insensitive
            ItemClass itemClass = ItemClass.GetItemClass(itemName, true) ?? ItemClass.GetItemClass(itemName, false); // fallback

            if (itemClass == null)
            {
                reason = "ItemClass not found (name mismatch, missing mod, or item not registered yet)";
                return false;
            }

            itemValue = new ItemValue(itemClass.Id, true);

            if (itemValue == null || itemValue.ItemClass == null || itemValue.type <= 0)
            {
                reason = "ItemValue could not be created (invalid type or ItemClass)";
                itemValue = null;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            reason = $"Exception resolving item: {ex.GetType().Name}: {ex.Message}";
            itemValue = null;
            return false;
        }
    }


    private static bool GiveItemViaForcedPickup(int entityId, string itemName, ItemValue resolved, int count, int quality = 1)
    {
        if (count <= 0 || resolved == null || resolved.ItemClass == null)
            return false;

        World world = GameManager.Instance.World;
        if (world == null) return false;

        ClientInfo cInfo = ConnectionManager.Instance.Clients.ForEntityId(entityId);
        if (cInfo == null) return false;

        EntityPlayer player = world.GetEntity(entityId) as EntityPlayer;
        if (player == null || !player.IsSpawned() || player.IsDead()) return false;

        if (quality < 1) quality = 1;
        if (quality > 6) quality = 6;

        ItemStack stack = new ItemStack(
            new ItemValue(resolved.type, quality, quality, false, null, 1f),
            count
        );

        EntityItem entityItem = (EntityItem)EntityFactory.CreateEntity(new EntityCreationData
        {
            entityClass = EntityClass.FromString("item"),
            id = EntityFactory.nextEntityID++,
            itemStack = stack,
            pos = player.position,
            rot = new Vector3(20f, 0f, 20f),
            lifetime = 60f,
            belongsPlayerId = entityId
        });

        world.SpawnEntityInWorld(entityItem);

        cInfo.SendPackage(
            NetPackageManager.GetPackage<NetPackageEntityCollect>()
                .Setup(entityItem.entityId, entityId)
        );

        world.RemoveEntity(entityItem.entityId, EnumRemoveEntityReason.Despawned);

        Debug.Log($"[StarterKit] Forced pickup of {itemName} x{count} (Q{quality}) for player {entityId}.");
        return true;
    }


    private static bool TryConsumeTeleportCooldown(int entityId, DataPlayer player)
    {
        int cdSeconds = ConfigManager.Config?.TeleportCooldownSeconds ?? 0;
        if (cdSeconds <= 0)
            return true;

        long nowTicks = DateTime.UtcNow.Ticks;
        long lastTicks = player.LastTeleportUtcTicks;

        if (lastTicks > 0)
        {
            TimeSpan elapsed = new TimeSpan(nowTicks - lastTicks);
            if (elapsed.TotalSeconds < cdSeconds)
            {
                int remain = (int)Math.Ceiling(cdSeconds - elapsed.TotalSeconds);
                SendServerMessage(entityId, $"Teleport is on cooldown. Try again in {remain}s.");
                return false;
            }
        }


        player.LastTeleportUtcTicks = nowTicks;
        PlayerStorage.Save();
        return true;
    }
}
