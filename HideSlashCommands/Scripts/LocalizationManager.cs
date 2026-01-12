using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DMChatTeleport
{
    internal static class L
    {
        private static readonly object _lock = new object();

        // lang -> (key -> text)
        private static readonly Dictionary<string, Dictionary<string, string>> _cache =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private const string DefaultLang = "en";

        private static string Folder =>
            GameIO.GetGameDir("Mods/DMChatTeleport/Localization");

        // ---------- Public API ----------

        public static string Get(string lang, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "";

            lang = NormalizeLang(lang);

            var dict = GetDictionary(lang);

            if (dict.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;

            // fallback to English
            if (!lang.Equals(DefaultLang, StringComparison.OrdinalIgnoreCase))
            {
                var en = GetDictionary(DefaultLang);
                if (en.TryGetValue(key, out var enValue))
                    return enValue;
            }

            // visible missing-key marker for debugging
            return $"[{key}]";
        }

        public static string Format(string lang, string key, params (string name, object value)[] args)
        {
            string text = Get(lang, key);

            if (args == null || args.Length == 0)
                return text;

            foreach (var (name, value) in args)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                text = text.Replace("{" + name + "}", value?.ToString() ?? "");
            }

            return text;
        }

        public static IEnumerable<string> GetAvailableLanguages()
        {
            EnsureFolder();

            foreach (var file in Directory.GetFiles(Folder, "*.json"))
            {
                yield return Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            }
        }

        // ---------- Internal ----------

        private static Dictionary<string, string> GetDictionary(string lang)
        {
            lang = NormalizeLang(lang);

            lock (_lock)
            {
                if (_cache.TryGetValue(lang, out var dict))
                    return dict;

                dict = LoadLanguageFile(lang);
                _cache[lang] = dict;
                return dict;
            }
        }

        private static Dictionary<string, string> LoadLanguageFile(string lang)
        {
            EnsureFolder();

            string path = Path.Combine(Folder, $"{lang}.json");

            if (!File.Exists(path))
            {
                if (lang == DefaultLang)
                    WriteDefaultEnglish(path);

                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DMChatTeleport] Failed to load localization '{lang}': {ex}");
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void WriteDefaultEnglish(string path)
        {
            var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bloodmoon.end.no_kills"] = "Blood Moon ended! No kills were recorded.",
                ["bloodmoon.end.title"] = "Blood Moon ended!",
                ["bloodmoon.total_kills.header"] = "Total Kills",
                ["bloodmoon.total_kills.rank_line"] = "{rank}. {name} - {kills} kills",
                ["bloodmoon.total_kills.footer"] = "Total kills: {total}",

                ["bloodmoon.party.header"] = "Party Results",
                ["bloodmoon.party.none"] = "No party kills recorded.",
                ["bloodmoon.party.title"] = "{partyTitle}",
                ["bloodmoon.party.members"] = "Members: {members}",
                ["bloodmoon.party.total"] = "Party Total Kills: {kills}",

                ["lang.current"] = "Current language: {lang}",
                ["lang.set"] = "Language set to {lang}",
                ["lang.invalid"] = "Invalid language. Available: {list}"
            };

            File.WriteAllText(path, JsonConvert.SerializeObject(defaults, Formatting.Indented));
        }

        private static void EnsureFolder()
        {
            if (!Directory.Exists(Folder))
                Directory.CreateDirectory(Folder);
        }

        private static string NormalizeLang(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
                return DefaultLang;

            return lang.Trim().ToLowerInvariant();
        }
    }
}
