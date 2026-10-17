using System;
using System.Collections.Generic;
using MTM101BaldAPI.Registers;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Adds the mod's items to each level's item pool.
    ///
    /// This is the missing half of item registration. Registering an ItemObject makes it
    /// exist, but nothing places it: LevelBuilder only ever draws from
    /// LevelGenerationParameters.potentialItems, which AssignData copies out of
    /// LevelObject.potentialItems. Unless a mod appends to that list itself, its items
    /// never appear in a procedurally generated level - they only exist in the shop and
    /// in the editor.
    ///
    /// The hook is MTM101's own generation callback, registered at the Preparation stage
    /// (the first stage, before AssignData copies anything). GeneratorDispatchPatch is
    /// what actually drives Invoke, since no other assembly calls it.
    ///
    /// Whether an item belongs in a given level is decided by its own
    /// standardData.levelTypes, so Taser ends up in a Schoolhouse and 99 Cassettes does
    /// not.
    /// </summary>
    internal static class ItemGeneration
    {
        /// <summary>Weight used for the pool entry, on the same scale as the json.</summary>
        private const int PoolWeight = 100;

        /// <summary>Weight for shop stock entries, matching the room-pool scale.</summary>
        private const int ShopWeight = 100;

        /// <summary>One-shot flags so the diagnostic logs do not repeat per scene.</summary>
        private static bool reportedCall;
        private static bool reportedNull;

        internal static void Register(Plugin mod)
        {
            if (mod == null)
            {
                return;
            }

            try
            {
                GeneratorManagement.Register(mod, GenerationModType.Preparation, AddItems);
                Plugin.Log.LogInfo("Item generation callback registered (Preparation stage).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Item generation: could not register the callback: " + e);
            }
        }

        private static void AddItems(string sceneName, int floor, SceneObject scene)
        {
            // Logged on entry: the callback never producing an "added" line left it
            // ambiguous whether it was never called or bailed out at this check.
            if (!reportedCall)
            {
                reportedCall = true;
                Plugin.Log.LogInfo("Items: generation callback fired (first: scene=" + sceneName +
                                   ", floor=" + floor +
                                   ", sceneObject=" + (scene == null ? "null" : "ok") + ").");
            }

            if (scene == null || scene.levelObject == null)
            {
                if (!reportedNull)
                {
                    reportedNull = true;
                    Plugin.Log.LogWarning("Items: scene '" + sceneName +
                                          "' has no levelObject, so no item pool could be filled.");
                }

                return;
            }

            LevelObject level = scene.levelObject;
            LevelType type = level.type;
            int added = 0;

            try
            {
                var pool = new List<WeightedItemObject>(
                    level.potentialItems ?? new WeightedItemObject[0]);

                foreach (KeyValuePair<string, ItemObject> pair in ItemRegistration.Items)
                {
                    ItemObject item = pair.Value;
                    if (item == null)
                    {
                        continue;
                    }

                    // 99 Cassettes is placed deliberately in a faculty room by
                    // CassetteSpawn; letting it into the random pool would defeat that.
                    if (pair.Key == ItemRegistration.CassetteReference)
                    {
                        continue;
                    }

                    GenerationConfig config = GenerationConfig.Load(pair.Key);
                    if (!config.AllowsLevel(type))
                    {
                        continue;
                    }

                    if (AlreadyIn(pool, item))
                    {
                        continue;
                    }

                    pool.Add(new WeightedItemObject
                    {
                        selection = item,
                        weight = PoolWeight,
                    });
                    added++;
                }

                if (added > 0)
                {
                    level.potentialItems = pool.ToArray();
                    Plugin.Log.LogInfo("Items: added " + added + " item(s) to '" + sceneName +
                                       "' pool (level type " + type + ", total " + pool.Count + ").");
                }

                AddShopItems(level, sceneName);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Item generation failed for '" + sceneName + "': " + e);
            }
        }

        /// <summary>
        /// Adds the same items to the level's shop stock.
        ///
        /// The shop is a separate pool: StoreScreen.Start draws it from
        /// LevelGenerationParameters.shopItems, which AssignData copies from
        /// LevelObject.shopItems. Populating potentialItems alone only affects items
        /// scattered around rooms, so the shop needs this second list.
        ///
        /// 99 Cassettes is excluded here too - it is an unlock, not merchandise.
        /// </summary>
        private static void AddShopItems(LevelObject level, string sceneName)
        {
            var pool = new List<WeightedItemObject>(level.shopItems ?? new WeightedItemObject[0]);
            int added = 0;

            foreach (KeyValuePair<string, ItemObject> pair in ItemRegistration.Items)
            {
                ItemObject item = pair.Value;
                if (item == null || pair.Key == ItemRegistration.CassetteReference)
                {
                    continue;
                }

                if (AlreadyIn(pool, item))
                {
                    continue;
                }

                pool.Add(new WeightedItemObject
                {
                    selection = item,
                    weight = ShopWeight,
                });
                added++;
            }

            if (added > 0)
            {
                level.shopItems = pool.ToArray();
                Plugin.Log.LogInfo("Shop: added " + added + " item(s) to '" + sceneName +
                                   "' shop stock at " + ItemRegistration.ShopPrice + " YTP each.");
            }
        }

        private static bool AlreadyIn(List<WeightedItemObject> pool, ItemObject item)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].selection == item)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
