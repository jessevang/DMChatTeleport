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

        private static void HandleWallet(string playerId, int entityId)
        {
            var cfg = ConfigManager.Config;

            if (cfg?.RewardPoints == null || !cfg.RewardPoints.Enabled)
            {
                SayPlayer(entityId, "Reward Points are disabled on this server.");
                return;
            }

            int rp = PlayerStorage.GetRP(playerId);
            SayPlayer(entityId, $"Reward Points: {rp} RP");
        }

        private static void HandleShopList(string playerId, int entityId)
        {
            var cfg = ConfigManager.Config;

            if (cfg?.RewardPoints == null || !cfg.RewardPoints.Enabled)
            {
                SayPlayer(entityId, "Reward Points are disabled on this server.");
                return;
            }

            if (cfg?.Shop == null || !cfg.Shop.Enabled)
            {
                SayPlayer(entityId, "Shop is disabled on this server.");
                return;
            }

            var list = BuildEnabledShopList(cfg);
            if (list.Count == 0)
            {
                SayPlayer(entityId, "Shop has no enabled items.");
                return;
            }

            int rp = PlayerStorage.GetRP(playerId);

            SayPlayer(entityId, $"Wallet: {rp} RP");
            SayPlayer(entityId, "Shop Items:");

            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                int idx = i + 1;
                string special = SpecialKeys.Contains(entry.key) ? " (special)" : "";
                SayPlayer(entityId, $"{idx}. {entry.key} - {entry.cost} RP{special}");
            }

            SayPlayer(entityId, "Use: /buy <ShopItem#> <Amount>");
        }

        private static void HandleBuy(string playerId, int entityId, string cmd)
        {
            var cfg = ConfigManager.Config;

            if (cfg?.RewardPoints == null || !cfg.RewardPoints.Enabled)
            {
                SayPlayer(entityId, "Reward Points are disabled on this server.");
                return;
            }

            if (cfg?.Shop == null || !cfg.Shop.Enabled)
            {
                SayPlayer(entityId, "Shop is disabled on this server.");
                return;
            }

            var parts = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                SayPlayer(entityId, "Usage: /buy <ShopItem#> <Amount>");
                return;
            }

            if (!int.TryParse(parts[1], out int itemNumber) || itemNumber <= 0)
            {
                SayPlayer(entityId, "Invalid item number. Use /shop to see item numbers.");
                return;
            }

            int amount = 1;
            if (parts.Length >= 3)
            {
                if (!int.TryParse(parts[2], out amount) || amount <= 0)
                {
                    SayPlayer(entityId, "Invalid amount. Example: /buy 2 3");
                    return;
                }
            }

            // Optional: prevent absurd spam buys
            amount = Math.Min(amount, 5000);

            var list = BuildEnabledShopList(cfg);
            if (list.Count == 0)
            {
                SayPlayer(entityId, "Shop has no enabled items.");
                return;
            }

            if (itemNumber > list.Count)
            {
                SayPlayer(entityId, $"That item number is out of range. Use /shop (1-{list.Count}).");
                return;
            }

            var entry = list[itemNumber - 1];
            string key = entry.key;
            int costEach = entry.cost;

            if (costEach < 0) costEach = 0;

            // Total cost (checked for overflow)
            long totalCostLong = (long)costEach * (long)amount;
            if (totalCostLong > int.MaxValue)
            {
                SayPlayer(entityId, "That purchase is too large.");
                return;
            }
            int totalCost = (int)totalCostLong;

            // Enforce optional per-10-level limit (for skill_token, or any item you set LimitPer10Levels on)
            if (cfg.Shop.Items.TryGetValue(key, out var itemCfg) && itemCfg != null && itemCfg.LimitPer10Levels)
            {
                int playerLevel = TryGetPlayerLevel(entityId, playerId);
                int allowedLifetime = Math.Max(1, (playerLevel / 10) + 1); // 1-9 => 1, 10-19 => 2, etc.

                int alreadyBought = PlayerStorage.GetPurchaseCount(playerId, key);
                if (alreadyBought + amount > allowedLifetime)
                {
                    SayPlayer(entityId, $"Limit reached for {key}. Allowed: {allowedLifetime} total at your level (already bought {alreadyBought}).");
                    return;
                }
            }

            // Spend RP
            if (!PlayerStorage.TrySpendRP(playerId, totalCost, out int newBalance))
            {
                int cur = PlayerStorage.GetRP(playerId);
                SayPlayer(entityId, $"Not enough RP. Cost: {totalCost}, You have: {cur}.");
                return;
            }

            bool success = GrantShopItem(playerId, entityId, key, amount);

            if (!success)
            {
                // Refund if the grant failed
                PlayerStorage.AddRP(playerId, totalCost);
                SayPlayer(entityId, "Purchase failed (item/action could not be granted). RP refunded.");
                return;
            }

            // Record purchase count (for limits + stats)
            PlayerStorage.IncrementPurchaseCount(playerId, key, amount);

            if (cfg.Shop.LogPurchases)
                Debug.Log($"[DMChatTeleport] SHOP: {playerId} bought {key} x{amount} for {totalCost} RP. NewBalance={newBalance}");

            SayPlayer(entityId, $"Purchased: {key} x{amount} (-{totalCost} RP). Wallet: {newBalance} RP");
            PlayerStorage.Save();
        }
        
  
        private static List<(string key, int cost)> BuildEnabledShopList(ModConfig cfg)
        {
            var result = new List<(string key, int cost)>();

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
                result.Add((key.Trim(), cost));
            }

            // Stable ordering by key (so item numbers don’t jump around randomly)
            result.Sort((a, b) => string.Compare(a.key, b.key, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static bool GrantShopItem(string playerId, int entityId, string key, int amount)
        {
            if (string.IsNullOrWhiteSpace(key) || amount <= 0)
                return false;

            // Special actions
            if (key.Equals("skill_token", StringComparison.OrdinalIgnoreCase))
                return HandleSkillToken(entityId, amount);

            if (key.Equals("clone_item", StringComparison.OrdinalIgnoreCase))
                return HandleCloneItem(entityId, amount);

            if (key.Equals("reroll_item", StringComparison.OrdinalIgnoreCase))
                return HandleRerollItem(entityId, amount);

            if (key.Equals("armor_q3_random", StringComparison.OrdinalIgnoreCase))
                return HandleRandomArmorQ3(entityId, amount);

            // Default: treat as a real item id
            return CommandHandler.GiveItemToPlayer(entityId, key, amount, quality: 1);
        }

        // ----- Special handlers  -----

        private static bool HandleRandomArmorQ3(int entityId, int amount)
        {
            if (entityId <= 0 || amount <= 0)
                return false;

            if (RandomArmorQ3Pool == null || RandomArmorQ3Pool.Length == 0)
            {
                SayPlayer(entityId, "armor_q3_random pool is empty.");
                return false;
            }

            // Give N random pieces, each quality 3
            for (int i = 0; i < amount; i++)
            {
                string armorId = RandomArmorQ3Pool[_rng.Next(RandomArmorQ3Pool.Length)];
                bool ok = CommandHandler.GiveItemToPlayer(entityId, armorId, 1, quality: 3);

                if (!ok)
                {
                    // If any fail, stop and signal failure so caller refunds RP
                    Debug.LogWarning($"[DMChatTeleport] armor_q3_random failed to give '{armorId}' (q3) to entityId={entityId} at i={i + 1}/{amount}");
                    return false;
                }
            }

            return true;
        }

        private static bool HandleSkillToken(int entityId, int amount)
        {
            if (entityId <= 0 || amount <= 0)
                return false;

            try
            {
                var world = GameManager.Instance?.World;
                var ep = world?.GetEntity(entityId) as EntityPlayer;
                if (ep == null)
                {
                    SayPlayer(entityId, "Could not find your player entity.");
                    return false;
                }

                if (ep.Progression == null)
                {
                    SayPlayer(entityId, "Your progression data is not available right now.");
                    return false;
                }

                // This matches RewardSkillPoints.GiveReward exactly.
                ep.Progression.SkillPoints += amount;

                SayPlayer(entityId, $"Granted {amount} skill point(s).");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DMChatTeleport] HandleSkillToken failed: {ex}");
                SayPlayer(entityId, "Failed to grant skill points (exception).");
                return false;
            }
        }


        private static bool HandleCloneItem(int entityId, int amount)
        {
            SayPlayer(entityId, $"clone_item not implemented yet (requested x{amount}).");
            return false;
        }

        private static bool HandleRerollItem(int entityId, int amount)
        {
            SayPlayer(entityId, $"reroll_item not implemented yet (requested x{amount}).");
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

            SdtdConsole.Instance.ExecuteSync($"sayplayer {entityId} \"{msg}\"", null);
        }


    }
}
