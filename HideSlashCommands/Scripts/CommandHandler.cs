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

            string lang = PlayerStorage.GetLanguage(playerId, "en");

            World world = GameManager.Instance.World;
            if (world == null)
                return;





            // --------------------------------------------------------------------
            // LANGUAGE: /lang [code]
            // --------------------------------------------------------------------
            // /lang           -> shows current + available
            // /lang en        -> sets English
            // /lang ja        -> sets Japanese
            if (cmd.Equals("/lang", StringComparison.OrdinalIgnoreCase) ||
                cmd.StartsWith("/lang ", StringComparison.OrdinalIgnoreCase))
            {
                string[] split = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                var available = new HashSet<string>(L.GetAvailableLanguages(), StringComparer.OrdinalIgnoreCase);
                string list = string.Join(", ", available);

                // List current & available
                if (split.Length == 1)
                {
                    string current = PlayerStorage.GetLanguage(playerId, "en");

                    SendServerMessage(entityId, L.Format(current, "lang.current", ("lang", current)));
                    SendServerMessage(entityId, L.Format(current, "lang.available", ("list", list)));
                    SendServerMessage(entityId, L.Get(current, "lang.usage"));
                    return;
                }

                string requested = split[1].Trim().ToLowerInvariant();

                // If the requested language file doesn't exist, reject.
                if (!available.Contains(requested))
                {
                    string current = PlayerStorage.GetLanguage(playerId, "en");
                    SendServerMessage(entityId, L.Format(current, "lang.invalid", ("list", list)));
                    SendServerMessage(entityId, L.Get(current, "lang.usage"));
                    return;
                }

                PlayerStorage.SetLanguage(playerId, requested);
                PlayerStorage.Save();

                // Confirm in the NEW language
                SendServerMessage(entityId, L.Format(requested, "lang.set", ("lang", requested)));
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

                SendServerMessage(entityId, L.Format(lang, "cmd.isbloodmoon.result",
                    ("active", active),
                    ("day", info.day),
                    ("hour", info.hour),
                    ("bmDay", info.bmDay),
                    ("dusk", info.dusk),
                    ("dawn", info.dawn)
                ));

                return;
            }



            // Route to Admin/Console commands
            if (CommandHandlerAdmin.TryHandle(playerId, entityId, cmd))
                return;


            // Shop & RP commands (these should also localize internally later)
            if (CommandHandlerShop.TryHandle(playerId, entityId, cmd))
                return;

            // ====================================================================
            // TELEPORT: SETBASE
            // ====================================================================
            if (cmd.Equals("/setbase", StringComparison.OrdinalIgnoreCase))
            {
                if (!teleportsEnabled)
                {
                    SendServerMessage(entityId, L.Get(lang, "cmd.teleport.disabled"));
                    return;
                }

                EntityPlayer ep = world.GetEntity(entityId) as EntityPlayer;
                if (ep != null)
                {
                    player.baseX = ep.position.x;
                    player.baseY = ep.position.y;
                    player.baseZ = ep.position.z;

                    player.hasBase = true;

                    SendServerMessage(entityId, L.Get(lang, "cmd.setbase.ok"));
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
                    SendServerMessage(entityId, L.Get(lang, "cmd.teleport.disabled"));
                    return;
                }

                if (!player.hasBase)
                {
                    SendServerMessage(entityId, L.Get(lang, "cmd.base.no_base"));
                    return;
                }

                EntityPlayer ep = world.GetEntity(entityId) as EntityPlayer;
                if (ep != null)
                {
                    player.returnX = ep.position.x;
                    player.returnY = ep.position.y;
                    player.returnZ = ep.position.z;

                    player.hasReturn = true;

                    if (!TryConsumeTeleportCooldown(entityId, player, lang))
                        return;

                    Teleport(entityId, new Vector3(player.baseX, player.baseY, player.baseZ));

                    SendServerMessage(entityId, L.Get(lang, "cmd.base.ok"));
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
                    SendServerMessage(entityId, L.Get(lang, "cmd.teleport.disabled"));
                    return;
                }

                if (!player.hasReturn)
                {
                    SendServerMessage(entityId, L.Get(lang, "cmd.return.no_saved"));
                    return;
                }

                if (!TryConsumeTeleportCooldown(entityId, player, lang))
                    return;

                Teleport(entityId, new Vector3(player.returnX, player.returnY, player.returnZ));

                SendServerMessage(entityId, L.Get(lang, "cmd.return.ok"));
                player.hasReturn = false;
                PlayerStorage.Save();
                return;
            }

            // ====================================================================
            // HELP COMMAND (Dynamic)
            // ====================================================================
            if (cmd.Equals("/help", StringComparison.OrdinalIgnoreCase))
            {
                SendServerMessage(entityId, L.Get(lang, "cmd.help.header"));

                if (teleportsEnabled)
                {
                    SendServerMessage(entityId, L.Get(lang, "cmd.help.setbase"));
                    SendServerMessage(entityId, L.Get(lang, "cmd.help.base"));
                    SendServerMessage(entityId, L.Get(lang, "cmd.help.return"));
                }

                if (kitsEnabled)
                {
                    SendServerMessage(entityId, L.Get(lang, "cmd.help.liststarterkits"));
                    SendServerMessage(entityId, L.Get(lang, "cmd.help.pick"));
                    SendServerMessage(entityId, L.Get(lang, "cmd.help.pick_random"));
                }

                SendServerMessage(entityId, L.Get(lang, "cmd.help.lang"));
                SendServerMessage(entityId, L.Get(lang, "cmd.help.reloadconfig"));
                SendServerMessage(entityId, L.Get(lang, "cmd.help.isbloodmoon"));

                return;
            }

            // ====================================================================
            // STARTER KITS DISABLED
            // ====================================================================
            if (!kitsEnabled && (cmd.StartsWith("/pick", StringComparison.OrdinalIgnoreCase) ||
                                cmd.StartsWith("/choose", StringComparison.OrdinalIgnoreCase) ||
                                cmd.Equals("/liststarterkits", StringComparison.OrdinalIgnoreCase)))
            {
                SendServerMessage(entityId, L.Get(lang, "cmd.kits.disabled"));
                return;
            }

            // ====================================================================
            // LIST STARTER KITS
            // ====================================================================
            if (cmd.Equals("/liststarterkits", StringComparison.OrdinalIgnoreCase))
            {
                if (StarterKitManager.Kits.Count == 0)
                {
                    SendServerMessage(entityId, L.Get(lang, "cmd.kits.none"));
                    return;
                }

                SendServerMessage(entityId, L.Get(lang, "cmd.kits.list.header"));

                foreach (var kv in StarterKitManager.Kits)
                {
                    var kit = kv.Value;
                    // Keep the kit name/description as-is (server-defined content). Only localize the wrapper.
                    SendServerMessage(entityId, L.Format(lang, "cmd.kits.list.item",
                        ("name", kit.Name),
                        ("desc", kit.Description)
                    ));
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
                    SendServerMessage(entityId, L.Get(lang, "cmd.pick.usage"));
                    return;
                }

                // Supports kit names with spaces if you ever want it later:
                // /pick "Cool Kit Name"
                // For now: join everything after /pick
                string kitName = string.Join(" ", split, 1, split.Length - 1);

                if (player.HasPickedStarterKit)
                {
                    SendServerMessage(entityId, L.Format(lang, "cmd.pick.already",
                        ("kit", player.PickedStarterKit ?? "")
                    ));
                    return;
                }

                StarterKit kit;

                if (kitName.Equals("Random", StringComparison.OrdinalIgnoreCase))
                {
                    var list = new List<StarterKit>(StarterKitManager.Kits.Values);
                    if (list.Count == 0)
                    {
                        SendServerMessage(entityId, L.Get(lang, "cmd.kits.none"));
                        return;
                    }

                    System.Random rnd = new System.Random();
                    kit = list[rnd.Next(list.Count)];

                    SendServerMessage(entityId, L.Format(lang, "cmd.pick.random_selected",
                        ("kit", kit.Name)
                    ));

                    // Bonus item for RANDOM pick (skip if invalid)
                    GiveItemToPlayer(entityId, "adminT1QuestTicket", 2, 1);
                    Thread.Sleep(StarterKitGiveDelayMs);
                }
                else
                {
                    if (!StarterKitManager.TryGetKit(kitName, out kit))
                    {
                        SendServerMessage(entityId, L.Get(lang, "cmd.pick.not_found"));
                        return;
                    }
                }

                EntityPlayer ep = world.GetEntity(entityId) as EntityPlayer;
                if (ep == null)
                    return;

                SendServerMessage(entityId, L.Format(lang, "cmd.pick.applied_header",
                    ("kit", kit.Name)
                ));

                int i = 0;
                foreach (var item in kit.Items)
                {
                    i++;

                    if (item == null || string.IsNullOrWhiteSpace(item.ItemName) || item.Count <= 0)
                    {
                        SendServerMessage(entityId, L.Format(lang, "cmd.pick.item_invalid", ("index", i)));
                        Thread.Sleep(StarterKitGiveDelayMs);
                        continue;
                    }

                    int q = item.Quality <= 0 ? 1 : item.Quality;

                    bool given = GiveItemToPlayer(entityId, item.ItemName, item.Count, q);

                    if (given)
                    {
                        SendServerMessage(entityId, L.Format(lang, "cmd.pick.item_ok",
                            ("index", i),
                            ("item", item.ItemName),
                            ("qty", item.Count),
                            ("q", q)
                        ));
                    }
                    else
                    {
                        SendServerMessage(entityId, L.Format(lang, "cmd.pick.item_skip",
                            ("index", i),
                            ("item", item.ItemName)
                        ));
                    }

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
            if (string.IsNullOrEmpty(msg))
                return;

            // Escape quotes to avoid breaking the console command
            msg = msg.Replace("\"", "\\\"");

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

        private static bool TryConsumeTeleportCooldown(int entityId, DataPlayer player, string lang)
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
                    SendServerMessage(entityId, L.Format(lang, "cmd.teleport.cooldown",
                        ("seconds", remain)
                    ));
                    return false;
                }
            }

            player.LastTeleportUtcTicks = nowTicks;
            PlayerStorage.Save();
            return true;
        }
    }
}
