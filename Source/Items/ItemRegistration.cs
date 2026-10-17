using System;
using System.Collections.Generic;
using System.IO;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI.ObjectCreation;
using MTM101BaldAPI.Registers;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Builds every SH20XX item from the loose textures inside Textures/ltems.
    /// Spawn data, use counts and costs are read from Data/Generation/*.json.
    /// </summary>
    internal static class ItemRegistration
    {
        private const string TextureFolderName = "ltems";
        private const int SmallIconPixelsPerUnit = 25;
        private const int LargeIconPixelsPerUnit = 50;

        internal static readonly Dictionary<string, ItemObject> Items = new Dictionary<string, ItemObject>();

        /// <summary>The unlock item that gates the "Back to the old school" mode.</summary>
        internal const string CassetteReference = "99Cassettes";

        /// <summary>
        /// Flat shop price for every item in the set, in YTP. Applied to ItemObject.price
        /// so StoreScreen charges exactly this.
        /// </summary>
        internal const int ShopPrice = 200;

        internal static void Register(Plugin mod)
        {
            RegisterItem<ITM_Taser>(mod, "Taser", "ITM_Taser", "With it even the gods have to yield to you.");
            RegisterItem<ITM_Potion>(mod, "Potion", "ITM_Potion", "Drink it and you'll be immune to Baldi's BUZZ.");
            RegisterItem<ITM_99Cassettes>(mod, "99Cassettes", "ITM_99Cassettes", "99 cassettes are hiding inside this strange box.");
            RegisterItem<ITM_TestPaper>(mod, "TestPaper", "ITM_TestPaper", "Baldi cannot resist stopping to admire a stolen test paper.");

            // The four scream toys. They share one behaviour (ScreamSound) and differ only
            // in their flavour text; each Use() emits the noise that pulls characters in.
            RegisterItem<ITM_Cirno>(mod, "Cirno", "ITM_Cirno", "Press E and the Baka lets out a scream that everyone comes running to.");
            RegisterItem<ITM_FumoReimuHakurei>(mod, "FumoReimuHakurei", "ITM_FumoReimuHakurei", "Press E and this cute toy screams, drawing everyone toward you.");
            RegisterItem<ITM_Noise>(mod, "Noise", "ITM_Noise", "Press E and it shouts WOAH - loud enough that nobody can ignore it.");
            RegisterItem<ITM_SanaeKochiya>(mod, "SanaeKochiya", "ITM_SanaeKochiya", "Press E and this cute toy screams, drawing everyone toward you.");
        }

        /// <summary>
        /// Loads one item icon, tolerating the naming inconsistencies in the shipped set.
        ///
        /// The icons misspell "Small" as "Samll", and some carry a space that the other
        /// size does not (ITM_Sanae Kochiya_Samll.png next to ITM_SanaeKochiya_Large.png).
        /// A direct path is tried first, then a scan of the folder that ignores spaces in
        /// both names. Without the scan the Sanae item was skipped outright.
        /// </summary>
        private static Sprite LoadIcon(string folder, string textureName, string suffix, int ppu)
        {
            Sprite direct = ModPaths.LoadSprite(
                Path.Combine(folder, textureName + suffix + ".png"), ppu);
            if (direct != null)
            {
                return direct;
            }

            Sprite spaced = ModPaths.LoadSprite(
                Path.Combine(folder, textureName + " " + suffix + ".png"), ppu);
            if (spaced != null)
            {
                return spaced;
            }

            // Last resort: compare names with all whitespace removed.
            try
            {
                if (!Directory.Exists(folder))
                {
                    return null;
                }

                string wanted = (textureName + suffix).Replace(" ", string.Empty).ToLowerInvariant();
                foreach (string path in Directory.GetFiles(folder, "*.png"))
                {
                    string candidate = Path.GetFileNameWithoutExtension(path)
                        .Replace(" ", string.Empty).ToLowerInvariant();
                    if (candidate == wanted)
                    {
                        return ModPaths.LoadSprite(path, ppu);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not scan for the icon of '" + textureName + "': " + e.Message);
            }

            return null;
        }

        private static void RegisterItem<T>(Plugin mod, string reference, string textureName, string description)
            where T : Item
        {
            string textureFolder = ModPaths.Textures(TextureFolderName);

            // "Samll" first: that is how the files actually spell it.
            Sprite small = LoadIcon(textureFolder, textureName, "_Samll", SmallIconPixelsPerUnit)
                           ?? LoadIcon(textureFolder, textureName, "_Small", SmallIconPixelsPerUnit);
            Sprite large = LoadIcon(textureFolder, textureName, "_Large", LargeIconPixelsPerUnit);

            if (small == null || large == null)
            {
                Plugin.Log.LogError("Skipping item '" + reference + "' because its icons are missing.");
                return;
            }

            GenerationConfig config = GenerationConfig.Load(reference);
            string nameKey = textureName;
            string descKey = textureName + "_Desc";

            if (ModLocalization.Get(nameKey, null) == null)
            {
                ModLocalization.RegisterFallback(nameKey, reference);
            }

            if (ModLocalization.Get(descKey, null) == null)
            {
                ModLocalization.RegisterFallback(descKey, description);
            }

            GameObject holder = new GameObject("SH20XX_Item_" + reference);
            holder.transform.SetParent(mod.transform, false);
            T component = holder.AddComponent<T>();

            // ItemFlags.MultipleUse is metadata only; the real reuse behaviour lives
            // in each Item.Use implementation. It is derived from the json "uses".
            ItemFlags flags = config.MultipleUse ? ItemFlags.MultipleUse : ItemFlags.None;

            ItemObject item = new ItemBuilder(mod.Info)
                .SetEnum(reference)
                // The game resolves both keys through LocalizationManager, so the key
                // registered here has to be the one Localization.json defines.
                .SetNameAndDescription(nameKey, descKey)
                .SetSprites(small, large)
                .SetItemComponent(component)
                .SetGeneratorCost(config.GeneratorCost)
                .SetShopPrice(config.ShopPrice)
                .SetMeta(flags, new[] { "sh20xx", "item" })
                .Build();

            Items[reference] = item;

            // One flat price for the whole set, rather than the value derived from each
            // json's shopWeights. StoreScreen reads ItemObject.price to print and charge
            // it, and that field is public, so this is exact where the scaling was not.
            item.price = ShopPrice;

            // 99 Cassettes is not a usable item: picking it up is the whole event, so it
            // must never occupy an inventory slot and must fire straight away.
            // addToInventory is a public ItemObject field the builder does not expose.
            if (reference == CassetteReference)
            {
                item.addToInventory = false;
                Plugin.Log.LogInfo("99 Cassettes configured as a non-inventory instant unlock item.");
            }

            Plugin.Log.LogInfo(string.Format(
                "Registered item {0} (id {1}, value {2}, price {3}, uses {4}, levels [{5}]).",
                reference, (int)item.itemType, item.value, item.price,
                config.Uses > 0 ? config.Uses.ToString() : "?", config.LevelTypes));
        }
    }

    /// <summary>
    /// Mirrors one Data/Generation/*.json file.
    ///
    /// Field meanings, taken from the shape of those files:
    ///   reference    - which item this entry describes
    ///   uses         - how many times the item may be used
    ///   standardData - where it may appear: levelTypes plus a per-floor weight
    ///   shopWeights  - per-floor weight for it appearing in the shop
    /// </summary>
    internal sealed class GenerationConfig
    {
        internal const int DefaultGeneratorCost = 35;
        internal const int DefaultShopPrice = 100;

        /// <summary>
        /// Weights in the shipped files top out around 100, so that is treated as
        /// "one vanilla-tier item" and everything else is scaled from it.
        /// </summary>
        private const int WeightBaseline = 100;

        internal string Reference { get; private set; } = string.Empty;

        internal int Uses { get; private set; }

        internal int GeneratorCost { get; private set; } = DefaultGeneratorCost;

        internal int ShopPrice { get; private set; } = DefaultShopPrice;

        internal string LevelTypes { get; private set; } = "all";

        internal bool MultipleUse
        {
            get { return Uses > 1; }
        }

        internal static GenerationConfig Load(string reference)
        {
            GenerationConfig config = new GenerationConfig { Reference = reference };

            string path = ModPaths.Data("Generation", reference + ".json");
            if (!File.Exists(path))
            {
                Plugin.Log.LogWarning("No generation config for '" + reference + "', using defaults.");
                return config;
            }

            try
            {
                SimpleJson json = SimpleJson.Parse(File.ReadAllText(path));

                config.Uses = (int)json.Number("uses", 0f);

                SimpleJson standard = json.Object("standardData");
                int standardWeight = standard == null ? 0 : standard.Weight("weights");
                if (standardWeight > 0)
                {
                    config.GeneratorCost = Mathf.Clamp(
                        Mathf.RoundToInt(DefaultGeneratorCost * (standardWeight / (float)WeightBaseline)),
                        5,
                        200);
                }

                int shopWeight = json.Weight("shopWeights");
                if (shopWeight > 0)
                {
                    config.ShopPrice = Mathf.Clamp(
                        Mathf.RoundToInt(DefaultShopPrice * (shopWeight / (float)WeightBaseline)),
                        25,
                        400);
                }

                config.LevelTypes = ReadLevelTypes(standard);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Could not read generation config '" + path + "': " + e.Message);
            }

            return config;
        }

        private static string ReadLevelTypes(SimpleJson standard)
        {
            if (standard == null)
            {
                return "all";
            }

            SimpleJson levelTypes = standard.Array("levelTypes");
            if (levelTypes == null)
            {
                return "all";
            }

            var names = new List<string>();
            foreach (SimpleJson entry in levelTypes.Items())
            {
                string name = entry.AsString(null);
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }

            return names.Count == 0 ? "all" : string.Join(", ", names.ToArray());
        }

        /// <summary>
        /// Whether this item may appear in a level of the given type.
        ///
        /// LevelTypes holds the names read from the json's standardData.levelTypes,
        /// joined with ", " - or "all" when the file lists none. The comparison is by
        /// enum name, so "Schoolhouse" in the json matches LevelType.Schoolhouse.
        /// </summary>
        internal bool AllowsLevel(LevelType type)
        {
            if (string.IsNullOrEmpty(LevelTypes) || LevelTypes == "all")
            {
                return true;
            }

            string[] names = LevelTypes.Split(',');
            for (int i = 0; i < names.Length; i++)
            {
                string candidate = names[i].Trim();
                if (candidate.Length > 0 &&
                    string.Equals(candidate, type.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
