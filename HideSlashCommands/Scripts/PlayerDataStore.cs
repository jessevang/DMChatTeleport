using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DMChatTeleport
{
    internal static class PlayerDataStore
    {
        public class PlayerData
        {
            // Persistent identifier: "EOS_..." OR "Steam_..." etc
            public string playerId;

            public int entityId;

            public float baseX, baseY, baseZ;
            public float returnX, returnY, returnZ;

            public bool hasBase;
            public bool hasReturn;

            public bool HasPickedStarterKit;
            public string PickedStarterKit;

            public int LifetimeKills = 0;
            public int CurrentBloodmoonKills = 0;
            public int LastBloodmoonKills = 0;

            public int HighestLevel = 1;
            public int DayReachedHighestLevel = 1;

            public int BlocksPlaced = 0;
            public int BlocksUpgraded = 0;

            public long LastTeleportUtcTicks = 0;

            // Reward Points
            public int RewardPoints = 0;
            public int TotalRewardPointsEarned = 0;
            public int TotalRewardPointsSpent = 0;

            // Online-time tracking for RP accrual
            public long OnlineSessionStartUtcTicks = 0;
            public long LastOnlineSeenUtcTicks = 0;
            public int AccumulatedOnlineSeconds = 0;
            public int TotalOnlineSecondsLifetime = 0;

            public Dictionary<string, int> ShopPurchaseCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        public static class PlayerStorage
        {
            private static readonly object _lock = new object();

            // Keys are "EOS_..." OR "Steam_..." etc
            private static Dictionary<string, PlayerData> data =
                new Dictionary<string, PlayerData>(StringComparer.OrdinalIgnoreCase);

            private static string SavePath =>
                GameIO.GetGameDir("Mods/DMChatTeleport/Data/player_data.json");

            private static string NormalizeId(string id)
            {
                if (string.IsNullOrWhiteSpace(id))
                    return null;

                return id.Trim();
            }

            public static void Load()
            {
                lock (_lock)
                {
                    try
                    {
                        string path = SavePath;
                        string dir = Path.GetDirectoryName(path);

                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);

                        if (!File.Exists(path))
                        {
                            data = new Dictionary<string, PlayerData>(StringComparer.OrdinalIgnoreCase);
                            File.WriteAllText(path, JsonConvert.SerializeObject(data, Formatting.Indented));
                            return;
                        }

                        string json = File.ReadAllText(path);
                        if (string.IsNullOrWhiteSpace(json))
                        {
                            data = new Dictionary<string, PlayerData>(StringComparer.OrdinalIgnoreCase);
                            return;
                        }

                        var loaded = JsonConvert.DeserializeObject<Dictionary<string, PlayerData>>(json);
                        data = loaded ?? new Dictionary<string, PlayerData>(StringComparer.OrdinalIgnoreCase);

                        // Normalize: drop null/blank keys, ensure pd.playerId matches key, ensure dicts exist
                        var cleaned = new Dictionary<string, PlayerData>(StringComparer.OrdinalIgnoreCase);

                        foreach (var kv in data)
                        {
                            string key = NormalizeId(kv.Key);
                            if (string.IsNullOrWhiteSpace(key))
                                continue;

                            var pd = kv.Value ?? new PlayerData();
                            pd.playerId = key;

                            if (pd.ShopPurchaseCounts == null)
                                pd.ShopPurchaseCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                            cleaned[key] = pd;
                        }

                        data = cleaned;
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"[DMChatTeleport] PlayerStorage.Load failed. Path='{SavePath}'. Error: {ex}");
                        data = new Dictionary<string, PlayerData>(StringComparer.OrdinalIgnoreCase);
                    }
                }
            }

            public static void Save()
            {
                lock (_lock)
                {
                    try
                    {
                        string path = SavePath;
                        string dir = Path.GetDirectoryName(path);

                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);

                        // Normalize keys & pd.playerId
                        var normalized = new Dictionary<string, PlayerData>(StringComparer.OrdinalIgnoreCase);

                        foreach (var kv in data)
                        {
                            string key = NormalizeId(kv.Key);
                            if (string.IsNullOrWhiteSpace(key))
                                continue;

                            var pd = kv.Value ?? new PlayerData();
                            pd.playerId = key;

                            if (pd.ShopPurchaseCounts == null)
                                pd.ShopPurchaseCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                            normalized[key] = pd;
                        }

                        data = normalized;

                        string json = JsonConvert.SerializeObject(data, Formatting.Indented);

                        string tmp = path + ".tmp";
                        File.WriteAllText(tmp, json);

                        if (File.Exists(path))
                        {
                            string bak = path + ".bak";
                            try
                            {
                                File.Replace(tmp, path, bak, ignoreMetadataErrors: true);
                            }
                            catch
                            {
                                File.Delete(path);
                                File.Move(tmp, path);
                            }
                        }
                        else
                        {
                            File.Move(tmp, path);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"[DMChatTeleport] PlayerStorage.Save failed. Path='{SavePath}'. Error: {ex}");
                    }
                }
            }

            public static PlayerData Get(string playerId)
            {
                lock (_lock)
                {
                    playerId = NormalizeId(playerId);
                    if (string.IsNullOrWhiteSpace(playerId))
                        throw new Exception("[DMChatTeleport] PlayerStorage.Get called with empty playerId.");

                    if (!data.TryGetValue(playerId, out var pd) || pd == null)
                    {
                        pd = new PlayerData
                        {
                            playerId = playerId,
                            ShopPurchaseCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                        };
                        data[playerId] = pd;
                    }
                    else
                    {
                        pd.playerId = playerId;
                        if (pd.ShopPurchaseCounts == null)
                            pd.ShopPurchaseCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    }

                    return pd;
                }
            }

            public static PlayerData GetOrCreate(string playerId, out bool created)
            {
                lock (_lock)
                {
                    created = false;
                    playerId = NormalizeId(playerId);
                    if (string.IsNullOrWhiteSpace(playerId))
                        throw new Exception("[DMChatTeleport] PlayerStorage.GetOrCreate called with empty playerId.");

                    if (!data.TryGetValue(playerId, out var pd) || pd == null)
                    {
                        pd = new PlayerData
                        {
                            playerId = playerId,
                            ShopPurchaseCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                        };
                        data[playerId] = pd;
                        created = true;
                    }
                    else
                    {
                        pd.playerId = playerId;
                        if (pd.ShopPurchaseCounts == null)
                            pd.ShopPurchaseCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    }

                    return pd;
                }
            }

            public static IEnumerable<PlayerData> GetAll()
            {
                lock (_lock)
                {
                    return new List<PlayerData>(data.Values);
                }
            }

            public static bool TrySpendRP(string playerId, int amount, out int newBalance)
            {
                lock (_lock)
                {
                    var pd = Get(playerId);

                    if (amount <= 0)
                    {
                        newBalance = pd.RewardPoints;
                        return true;
                    }

                    if (pd.RewardPoints < amount)
                    {
                        newBalance = pd.RewardPoints;
                        return false;
                    }

                    pd.RewardPoints -= amount;
                    pd.TotalRewardPointsSpent += amount;

                    newBalance = pd.RewardPoints;
                    return true;
                }
            }

            public static int AddRP(string playerId, int amount)
            {
                lock (_lock)
                {
                    var pd = Get(playerId);

                    if (amount <= 0)
                        return pd.RewardPoints;

                    pd.RewardPoints += amount;
                    pd.TotalRewardPointsEarned += amount;
                    return pd.RewardPoints;
                }
            }

            public static int GetRP(string playerId)
            {
                lock (_lock)
                {
                    return Get(playerId).RewardPoints;
                }
            }

            public static int GetPurchaseCount(string playerId, string itemKey)
            {
                lock (_lock)
                {
                    var pd = Get(playerId);

                    if (pd.ShopPurchaseCounts == null)
                        pd.ShopPurchaseCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    return pd.ShopPurchaseCounts.TryGetValue(itemKey, out var count) ? count : 0;
                }
            }

            public static int IncrementPurchaseCount(string playerId, string itemKey, int amount = 1)
            {
                lock (_lock)
                {
                    var pd = Get(playerId);

                    if (pd.ShopPurchaseCounts == null)
                        pd.ShopPurchaseCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    pd.ShopPurchaseCounts.TryGetValue(itemKey, out var count);
                    count += amount;
                    pd.ShopPurchaseCounts[itemKey] = count;
                    return count;
                }
            }

            public static int CountPlayers()
            {
                lock (_lock)
                {
                    return data.Count;
                }
            }

            public static void ClearAllData()
            {
                lock (_lock)
                {
                    data.Clear();
                }
            }
        }
    }
}
