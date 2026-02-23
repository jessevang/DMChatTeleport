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




            if (CommandHandlerAdmin.TryHandle(playerId, entityId, cmd))
                return;



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
            if (cmd.Equals("/base", StringComparison.OrdinalIgnoreCase)|| cmd.Equals("/b", StringComparison.OrdinalIgnoreCase))
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
            if (cmd.Equals("/return", StringComparison.OrdinalIgnoreCase)|| cmd.Equals("/r", StringComparison.OrdinalIgnoreCase))
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
            if (cmd.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
            {
                string rawHelp = cmd.Trim();
                string[] helpParts = rawHelp.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // /help <topic>
                if (helpParts.Length >= 2)
                {
                    string topic = helpParts[1].Trim().ToLowerInvariant();

                    // =========================
                    // Reward Points help
                    // =========================
                    if (topic == "rp" || topic == "reward" || topic == "rewards" || topic == "points" || topic == "wallet")
                    {
                        SendServerMessage(entityId, L.Get(lang, "cmd.help.topic.rp.header"));
                        SendServerMessage(entityId, L.Get(lang, "cmd.help.topic.rp.balance"));
                        SendServerMessage(entityId, L.Get(lang, "cmd.help.topic.rp.shop"));
                        SendServerMessage(entityId, L.Get(lang, "cmd.help.topic.rp.buy"));
                        return;
                    }

                    // =========================
                    // Shop help
                    // =========================
                    if (topic == "shop" || topic == "buy")
                    {
                        SendServerMessage(entityId, L.Get(lang, "cmd.help.topic.shop.header"));
                        SendServerMessage(entityId, L.Get(lang, "cmd.help.topic.shop.list"));
                        SendServerMessage(entityId, L.Get(lang, "cmd.help.topic.shop.buy"));
                        SendServerMessage(entityId, L.Get(lang, "cmd.help.topic.shop.rp"));
                        return;
                    }

                    // =========================
                    // Teleport help
                    // =========================
                    if (topic == "setbase" || topic == "base" || topic == "return" || topic == "teleport" || topic == "tp")
                    {
                        if (!teleportsEnabled)
                        {
                            SendServerMessage(entityId, L.Get(lang, "cmd.teleport.disabled"));
                            return;
                        }

                        // Show only the relevant teleport command(s)
                        if (topic == "setbase" || topic == "teleport" || topic == "tp")
                            SendServerMessage(entityId, L.Get(lang, "cmd.help.setbase"));

                        if (topic == "base" || topic == "teleport" || topic == "tp")
                            SendServerMessage(entityId, L.Get(lang, "cmd.help.base"));

                        if (topic == "return" || topic == "teleport" || topic == "tp")
                            SendServerMessage(entityId, L.Get(lang, "cmd.help.return"));

                        return;
                    }

                    // =========================
                    // Starter kits help
                    // =========================
                    if (topic == "starterkits" || topic == "kits" || topic == "liststarterkits" || topic == "pick")
                    {
                        if (!kitsEnabled)
                        {
                            SendServerMessage(entityId, L.Get(lang, "cmd.kits.disabled"));
                            return;
                        }

                        // Show only the relevant kit command(s)
                        if (topic == "starterkits" || topic == "kits" || topic == "liststarterkits")
                            SendServerMessage(entityId, L.Get(lang, "cmd.help.liststarterkits"));

                        if (topic == "starterkits" || topic == "kits" || topic == "pick")
                        {
                            SendServerMessage(entityId, L.Get(lang, "cmd.help.pick"));
                            SendServerMessage(entityId, L.Get(lang, "cmd.help.pick_random"));
                        }

                        return;
                    }

                    // =========================
                    // Language help
                    // =========================
                    if (topic == "lang" || topic == "language")
                    {
                        // keep it minimal + useful
                        SendServerMessage(entityId, L.Get(lang, "lang.usage"));
                        SendServerMessage(entityId, L.Get(lang, "lang.available"));
                        return;
                    }

                    // =========================
                    // Blood moon help
                    // =========================
                    if (topic == "isbloodmoon" || topic == "bloodmoon" || topic == "bm")
                    {
                        SendServerMessage(entityId, L.Get(lang, "cmd.help.isbloodmoon"));
                        return;
                    }

                    // =========================
                    // Admin help (admins only)
                    // =========================
                    if (topic == "admin")
                    {
                        if (IsAdmin(entityId))
                        {
                            SendServerMessage(entityId, L.Get(lang, "admin.help.header"));
                            SendServerMessage(entityId, L.Get(lang, "admin.help.reloadconfig"));
                            SendServerMessage(entityId, L.Get(lang, "admin.help.addrp"));
                            SendServerMessage(entityId, L.Get(lang, "admin.help.setrp"));
                            SendServerMessage(entityId, L.Get(lang, "admin.help.rpof"));
                            SendServerMessage(entityId, L.Get(lang, "admin.help.players"));
                            SendServerMessage(entityId, L.Get(lang, "admin.help.tip"));
                        }
                        else
                        {
                            SendServerMessage(entityId, L.Get(lang, "admin.help.denied"));
                        }
                        return;
                    }

                    // IMPORTANT CHANGE:
                    // Unknown topic => list nothing (no fallback to general help)
                    return;
                }

                // /help (general)
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

                // Reward Points + Shop (always listed so all players know about them)
                SendServerMessage(entityId, L.Get(lang, "cmd.help.rp"));
                SendServerMessage(entityId, L.Get(lang, "cmd.help.shop"));
                SendServerMessage(entityId, L.Get(lang, "cmd.help.buy"));

                SendServerMessage(entityId, L.Get(lang, "cmd.help.lang"));
                SendServerMessage(entityId, L.Get(lang, "cmd.help.isbloodmoon"));

                // Admin-only commands (hidden from non-admins)
                if (IsAdmin(entityId))
                {
                    SendServerMessage(entityId, L.Get(lang, "admin.help.header"));
                    SendServerMessage(entityId, L.Get(lang, "admin.help.reloadconfig"));
                    SendServerMessage(entityId, L.Get(lang, "admin.help.addrp"));
                    SendServerMessage(entityId, L.Get(lang, "admin.help.setrp"));
                    SendServerMessage(entityId, L.Get(lang, "admin.help.rpof"));
                    SendServerMessage(entityId, L.Get(lang, "admin.help.players"));
                    SendServerMessage(entityId, L.Get(lang, "admin.help.tip"));
                }

                return;
            }

            // ====================================================================
            // STARTER KITS DISABLED
            // ====================================================================
            if (!kitsEnabled && (cmd.StartsWith("/pick", StringComparison.OrdinalIgnoreCase) ||
                                cmd.StartsWith("/choose", StringComparison.OrdinalIgnoreCase) ||
                                cmd.Equals("/liststarterkits", StringComparison.OrdinalIgnoreCase) ||
                                cmd.Equals("/starterkits", StringComparison.OrdinalIgnoreCase))
                                )
            {
                SendServerMessage(entityId, L.Get(lang, "cmd.kits.disabled"));
                return;
            }

            // ====================================================================
            // LIST STARTER KITS (BATCHED - NO DELAYS)
            // ====================================================================
            if (cmd.Equals("/liststarterkits", StringComparison.OrdinalIgnoreCase) ||
                cmd.Equals("/starterkits", StringComparison.OrdinalIgnoreCase))
            {
                var kits = StarterKitManager.GetKitsNumbered();
                if (kits.Count == 0)
                {
                    SendServerMessage(entityId, L.Get(lang, "cmd.kits.none"));
                    return;
                }

                SendServerMessage(entityId, L.Get(lang, "cmd.kits.list.header"));

                const int chunkMax = 180; // keep well under your 240 cap
                string chunk = "";

                for (int idx = 0; idx < kits.Count; idx++)
                {
                    var kit = kits[idx];
                    int number = idx + 1;

                    string line = L.Format(lang, "cmd.kits.list.item_numbered",
                        ("num", number),
                        ("name", kit?.Name ?? ""),
                        ("desc", kit?.Description ?? "")
                    );

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        Debug.LogWarning($"[DMChatTeleport] Blank kit line at #{number}. Name='{kit?.Name}'");
                        continue;
                    }

                    // Put multiple entries in one message
                    string add = (chunk.Length == 0) ? line : ("  |  " + line);

                    if (chunk.Length + add.Length > chunkMax)
                    {
                        SendServerMessage(entityId, chunk);
                        chunk = line;
                    }
                    else
                    {
                        chunk += add;
                    }
                }

                if (!string.IsNullOrWhiteSpace(chunk))
                    SendServerMessage(entityId, chunk);

                SendServerMessage(entityId, L.Get(lang, "cmd.kits.pick_hint"));
                return;
            }

            // ====================================================================
            // PICK STARTER KIT (Choose number intead of name now)
            // ====================================================================
            if (cmd.StartsWith("/pick", StringComparison.OrdinalIgnoreCase) ||
                cmd.StartsWith("/choose", StringComparison.OrdinalIgnoreCase))
            {
                string[] split = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length < 2)
                {
                    SendServerMessage(entityId, L.Get(lang, "cmd.pick.usage_numbered"));
                    return;
                }

                string arg = string.Join(" ", split, 1, split.Length - 1).Trim();

                if (player.HasPickedStarterKit)
                {
                    SendServerMessage(entityId, L.Format(lang, "cmd.pick.already",
                        ("kit", player.PickedStarterKit ?? "")
                    ));
                    return;
                }

                var list = StarterKitManager.GetKitsNumbered();
                if (list.Count == 0)
                {
                    SendServerMessage(entityId, L.Get(lang, "cmd.kits.none"));
                    return;
                }

                StarterKit kit = null;

                // RANDOM
                if (arg.Equals("Random", StringComparison.OrdinalIgnoreCase))
                {
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
                    // NUMBERED PICK
                    if (!int.TryParse(arg, out int pickNumber))
                    {
                        SendServerMessage(entityId, L.Get(lang, "cmd.pick.invalid_number"));
                        SendServerMessage(entityId, L.Get(lang, "cmd.pick.usage_numbered"));
                        return;
                    }

                    if (pickNumber < 1 || pickNumber > list.Count)
                    {
                        SendServerMessage(entityId, L.Format(lang, "cmd.pick.number_out_of_range",
                            ("min", 1),
                            ("max", list.Count)
                        ));
                        return;
                    }

                    kit = list[pickNumber - 1];
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
                player.PickedStarterKit = kit.Name; // continues storing name
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

        /*
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
        */


        private static void SendServerMessage(int entityId, string msg)
        {
            if (string.IsNullOrEmpty(msg))
                return;

            // 1) Normalize to ONE LINE (console commands hate newlines)
            msg = msg.Replace("\r", " ").Replace("\n", " ");

            // 2) Strip other control chars (tabs, etc.)
            // Keep normal text + your color tags.
            var chars = msg.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (char.IsControl(chars[i]))
                    chars[i] = ' ';
            }
            msg = new string(chars);

            // 3) Escape backslashes first, then quotes
            msg = msg.Replace("\\", "\\\\");
            msg = msg.Replace("\"", "\\\"");

            // 4) Optional: cap length to avoid silent failures
            const int maxLen = 240;
            if (msg.Length > maxLen)
                msg = msg.Substring(0, maxLen);

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

        private static bool IsAdmin(int entityId)
        {
            try
            {
                var ci = ConnectionManager.Instance?.Clients?.ForEntityId(entityId);
                if (ci == null) return false;

                int perm = GameManager.Instance.adminTools.Users.GetUserPermissionLevel(ci);
                return perm == 0;
            }
            catch
            {
                return false;
            }
        }
        private static string OneLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            s = s.Replace("\r", " ").Replace("\n", " ");
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (char.IsControl(chars[i])) chars[i] = ' ';
            return new string(chars).Trim();
        }
    }
}
