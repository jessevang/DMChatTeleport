using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DMChatTeleport
{
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

    public class BloodMoonRewardsConfig
    {
        public bool Enabled = true;
        public bool RequirePresenceForRankRewards = true;
        public bool AnnounceRewardMessages = true;

        public PresenceRewardConfig Presence = new PresenceRewardConfig();
        public PartyRankRewardsConfig PartyRankRewards = new PartyRankRewardsConfig();
        public SoloRankRewardsConfig SoloRankRewards = new SoloRankRewardsConfig();
        public BloodMoonBonusConfig Bonuses = new BloodMoonBonusConfig();
    }

    public class PresenceRewardConfig
    {
        public bool Enabled = true;
        public int RP = 3;
    }

    public class PartyRankRewardsConfig
    {
        public bool Enabled = true;
        public int FirstPlaceRP = 15;
        public int SecondPlaceRP = 10;
    }

    public class SoloRankRewardsConfig
    {
        public bool Enabled = true;
        public int FirstPlaceRP = 15;
        public int SecondPlaceRP = 10;
    }

    public class BloodMoonBonusConfig
    {
        public KillStepBonusConfig KillStep = new KillStepBonusConfig();
    }

    public class KillStepBonusConfig
    {
        public bool Enabled = true;
        public int EveryKills = 10;
        public int RPPerStep = 1;
        public int MaxRP = 0;
    }

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

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int Qty = 1;

        public bool LimitPer10Levels = false;
    }

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

        private static ModConfig BuildDefaultConfig()
        {
            var cfg = new ModConfig
            {
                TurnOnTeleportCommands = true,
                TurnOnStarterKits = true,
                TurnOnHideCommandsWithSlashes = true,
                TeleportCooldownSeconds = 0,

                RewardPoints = new RewardPointsConfig
                {
                    Enabled = true,
                    MinutesPerPoint = 30,
                    TickSeconds = 10,
                    SaveIntervalSeconds = 60
                },

                BloodMoonRewards = new BloodMoonRewardsConfig
                {
                    Enabled = true,
                    RequirePresenceForRankRewards = true,
                    AnnounceRewardMessages = true,

                    Presence = new PresenceRewardConfig
                    {
                        Enabled = true,
                        RP = 3
                    },

                    PartyRankRewards = new PartyRankRewardsConfig
                    {
                        Enabled = true,
                        FirstPlaceRP = 15,
                        SecondPlaceRP = 10
                    },

                    SoloRankRewards = new SoloRankRewardsConfig
                    {
                        Enabled = true,
                        FirstPlaceRP = 15,
                        SecondPlaceRP = 10
                    },

                    Bonuses = new BloodMoonBonusConfig
                    {
                        KillStep = new KillStepBonusConfig
                        {
                            Enabled = true,
                            EveryKills = 10,
                            RPPerStep = 1,
                            MaxRP = 0
                        }
                    }
                },

                Shop = new ShopConfig
                {
                    Enabled = true,
                    LogPurchases = true,
                    Items = new Dictionary<string, ShopItemConfig>(StringComparer.OrdinalIgnoreCase)
                }
            };

            cfg.Shop.Items["ammo762mmBulletBall"] = new ShopItemConfig { Enabled = true, CostRP = 1, Qty = 100 };
            cfg.Shop.Items["ammo9mmBulletBall"] = new ShopItemConfig { Enabled = true, CostRP = 1, Qty = 100 };
            cfg.Shop.Items["ammoShotgunShell"] = new ShopItemConfig { Enabled = true, CostRP = 1, Qty = 100 };
            cfg.Shop.Items["ammo44MagnumBulletAP"] = new ShopItemConfig { Enabled = true, CostRP = 1, Qty = 100 };

            cfg.Shop.Items["armor_q3_random"] = new ShopItemConfig { Enabled = true, CostRP = 1, Qty = 1 };

            cfg.Shop.Items["skill_token"] = new ShopItemConfig { Enabled = true, CostRP = 3, Qty = 1, LimitPer10Levels = true };

            cfg.Shop.Items["giveXP_T2_admin"] = new ShopItemConfig { Enabled = true, CostRP = 20, Qty = 1 };

            cfg.Shop.Items["drugAtomJunkies"] = new ShopItemConfig { Enabled = true, CostRP = 1, Qty = 3 };
            cfg.Shop.Items["drugSkullCrushers"] = new ShopItemConfig { Enabled = true, CostRP = 1, Qty = 3 };
            cfg.Shop.Items["drugRecog"] = new ShopItemConfig { Enabled = true, CostRP = 1, Qty = 3 };
            cfg.Shop.Items["drugRockBusters"] = new ShopItemConfig { Enabled = true, CostRP = 1, Qty = 3 };
            cfg.Shop.Items["drugEyeKandy"] = new ShopItemConfig { Enabled = true, CostRP = 1, Qty = 3 };

            cfg.Shop.Items["drinkCanMegaCrush"] = new ShopItemConfig { Enabled = true, CostRP = 1, Qty = 3 };
            cfg.Shop.Items["drinkJarGrandpasLearningElixir"] = new ShopItemConfig { Enabled = true, CostRP = 1, Qty = 3 };

            return cfg;
        }

        private static void ApplyDefaultsInPlace(ModConfig cfg)
        {
            if (cfg == null)
                return;

            if (cfg.RewardPoints == null)
                cfg.RewardPoints = new RewardPointsConfig();

            cfg.RewardPoints.MinutesPerPoint = Math.Max(1, cfg.RewardPoints.MinutesPerPoint);
            cfg.RewardPoints.TickSeconds = Math.Max(1, cfg.RewardPoints.TickSeconds);
            cfg.RewardPoints.SaveIntervalSeconds = Math.Max(5, cfg.RewardPoints.SaveIntervalSeconds);

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

            if (cfg.BloodMoonRewards.Bonuses == null)
                cfg.BloodMoonRewards.Bonuses = new BloodMoonBonusConfig();

            if (cfg.BloodMoonRewards.Bonuses.KillStep == null)
                cfg.BloodMoonRewards.Bonuses.KillStep = new KillStepBonusConfig();

            cfg.BloodMoonRewards.Bonuses.KillStep.EveryKills = Math.Max(1, cfg.BloodMoonRewards.Bonuses.KillStep.EveryKills);
            cfg.BloodMoonRewards.Bonuses.KillStep.RPPerStep = Math.Max(0, cfg.BloodMoonRewards.Bonuses.KillStep.RPPerStep);
            cfg.BloodMoonRewards.Bonuses.KillStep.MaxRP = Math.Max(0, cfg.BloodMoonRewards.Bonuses.KillStep.MaxRP);

            if (cfg.Shop == null)
                cfg.Shop = new ShopConfig();

            if (cfg.Shop.Items == null)
                cfg.Shop.Items = new Dictionary<string, ShopItemConfig>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in new List<KeyValuePair<string, ShopItemConfig>>(cfg.Shop.Items))
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                {
                    cfg.Shop.Items.Remove(kv.Key);
                    continue;
                }

                if (kv.Value == null)
                    cfg.Shop.Items[kv.Key] = new ShopItemConfig();

                var item = cfg.Shop.Items[kv.Key];

                if (item.CostRP < 0) item.CostRP = 0;
                if (item.Qty <= 0) item.Qty = 1;

                if (kv.Key.Equals("skill_token", StringComparison.OrdinalIgnoreCase) && item.LimitPer10Levels == false)
                    item.LimitPer10Levels = true;
            }

            EnsureShopItem(cfg, "ammo762mmBulletBall", 1, 100, false);
            EnsureShopItem(cfg, "ammo9mmBulletBall", 1, 100, false);
            EnsureShopItem(cfg, "ammoShotgunShell", 1, 100, false);
            EnsureShopItem(cfg, "ammo44MagnumBulletAP", 1, 100, false);

            EnsureShopItem(cfg, "armor_q3_random", 1, 1, false);

            EnsureShopItem(cfg, "giveXP_T2_admin", 10, 1, false);

            EnsureShopItem(cfg, "drugAtomJunkies", 1, 3, false);
            EnsureShopItem(cfg, "drugSkullCrushers", 1, 3, false);
            EnsureShopItem(cfg, "drugRecog", 1, 3, false);
            EnsureShopItem(cfg, "drugRockBusters", 1, 3, false);
            EnsureShopItem(cfg, "drugEyeKandy", 1, 3, false);

            EnsureShopItem(cfg, "drinkCanMegaCrush", 1, 3, false);
            EnsureShopItem(cfg, "drinkJarGrandpasLearningElixir", 1, 3, false);
        }

        private static void EnsureShopItem(ModConfig cfg, string key, int defaultCost, int defaultQty, bool limitPer10Levels)
        {
            if (!cfg.Shop.Items.TryGetValue(key, out var item) || item == null)
            {
                cfg.Shop.Items[key] = new ShopItemConfig
                {
                    Enabled = true,
                    CostRP = Math.Max(0, defaultCost),
                    Qty = Math.Max(1, defaultQty),
                    LimitPer10Levels = limitPer10Levels
                };
                return;
            }

            if (item.CostRP < 0)
                item.CostRP = 0;

            if (item.Qty <= 0)
                item.Qty = 1;

            if (key.Equals("skill_token", StringComparison.OrdinalIgnoreCase))
                item.LimitPer10Levels = limitPer10Levels;
        }
    }
}
