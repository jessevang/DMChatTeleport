using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DMChatTeleport
{
    // -----------------------------
    // Config Models
    // -----------------------------
    public class ModConfig
    {
        public bool TurnOnTeleportCommands = true;
        public bool TurnOnStarterKits = true;
        public bool TurnOnHideCommandsWithSlashes = true;
        public int TeleportCooldownSeconds = 0;

        public RewardPointsConfig RewardPoints = new RewardPointsConfig();
        public BloodMoonRewardsConfig BloodMoonRewards = new BloodMoonRewardsConfig();
        public ShopConfig Shop = new ShopConfig();
    }

    public class RewardPointsConfig
    {
        public bool Enabled = true;
        public int MinutesPerPoint = 30;
        public int TickSeconds = 10;
        public int SaveIntervalSeconds = 60;
    }

    // -----------------------------
    // Blood Moon Rewards
    // -----------------------------
    public class BloodMoonRewardsConfig
    {
        public bool Enabled = true;

        // If true, require that a player is present to receive rank rewards
        public bool RequirePresenceForRankRewards = true;

        // If true, send private messages (sayplayer) about rewards earned
        public bool AnnounceRewardMessages = true;

        // Presence reward (per player present during blood moon)
        public PresenceRewardConfig Presence = new PresenceRewardConfig();

        // Party ranking rewards (1st/2nd party)
        public PartyRankRewardsConfig PartyRankRewards = new PartyRankRewardsConfig();

        // Individual ranking rewards (Top Kills 1st/2nd)
        public SoloRankRewardsConfig SoloRankRewards = new SoloRankRewardsConfig();

        // configurable bonus system (ONLY KillStep now)
        public BloodMoonBonusConfig Bonuses = new BloodMoonBonusConfig();
    }

    public class PresenceRewardConfig
    {
        public bool Enabled = true;
        public int RP = 1;
    }

    public class PartyRankRewardsConfig
    {
        public bool Enabled = true;
        public int FirstPlaceRP = 5;
        public int SecondPlaceRP = 3;
    }

    public class SoloRankRewardsConfig
    {
        public bool Enabled = true;
        public int FirstPlaceRP = 5;
        public int SecondPlaceRP = 3;
    }

    // -----------------------------
    // Blood Moon Bonus Configs
    // -----------------------------
    public class BloodMoonBonusConfig
    {
        // Give RP based on kills: every N kills gives X RP (per player)
        public KillStepBonusConfig KillStep = new KillStepBonusConfig();
    }

    public class KillStepBonusConfig
    {
        public bool Enabled = false;

        // Every N kills...
        public int EveryKills = 10;

        // ...gives this many RP
        public int RPPerStep = 1;

        // Optional cap per blood moon (0 = no cap)
        public int MaxRP = 0;
    }

    // -----------------------------
    // Shop
    // -----------------------------
    public class ShopConfig
    {
        public bool Enabled = true;
        public bool LogPurchases = true;

        public Dictionary<string, ShopItemConfig> Items =
            new Dictionary<string, ShopItemConfig>(StringComparer.OrdinalIgnoreCase);
    }

    public class ShopItemConfig
    {
        public bool Enabled = true;
        public int CostRP = 0;

        public bool LimitPer10Levels = false;
    }

    // -----------------------------
    // Config Manager
    // -----------------------------
    public static class ConfigManager
    {
        private static readonly object _lock = new object();

        private static string ConfigPath =>
            GameIO.GetGameDir("Mods/DMChatTeleport/config.json");

        public static ModConfig Config { get; private set; }

        public static void Load()
        {
            lock (_lock)
            {
                try
                {
                    string path = ConfigPath;
                    string dir = Path.GetDirectoryName(path);

                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    if (!File.Exists(path))
                    {
                        Config = BuildDefaultConfig();
                        Save();
                        Debug.Log("[DMChatTeleport] config.json created with defaults.");
                        return;
                    }

                    string json = File.ReadAllText(path);

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        Config = BuildDefaultConfig();
                        Save();
                        return;
                    }

                    Config = JsonConvert.DeserializeObject<ModConfig>(json) ?? BuildDefaultConfig();

                    ApplyDefaultsInPlace(Config);

                    // Always write back to ensure new fields exist in config.json
                    Save();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DMChatTeleport] ConfigManager.Load failed. Path='{ConfigPath}'. Error: {ex}");
                    Config = BuildDefaultConfig();
                    Save();
                }
            }
        }

        public static void Save()
        {
            lock (_lock)
            {
                try
                {
                    string path = ConfigPath;
                    string dir = Path.GetDirectoryName(path);

                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    string json = JsonConvert.SerializeObject(Config, Formatting.Indented);

                    string tmp = path + ".tmp";
                    File.WriteAllText(tmp, json);

                    if (File.Exists(path))
                        File.Delete(path);

                    File.Move(tmp, path);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DMChatTeleport] ConfigManager.Save failed. Path='{ConfigPath}'. Error: {ex}");
                }
            }
        }

        // -----------------------------
        // Defaults + Repair
        // -----------------------------
        private static ModConfig BuildDefaultConfig()
        {
            var cfg = new ModConfig();

            cfg.RewardPoints = new RewardPointsConfig
            {
                Enabled = true,
                MinutesPerPoint = 30,
                TickSeconds = 10,
                SaveIntervalSeconds = 60
            };

            cfg.BloodMoonRewards = new BloodMoonRewardsConfig
            {
                Enabled = true,
                RequirePresenceForRankRewards = true,
                AnnounceRewardMessages = true,

                Presence = new PresenceRewardConfig { Enabled = true, RP = 1 },

                PartyRankRewards = new PartyRankRewardsConfig
                {
                    Enabled = true,
                    FirstPlaceRP = 5,
                    SecondPlaceRP = 3
                },

                SoloRankRewards = new SoloRankRewardsConfig
                {
                    Enabled = true,
                    FirstPlaceRP = 5,
                    SecondPlaceRP = 3
                },

                Bonuses = new BloodMoonBonusConfig
                {
                    KillStep = new KillStepBonusConfig
                    {
                        Enabled = false,
                        EveryKills = 10,
                        RPPerStep = 1,
                        MaxRP = 0
                    }
                }
            };

            cfg.Shop = new ShopConfig
            {
                Enabled = true,
                LogPurchases = true,
                Items = new Dictionary<string, ShopItemConfig>(StringComparer.OrdinalIgnoreCase)
            };

            cfg.Shop.Items["consumable_x5"] = new ShopItemConfig { Enabled = true, CostRP = 3 };
            cfg.Shop.Items["modArmorTripleStoragePocket"] = new ShopItemConfig { Enabled = true, CostRP = 3 };

            cfg.Shop.Items["armor_q3_random"] = new ShopItemConfig { Enabled = true, CostRP = 3 };
            cfg.Shop.Items["reroll_item"] = new ShopItemConfig { Enabled = true, CostRP = 15 };
            cfg.Shop.Items["clone_item"] = new ShopItemConfig { Enabled = true, CostRP = 40 };
            cfg.Shop.Items["skill_token"] = new ShopItemConfig { Enabled = true, CostRP = 10, LimitPer10Levels = true };

            return cfg;
        }

        private static void ApplyDefaultsInPlace(ModConfig cfg)
        {
            if (cfg == null)
                return;

            // -----------------------------
            // Reward Points
            // -----------------------------
            if (cfg.RewardPoints == null)
                cfg.RewardPoints = new RewardPointsConfig();

            cfg.RewardPoints.MinutesPerPoint = Math.Max(1, cfg.RewardPoints.MinutesPerPoint);
            cfg.RewardPoints.TickSeconds = Math.Max(1, cfg.RewardPoints.TickSeconds);
            cfg.RewardPoints.SaveIntervalSeconds = Math.Max(5, cfg.RewardPoints.SaveIntervalSeconds);

            // -----------------------------
            // Blood Moon Rewards
            // -----------------------------
            if (cfg.BloodMoonRewards == null)
                cfg.BloodMoonRewards = new BloodMoonRewardsConfig();

            if (cfg.BloodMoonRewards.Presence == null)
                cfg.BloodMoonRewards.Presence = new PresenceRewardConfig();

            cfg.BloodMoonRewards.Presence.RP = Math.Max(0, cfg.BloodMoonRewards.Presence.RP);

            if (cfg.BloodMoonRewards.PartyRankRewards == null)
                cfg.BloodMoonRewards.PartyRankRewards = new PartyRankRewardsConfig();

            if (cfg.BloodMoonRewards.SoloRankRewards == null)
                cfg.BloodMoonRewards.SoloRankRewards = new SoloRankRewardsConfig();

            cfg.BloodMoonRewards.PartyRankRewards.FirstPlaceRP = Math.Max(0, cfg.BloodMoonRewards.PartyRankRewards.FirstPlaceRP);
            cfg.BloodMoonRewards.PartyRankRewards.SecondPlaceRP = Math.Max(0, cfg.BloodMoonRewards.PartyRankRewards.SecondPlaceRP);

            cfg.BloodMoonRewards.SoloRankRewards.FirstPlaceRP = Math.Max(0, cfg.BloodMoonRewards.SoloRankRewards.FirstPlaceRP);
            cfg.BloodMoonRewards.SoloRankRewards.SecondPlaceRP = Math.Max(0, cfg.BloodMoonRewards.SoloRankRewards.SecondPlaceRP);

            // Ensure bonuses exist + clamp
            if (cfg.BloodMoonRewards.Bonuses == null)
                cfg.BloodMoonRewards.Bonuses = new BloodMoonBonusConfig();

            if (cfg.BloodMoonRewards.Bonuses.KillStep == null)
                cfg.BloodMoonRewards.Bonuses.KillStep = new KillStepBonusConfig();

            cfg.BloodMoonRewards.Bonuses.KillStep.EveryKills = Math.Max(1, cfg.BloodMoonRewards.Bonuses.KillStep.EveryKills);
            cfg.BloodMoonRewards.Bonuses.KillStep.RPPerStep = Math.Max(0, cfg.BloodMoonRewards.Bonuses.KillStep.RPPerStep);
            cfg.BloodMoonRewards.Bonuses.KillStep.MaxRP = Math.Max(0, cfg.BloodMoonRewards.Bonuses.KillStep.MaxRP);

            // -----------------------------
            // Shop
            // -----------------------------
            if (cfg.Shop == null)
                cfg.Shop = new ShopConfig();

            if (cfg.Shop.Items == null)
                cfg.Shop.Items = new Dictionary<string, ShopItemConfig>(StringComparer.OrdinalIgnoreCase);

            // Ensure baseline keys exist (you can delete these if you want zero "forced defaults")
            EnsureShopItem(cfg, "reroll_item", 15, limitPer10Levels: false);
            EnsureShopItem(cfg, "clone_item", 40, limitPer10Levels: false);
            EnsureShopItem(cfg, "skill_token", 10, limitPer10Levels: true);

            EnsureShopItem(cfg, "consumable_x5", 3, limitPer10Levels: false);
            EnsureShopItem(cfg, "pocket_3", 3, limitPer10Levels: false);
            EnsureShopItem(cfg, "armor_q3_random", 3, limitPer10Levels: false);

            foreach (var kv in cfg.Shop.Items)
            {
                if (kv.Value == null)
                    cfg.Shop.Items[kv.Key] = new ShopItemConfig();

                if (cfg.Shop.Items[kv.Key].CostRP < 0)
                    cfg.Shop.Items[kv.Key].CostRP = 0;
            }
        }

        private static void EnsureShopItem(ModConfig cfg, string key, int defaultCost, bool limitPer10Levels)
        {
            if (!cfg.Shop.Items.TryGetValue(key, out var item) || item == null)
            {
                cfg.Shop.Items[key] = new ShopItemConfig
                {
                    Enabled = true,
                    CostRP = Math.Max(0, defaultCost),
                    LimitPer10Levels = limitPer10Levels
                };
                return;
            }

            if (item.CostRP < 0)
                item.CostRP = 0;

            if (key.Equals("skill_token", StringComparison.OrdinalIgnoreCase))
                item.LimitPer10Levels = limitPer10Levels;
        }
    }
}
