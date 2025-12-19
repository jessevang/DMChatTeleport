using DMChatTeleport;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using DataPlayer = DMChatTeleport.PlayerDataStore.PlayerData;
using PlayerStorage = DMChatTeleport.PlayerDataStore.PlayerStorage;

namespace DMChatTeleport
{
    public static class CommandHandler
    {
        // Small delay between item grants (prevents client getting hammered)
        private const int StarterKitGiveDelayMs = 50; // tweak: 50–250ms

        /// <summary>
        /// playerId must be a persistent ID string (EOS_... OR Steam_... OR other PlatformId/CrossId CombinedString).
        /// entityId is used for teleport/give/sayplayer commands.
        /// </summary>
        public static void ProcessCommand(string playerId, int entityId, string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd))
                return;

            // Accept ANY non-empty persistent id. Do NOT enforce EOS-only.
            if (string.IsNullOrWhiteSpace(playerId))
            {
                Debug.LogWarning("[DMChatTeleport] CommandHandler.ProcessCommand called with empty playerId.");
                return;
            }

            bool created;
            DataPlayer player = PlayerStorage.GetOrCreate(playerId, out created);
            player.entityId = entityId;

            if (created)
                PlayerStorage.Save();

            World world = GameManager.Instance.World;
            if (world == null)
                return;

            // --------------------------------------------------------------------
            // Reloads Config Options
            // --------------------------------------------------------------------
            if (cmd.Equals("/reloadconfig", StringComparison.OrdinalIgnoreCase))
            {
                ConfigManager.Load();
                SendServerMessage(entityId, "Config reloaded.");
                return;
            }



            // --------------------------------------------------------------------
            // TELEPORT COMMANDS DISABLED?
            // --------------------------------------------------------------------
            bool teleportsEnabled = ConfigManager.Config != null && ConfigManager.Config.TurnOnTeleportCommands;
            bool kitsEnabled = ConfigManager.Config != null && ConfigManager.Config.TurnOnStarterKits;

            // --------------------------------------------------------------------
            // CheckIfBloodmoonisActive
            // ====================================================================
            if (cmd.Equals("/isbloodmoon", StringComparison.OrdinalIgnoreCase) ||
                cmd.Equals("/isbloomoon", StringComparison.OrdinalIgnoreCase))
            {
                bool active = BloodMoonUtil.IsActiveNow();
                var info = BloodMoonUtil.GetDebugInfo();

                SendServerMessage(entityId,
                    $"Blood Moon Active: {active} | Day={info.day} Hour={info.hour} | BMDay={info.bmDay} | Dusk={info.dusk} Dawn={info.dawn}");

                return;
            }

            // Shop & RP commands
            if (CommandHandlerShop.TryHandle(playerId, entityId, cmd))
                return;


            // ====================================================================
            // TELEPORT: SETBASE
            // ====================================================================
            if (cmd.Equals("/setbase", StringComparison.OrdinalIgnoreCase))
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
            if (cmd.Equals("/base", StringComparison.OrdinalIgnoreCase))
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
            if (cmd.Equals("/return", StringComparison.OrdinalIgnoreCase))
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
            if (cmd.Equals("/help", StringComparison.OrdinalIgnoreCase))
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
            if (!kitsEnabled && (cmd.StartsWith("/pick", StringComparison.OrdinalIgnoreCase) ||
                                cmd.StartsWith("/choose", StringComparison.OrdinalIgnoreCase) ||
                                cmd.Equals("/liststarterkits", StringComparison.OrdinalIgnoreCase)))
            {
                SendServerMessage(entityId, "Starter kits are disabled on this server.");
                return;
            }

            // ====================================================================
            // LIST STARTER KITS
            // ====================================================================
            if (cmd.Equals("/liststarterkits", StringComparison.OrdinalIgnoreCase))
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
            if (cmd.StartsWith("/pick", StringComparison.OrdinalIgnoreCase) ||
                cmd.StartsWith("/choose", StringComparison.OrdinalIgnoreCase))
            {
                string[] split = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length < 2)
                {
                    SendServerMessage(entityId, "Usage: /pick <name>");
                    return;
                }

                // Supports kit names with spaces if you ever want it later:
                // /pick "Cool Kit Name"
                // For now: join everything after /pick
                string kitName = string.Join(" ", split, 1, split.Length - 1);

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
            catch { }

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
                ItemClass itemClass = ItemClass.GetItemClass(itemName, true) ?? ItemClass.GetItemClass(itemName, false);

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
}
