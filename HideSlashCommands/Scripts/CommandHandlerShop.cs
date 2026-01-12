using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DataPlayer = DMChatTeleport.PlayerDataStore.PlayerData;
using PlayerStorage = DMChatTeleport.PlayerDataStore.PlayerStorage;

namespace DMChatTeleport
{
    public static class CommandHandlerShop
    {
        // Only these keys are "special actions" (NOT real item ids).
        // Everything else is treated as a normal item id and granted via GiveItemToPlayer().
        private static readonly HashSet<string> SpecialKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "skill_token",
                "clone_item",
                "reroll_item",
                "armor_q3_random",
            };

        // Random armor pool for armor_q3_random (Quality 3)
        private static readonly string[] RandomArmorQ3Pool =
        {
            "armorLumberjackHelmet",
            "armorLumberjackOutfit",
            "armorLumberjackGloves",
            "armorLumberjackBoots",
            "armorPreacherHelmet",
            "armorPreacherOutfit",
            "armorPreacherGloves",
            "armorPreacherBoots",
            "armorRogueHelmet",
            "armorRogueOutfit",
            "armorRogueGloves",
            "armorRogueBoots",
            "armorAthleticHelmet",
            "armorAthleticOutfit",
            "armorAthleticGloves",
            "armorAthleticBoots",
            "armorEnforcerHelmet",
            "armorEnforcerOutfit",
            "armorEnforcerGloves",
            "armorEnforcerBoots",
            "armorMediumMaster",
            "armorFarmerHelmet",
            "armorFarmerOutfit",
            "armorFarmerGloves",
            "armorFarmerBoots",
            "armorBikerHelmet",
            "armorBikerOutfit",
            "armorBikerGloves",
            "armorBikerBoots",
            "armorScavengerHelmet",
            "armorScavengerOutfit",
            "armorScavengerGloves",
            "armorScavengerBoots",
            "armorRangerHelmet",
            "armorRangerOutfit",
            "armorRangerGloves",
            "armorRangerBoots",
            "armorCommandoHelmet",
            "armorCommandoOutfit",
            "armorCommandoGloves",
            "armorCommandoBoots",
            "armorAssassinHelmet",
            "armorAssassinOutfit",
            "armorAssassinGloves",
            "armorAssassinBoots",
            "armorHeavyMaster",
            "armorMinerHelmet",
            "armorMinerOutfit",
            "armorMinerGloves",
            "armorMinerBoots",
            "armorNomadHelmet",
            "armorNomadOutfit",
            "armorNomadGloves",
            "armorNomadBoots",
            "armorNerdHelmet",
            "armorNerdOutfit",
            "armorNerdGloves",
            "armorNerdBoots",
            "armorRaiderHelmet",
            "armorRaiderOutfit",
            "armorRaiderGloves",
            "armorRaiderBoots",
        };

        private static readonly System.Random _rng = new System.Random();

