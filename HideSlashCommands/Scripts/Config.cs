using System;
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
                        Config = new ModConfig();
                        Save(); // create file immediately
                        Debug.Log("[DMChatTeleport] config.json created with defaults.");
                        return;
                    }

                    string json = File.ReadAllText(path);

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        Config = new ModConfig();
                        Save();
                        return;
                    }

                    Config = JsonConvert.DeserializeObject<ModConfig>(json) ?? new ModConfig();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DMChatTeleport] ConfigManager.Load failed. Path='{ConfigPath}'. Error: {ex}");
                    Config = new ModConfig();
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

                    // atomic-ish write
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
    }
}
