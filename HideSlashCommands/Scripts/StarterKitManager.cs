using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DMChatTeleport
{
    internal static class StarterKitManager
    {
        private static readonly object _lock = new object();

        public static Dictionary<string, StarterKit> Kits { get; private set; }
            = new Dictionary<string, StarterKit>(StringComparer.OrdinalIgnoreCase);

        // Absolute, Linux-safe path (case-sensitive on Linux!)
        private static string SavePath =>
            GameIO.GetGameDir("Mods/DMChatTeleport/Data/StarterKitConfig.json");

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

                    // Helpful for Linux path/casing debugging
                    Debug.Log($"[DMChatTeleport] Starter kits path: '{path}'");

                    if (!File.Exists(path))
                    {
                        // Create a default wrapper file in the RIGHT format
                        var empty = new StarterKitConfig();
                        File.WriteAllText(path, JsonConvert.SerializeObject(empty, Formatting.Indented));

                        Kits = new Dictionary<string, StarterKit>(StringComparer.OrdinalIgnoreCase);
                        Debug.Log("[DMChatTeleport] StarterKitConfig.json not found; created default (empty).");
                        return;
                    }

                    string json = File.ReadAllText(path);

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        Kits = new Dictionary<string, StarterKit>(StringComparer.OrdinalIgnoreCase);
                        Debug.Log("[DMChatTeleport] StarterKitConfig.json was empty; 0 kits loaded.");
                        return;
                    }

                    // ✅ Correct root type for your JSON: { "StarterKits": [ ... ] }
                    var config = JsonConvert.DeserializeObject<StarterKitConfig>(json) ?? new StarterKitConfig();

                    var dict = new Dictionary<string, StarterKit>(StringComparer.OrdinalIgnoreCase);

                    if (config.StarterKits != null)
                    {
                        foreach (var kit in config.StarterKits)
                        {
                            if (kit == null || string.IsNullOrWhiteSpace(kit.Name))
                                continue;

                            if (dict.ContainsKey(kit.Name))
                            {
                                Debug.Log($"[DMChatTeleport] Duplicate kit '{kit.Name}' found. Using first occurrence.");
                                continue;
                            }

                            // Ensure Items list isn't null
                            if (kit.Items == null)
                                kit.Items = new List<StarterKitItem>();

                            dict.Add(kit.Name, kit);
                        }
                    }

                    Kits = dict;
                    Debug.Log($"[DMChatTeleport] Loaded {Kits.Count} starter kits.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DMChatTeleport] StarterKitManager.Load failed. Path='{SavePath}'. Error: {ex}");
                    Kits = new Dictionary<string, StarterKit>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        public static bool TryGetKit(string name, out StarterKit kit)
        {
            lock (_lock)
            {
                return Kits.TryGetValue(name, out kit);
            }
        }
    }
}
