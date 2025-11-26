using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DMChatTeleport
{
    public static class StarterKitManager
    {
        private static readonly string PathConfig = "Mods/DMChatTeleport/Data/StarterKitConfig.json";

        public static readonly Dictionary<string, StarterKit> Kits =
            new Dictionary<string, StarterKit>(StringComparer.OrdinalIgnoreCase);

        public static void Load()
        {
            try
            {
                if (!File.Exists(PathConfig))
                {
                    Debug.Log($"[StarterKits] Config not found at '{PathConfig}'. No kits loaded.");
                    return;
                }

                string json = File.ReadAllText(PathConfig);
                var config = JsonConvert.DeserializeObject<StarterKitConfig>(json) ?? new StarterKitConfig();

                Kits.Clear();

                if (config.StarterKits != null)
                {
                    foreach (var kit in config.StarterKits)
                    {
                        if (kit == null || string.IsNullOrWhiteSpace(kit.Name))
                            continue;

                        if (Kits.ContainsKey(kit.Name))
                        {
                            Debug.Log($"[StarterKits] Duplicate kit '{kit.Name}' found. Using first occurrence.");
                            continue;
                        }

                        Kits.Add(kit.Name, kit);
                    }
                }

                Debug.Log($"[StarterKits] Loaded {Kits.Count} starter kits.");
            }
            catch (Exception ex)
            {
                Debug.Log($"[StarterKits] ERROR loading starter kits: {ex}");
            }
        }

        public static bool TryGetKit(string name, out StarterKit kit)
        {
            return Kits.TryGetValue(name, out kit);
        }
    }
}
