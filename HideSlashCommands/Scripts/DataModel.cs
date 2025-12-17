using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DMChatTeleport
{
    internal class DataModel
    {
        public class PlayerData
        {
            public string steamId;
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
        }

        public static class PlayerStorage
        {
            private static readonly object _lock = new object();
            private static Dictionary<string, PlayerData> data = new Dictionary<string, PlayerData>();

  
            private static string SavePath =>
                GameIO.GetGameDir("Mods/DMChatTeleport/Data/player_data.json");

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
                            data = new Dictionary<string, PlayerData>();
                            // Create initial file so permissions/path problems surface immediately
                            File.WriteAllText(path, JsonConvert.SerializeObject(data, Formatting.Indented));
                            return;
                        }

                        string json = File.ReadAllText(path);
                        if (string.IsNullOrWhiteSpace(json))
                        {
                            data = new Dictionary<string, PlayerData>();
                            return;
                        }

                        var loaded = JsonConvert.DeserializeObject<Dictionary<string, PlayerData>>(json);
                        data = loaded ?? new Dictionary<string, PlayerData>();
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"[DMChatTeleport] PlayerStorage.Load failed. Path='{SavePath}'. Error: {ex}");
                        data = new Dictionary<string, PlayerData>();
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

                        string json = JsonConvert.SerializeObject(data, Formatting.Indented);

                        // Atomic-ish write: write temp then replace
                        string tmp = path + ".tmp";
                        File.WriteAllText(tmp, json);

                        if (File.Exists(path))
                            File.Delete(path);

                        File.Move(tmp, path);
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"[DMChatTeleport] PlayerStorage.Save failed. Path='{SavePath}'. Error: {ex}");
                    }
                }
            }

            public static PlayerData Get(string steamId)
            {
                lock (_lock)
                {
                    if (string.IsNullOrWhiteSpace(steamId))
                        steamId = "UNKNOWN";

                    if (!data.TryGetValue(steamId, out var pd) || pd == null)
                    {
                        pd = new PlayerData { steamId = steamId };
                        data[steamId] = pd;
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
        }
    }
}
