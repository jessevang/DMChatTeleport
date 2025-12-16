using System.IO;
using Newtonsoft.Json;

namespace DMChatTeleport
{
    public class ModConfig
    {
        public bool TurnOnTeleportCommands = true;
        public bool TurnOnStarterKits = true;
        public bool TurnOnHideCommandsWithSlashes = true;

        // NEW: shared cooldown for all teleport commands that actually teleport (e.g. /base and /return)
        // 0 = no cooldown
        public int TeleportCooldownSeconds = 0;
    }

    public static class ConfigManager
    {
        private static readonly string ConfigPath = "Mods/DMChatTeleport/config.json";

        public static ModConfig Config { get; private set; }

        public static void Load()
        {
            if (!File.Exists(ConfigPath))
            {
                Config = new ModConfig();
                Save();
                return;
            }

            try
            {
                string json = File.ReadAllText(ConfigPath);
                Config = JsonConvert.DeserializeObject<ModConfig>(json) ?? new ModConfig();
            }
            catch
            {
                Config = new ModConfig();
                Save();
            }
        }

        public static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(Config, Formatting.Indented));
        }
    }
}