        /// <summary>
        /// Returns true if the command was a shop/RP command (handled here).
        /// Call this near the top of CommandHandler.ProcessCommand().
        /// </summary>
        public static bool TryHandle(string playerId, int entityId, string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd))
                return false;

            var cfg = ConfigManager.Config;
            if (cfg == null)
                return false;

            // /rp or /wallet
            if (cmd.Equals("/rp", StringComparison.OrdinalIgnoreCase) ||
                cmd.Equals("/wallet", StringComparison.OrdinalIgnoreCase))
            {
                HandleWallet(playerId, entityId);
                return true;
            }

            // /shop
            if (cmd.Equals("/shop", StringComparison.OrdinalIgnoreCase))
            {
                HandleShopList(playerId, entityId);
                return true;
            }

            // /buy <ShopItem#> [amount]
            if (cmd.StartsWith("/buy", StringComparison.OrdinalIgnoreCase))
            {
                HandleBuy(playerId, entityId, cmd);
                return true;
            }

            return false;
        }

        private static string GetLang(string playerId)
        {
            // PlayerStorage.GetLanguage should internally handle null/blank playerId,
            // but keep it defensive.
            try { return PlayerStorage.GetLanguage(playerId, "en"); }
            catch { return "en"; }
        }

        private static void HandleWallet(string playerId, int entityId)
        {
            var cfg = ConfigManager.Config;
            string lang = GetLang(playerId);

            if (cfg?.RewardPoints == null || !cfg.RewardPoints.Enabled)
            {
                SayPlayer(entityId, L.Get(lang, "shop.rp.disabled"));
                return;
            }

            int rp = PlayerStorage.GetRP(playerId);
            SayPlayer(entityId, L.Format(lang, "shop.wallet.line", ("rp", rp)));
        }

        private static void HandleShopList(string playerId, int entityId)
        {
            var cfg = ConfigManager.Config;
            string lang = GetLang(playerId);

            if (cfg?.RewardPoints == null || !cfg.RewardPoints.Enabled)
            {
                SayPlayer(entityId, L.Get(lang, "shop.rp.disabled"));
                return;
            }

            if (cfg?.Shop == null || !cfg.Shop.Enabled)
            {
                SayPlayer(entityId, L.Get(lang, "shop.disabled"));
                return;
            }

            var list = BuildEnabledShopList(cfg);
            if (list.Count == 0)
            {
                SayPlayer(entityId, L.Get(lang, "shop.empty"));
                return;
            }

            int rp = PlayerStorage.GetRP(playerId);

            SayPlayer(entityId, L.Format(lang, "shop.wallet.line", ("rp", rp)));
            SayPlayer(entityId, L.Get(lang, "shop.items.header"));

            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                int idx = i + 1;

                bool isSpecial = SpecialKeys.Contains(entry.key);
                string specialSuffix = isSpecial ? L.Get(lang, "shop.item.special_suffix") : "";

                int qty = Math.Max(1, entry.qty);

                SayPlayer(entityId, L.Format(lang, "shop.item.line",
                    ("index", idx),
                    ("key", entry.key),
                    ("cost", entry.cost),
                    ("qty", qty),
                    ("special", specialSuffix)
                ));
            }

            SayPlayer(entityId, L.Get(lang, "shop.buy.usage_short"));
        }

        private static void HandleBuy(string playerId, int entityId, string cmd)
        {
            var cfg = ConfigManager.Config;
            string lang = GetLang(playerId);

            if (cfg?.RewardPoints == null || !cfg.RewardPoints.Enabled)
            {
                SayPlayer(entityId, L.Get(lang, "shop.rp.disabled"));
                return;
            }

            if (cfg?.Shop == null || !cfg.Shop.Enabled)
            {
                SayPlayer(entityId, L.Get(lang, "shop.disabled"));
                return;
            }

            var parts = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                SayPlayer(entityId, L.Get(lang, "shop.buy.usage"));
                return;
            }

            if (!int.TryParse(parts[1], out int itemNumber) || itemNumber <= 0)
            {
                SayPlayer(entityId, L.Get(lang, "shop.buy.invalid_item_number"));
                return;
            }

            int purchases = 1;
            if (parts.Length >= 3)
            {
                if (!int.TryParse(parts[2], out purchases) || purchases <= 0)
                {
                    SayPlayer(entityId, L.Get(lang, "shop.buy.invalid_purchases"));
                    return;
                }
            }

            purchases = Math.Min(purchases, 5000);

            var list = BuildEnabledShopList(cfg);
            if (list.Count == 0)
            {
                SayPlayer(entityId, L.Get(lang, "shop.empty"));
                return;
            }

            if (itemNumber > list.Count)
            {
                SayPlayer(entityId, L.Format(lang, "shop.buy.out_of_range",
                    ("min", 1),
                    ("max", list.Count)
                ));
                return;
            }

            var entry = list[itemNumber - 1];
            string key = entry.key;
            int costEach = Math.Max(0, entry.cost);
            int qtyPerPurchase = Math.Max(1, entry.qty);

            long totalCostLong = (long)costEach * (long)purchases;
            if (totalCostLong > int.MaxValue)
            {
                SayPlayer(entityId, L.Get(lang, "shop.buy.too_large_cost"));
                return;
            }
            int totalCost = (int)totalCostLong;

            long totalUnitsLong = (long)purchases * (long)qtyPerPurchase;
            if (totalUnitsLong > int.MaxValue)
            {
                SayPlayer(entityId, L.Get(lang, "shop.buy.too_large_qty"));
                return;
            }
            int totalUnits = (int)totalUnitsLong;

            if (cfg.Shop.Items.TryGetValue(key, out var itemCfg) && itemCfg != null && itemCfg.LimitPer10Levels)
            {
                int playerLevel = TryGetPlayerLevel(entityId, playerId);
                int allowedLifetime = Math.Max(1, (playerLevel / 10) + 1);

                int alreadyBought = PlayerStorage.GetPurchaseCount(playerId, key);
                if (alreadyBought + purchases > allowedLifetime)
                {
                    SayPlayer(entityId, L.Format(lang, "shop.buy.limit_reached",
                        ("key", key),
                        ("allowed", allowedLifetime),
                        ("already", alreadyBought)
                    ));
                    return;
                }
            }

            if (!PlayerStorage.TrySpendRP(playerId, totalCost, out int newBalance))
            {
                int cur = PlayerStorage.GetRP(playerId);
                SayPlayer(entityId, L.Format(lang, "shop.buy.not_enough_rp",
                    ("cost", totalCost),
                    ("have", cur)
                ));
                return;
            }

            bool success = GrantShopItem(playerId, entityId, key, totalUnits, lang);

            if (!success)
            {
                PlayerStorage.AddRP(playerId, totalCost);
                SayPlayer(entityId, L.Get(lang, "shop.buy.failed_refunded"));
                return;
            }

            PlayerStorage.IncrementPurchaseCount(playerId, key, purchases);

            if (cfg.Shop.LogPurchases)
                Debug.Log($"[DMChatTeleport] SHOP: {playerId} bought {key} x{totalUnits} ({purchases} purchases @ qty {qtyPerPurchase}) for {totalCost} RP. NewBalance={newBalance}");

            SayPlayer(entityId, L.Format(lang, "shop.buy.success",
                ("key", key),
                ("units", totalUnits),
                ("cost", totalCost),
                ("wallet", newBalance)
            ));

            PlayerStorage.Save();
        }

        private static List<(string key, int cost, int qty)> BuildEnabledShopList(ModConfig cfg)
        {
            var result = new List<(string key, int cost, int qty)>();

            if (cfg?.Shop?.Items == null)
                return result;

            foreach (var kv in cfg.Shop.Items)
            {
                string key = kv.Key;
                var itemCfg = kv.Value;

                if (string.IsNullOrWhiteSpace(key) || itemCfg == null)
                    continue;

                if (!itemCfg.Enabled)
                    continue;

                int cost = Math.Max(0, itemCfg.CostRP);
                int qty = Math.Max(1, itemCfg.Qty);

                result.Add((key.Trim(), cost, qty));
            }

            return result;
        }

        private static bool GrantShopItem(string playerId, int entityId, string key, int amount, string lang)
        {
            if (string.IsNullOrWhiteSpace(key) || amount <= 0)
                return false;


            if (key.Equals("armor_q3_random", StringComparison.OrdinalIgnoreCase))
                return HandleRandomArmorQ3(entityId, amount, lang);

            // Default: treat as a real item id
            return CommandHandler.GiveItemToPlayer(entityId, key, amount, quality: 1);
        }

        // ----- Special handlers  -----

        private static bool HandleRandomArmorQ3(int entityId, int amount, string lang)
        {
            if (entityId <= 0 || amount <= 0)
                return false;

            if (RandomArmorQ3Pool == null || RandomArmorQ3Pool.Length == 0)
            {
                SayPlayer(entityId, L.Get(lang, "shop.special.armor_q3_random.empty_pool"));
                return false;
            }

            // Give N random pieces, each quality 3
            for (int i = 0; i < amount; i++)
            {
                string armorId = RandomArmorQ3Pool[_rng.Next(RandomArmorQ3Pool.Length)];
                bool ok = CommandHandler.GiveItemToPlayer(entityId, armorId, 1, quality: 3);

                if (!ok)
                {
                    Debug.LogWarning($"[DMChatTeleport] armor_q3_random failed to give '{armorId}' (q3) to entityId={entityId} at i={i + 1}/{amount}");
                    return false;
                }
            }

            return true;
        }

        private static bool HandleSkillToken(int entityId, int amount, string lang)
        {
            if (entityId <= 0 || amount <= 0)
                return false;

            try
            {
                var world = GameManager.Instance?.World;
                var ep = world?.GetEntity(entityId) as EntityPlayer;
                if (ep == null)
                {
                    SayPlayer(entityId, L.Get(lang, "shop.special.skill_token.no_entity"));
                    return false;
                }

                if (ep.Progression == null)
                {
                    SayPlayer(entityId, L.Get(lang, "shop.special.skill_token.no_progression"));
                    return false;
                }

                // This matches RewardSkillPoints.GiveReward exactly.
                ep.Progression.SkillPoints += amount;

                SayPlayer(entityId, L.Format(lang, "shop.special.skill_token.granted", ("amount", amount)));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DMChatTeleport] HandleSkillToken failed: {ex}");
                SayPlayer(entityId, L.Get(lang, "shop.special.skill_token.failed_exception"));
                return false;
            }
        }

        private static bool HandleCloneItem(int entityId, int amount, string lang)
        {
            SayPlayer(entityId, L.Format(lang, "shop.special.clone_item.not_implemented", ("amount", amount)));
            return false;
        }

        private static bool HandleRerollItem(int entityId, int amount, string lang)
        {
            SayPlayer(entityId, L.Format(lang, "shop.special.reroll_item.not_implemented", ("amount", amount)));
            return false;
        }

        private static int TryGetPlayerLevel(int entityId, string playerId)
        {
            // Best-effort:
            // 1) Live entity if available
            // 2) fallback to stored HighestLevel
            try
            {
                var world = GameManager.Instance?.World;
                var ep = world?.GetEntity(entityId) as EntityPlayer;
                if (ep != null && ep.Progression != null)
                {
                    // Common in 7DTD: ep.Progression.Level
                    return Math.Max(1, ep.Progression.Level);
                }
            }
            catch { }

            try
            {
                var pd = PlayerStorage.Get(playerId);
                return Math.Max(1, pd?.HighestLevel ?? 1);
            }
            catch { }

            return 1;
        }

        private static void SayPlayer(int entityId, string msg)
        {
            if (entityId <= 0 || string.IsNullOrWhiteSpace(msg))
                return;

            // Escape quotes to avoid breaking the console command
            msg = msg.Replace("\"", "\\\"");

            SdtdConsole.Instance.ExecuteSync($"sayplayer {entityId} \"{msg}\"", null);
        }
    }
}
