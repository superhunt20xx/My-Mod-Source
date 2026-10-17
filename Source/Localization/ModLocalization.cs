using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using MTM101BaldAPI.AssetTools;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Feeds Localization.json and Language/&lt;language&gt;/*.json into the game's
    /// localization table through AssetLoader.LocalizationFromFunction, which asks
    /// for one dictionary per language.
    ///
    /// The reader is deliberately forgiving: the shipped Localization.json contains
    /// an unquoted object, a smart quote and a missing closing brace, so anything
    /// that cannot be read is logged and skipped instead of killing the mod.
    /// </summary>
    internal static class ModLocalization
    {
        private static readonly Dictionary<string, string> entries = new Dictionary<string, string>();

        private static bool loaded;

        internal static void Load(BaseUnityPlugin mod)
        {            if (loaded)
            {
                return;
            }

            loaded = true;

            // Entries that always need to exist, whatever the json files say.
            entries["SH20XX_MainMenu_Title"] = "SH20XX";
            entries["SH20XX_Level_Mezzanine"] = "Mezzanine";

            LoadFile(Path.Combine(ModPaths.Root, "Localization.json"));

            string languageRoot = Path.Combine(ModPaths.Root, "Language");
            if (Directory.Exists(languageRoot))
            {
                string[] files = Directory.GetFiles(languageRoot, "*.json", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    LoadFile(files[i]);
                }
            }

            if (entries.Count == 0)
            {
                return;
            }

            try
            {
                AssetLoader.LocalizationFromFunction(language => new Dictionary<string, string>(entries));
                Plugin.Log.LogInfo("Registered " + entries.Count + " localization entries.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not register localization entries: " + e.Message);
            }
        }

        /// <summary>
        /// Looks an entry up. Item nameKey/descKey are resolved by the game through
        /// LocalizationManager, so these are the exact keys it will request.
        /// </summary>
        internal static string Get(string key, string fallback)
        {
            string value;
            return entries.TryGetValue(key, out value) && !string.IsNullOrEmpty(value) ? value : fallback;
        }

        /// <summary>
        /// Adds an entry that no json file provided, so a missing key never shows up
        /// as a raw identifier in game.
        /// </summary>
        internal static void RegisterFallback(string key, string value)
        {
            if (string.IsNullOrEmpty(key) || entries.ContainsKey(key))
            {
                return;
            }

            entries[key] = value ?? key;
        }

        private static void LoadFile(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            SimpleJson json;
            try
            {
                json = SimpleJson.Parse(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not parse localization file '" + path + "': " + e.Message);
                return;
            }

            SimpleJson items = json.Array("items");
            if (items == null)
            {
                // A plain { "key": "value" } file is accepted as well.
                foreach (KeyValuePair<string, SimpleJson> pair in json.Entries())
                {
                    string flat = pair.Value.AsString(null);
                    if (flat != null)
                    {
                        entries[pair.Key] = flat;
                    }
                }

                return;
            }

            int added = 0;
            foreach (SimpleJson item in items.Items())
            {
                string key = item.String("key");
                string value = item.String("value");
                if (!string.IsNullOrEmpty(key) && value != null)
                {
                    entries[key] = value;
                    added++;
                }
            }

            Plugin.Log.LogInfo("Read " + added + " localization entries from " + Path.GetFileName(path) + ".");
        }
    }
}
