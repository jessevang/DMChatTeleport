using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

namespace DMChatTeleport
{
    internal class DataModel
    {
        public class PlayerData
        {
            public string steamId;
            public int entityId;

            public float baseX;
            public float baseY;
            public float baseZ;

            public float returnX;
            public float returnY;
            public float returnZ;

            public bool hasBase;
            public bool hasReturn;

            public bool HasPickedStarterKit;
            public string PickedStarterKit;

            // Lifetime stats
            public int LifetimeKills = 0;

            // Bloodmoon stats
            public int CurrentBloodmoonKills = 0;
            public int LastBloodmoonKills = 0;

            // Level tracking
            public int HighestLevel = 1;
            public int DayReachedHighestLevel = 1;

            // Block tracking
            public int BlocksPlaced = 0;
            public int BlocksUpgraded = 0;

            public long LastTeleportUtcTicks = 0;
        }



        public static class PlayerStorage
        {
            private static readonly string SavePath = "Mods/DMChatTeleport/Data/player_data.json";

            private static Dictionary<string, PlayerData> data = new Dictionary<string, PlayerData>();

            public static void Load()
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        data = new Dictionary<string, PlayerData>();
                        return;
                    }

                    try
                    {
                        data = JsonConvert.DeserializeObject<Dictionary<string, PlayerData>>(json)
                               ?? new Dictionary<string, PlayerData>();
                    }
                    catch
                    {
                        Debug.Log("[DMChatTeleport] Failed to parse player_data.json. Resetting.");
                        data = new Dictionary<string, PlayerData>();
                    }
                }
            }

            public static void Save()
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(
                    SavePath,
                    JsonConvert.SerializeObject(data, Formatting.Indented)
                );
            }

            public static PlayerData Get(string steamId)
            {
                if (!data.ContainsKey(steamId))
                    data[steamId] = new PlayerData { steamId = steamId };

                return data[steamId];
            }

            public static IEnumerable<PlayerData> GetAll()
            {
                return data.Values;
            }
        }
    }
}
