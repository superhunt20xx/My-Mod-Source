using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Guarantees at least one mod item on each of the school's first five floors.
    ///
    /// Adding items to LevelObject.potentialItems only makes them eligible - the
    /// generator may still place none, which is what "the school has no items" was. A
    /// guarantee needs a deliberate placement, so once a level has finished generating
    /// this walks the rooms and drops one item into the first that will accept it.
    ///
    /// The engine's own placement call is used rather than instantiating a pickup by
    /// hand, because PlaceItemInRandomRoom already respects each room's min/max item
    /// value and its item spawn points. It silently does nothing when no room qualifies,
    /// so this retries across several candidate items before giving up.
    /// </summary>
    internal static class ItemGuarantee
    {
        /// <summary>Floors that must each contain one of the mod's items.</summary>
        private const int FirstFloor = 1;
        private const int LastFloor = 5;

        /// <summary>How many different items to try before accepting the floor has none.</summary>
        private const int Attempts = 8;

        /// <summary>
        /// The builder for the level being generated, captured by GeneratorDispatchPatch
        /// since a fresh one exists per level.
        /// </summary>
        private static LevelBuilder builder;

        /// <summary>Set once a level has been handled, so the guarantee runs once each.</summary>
        private static int handledLevel = -1;

        internal static void CaptureBuilder(LevelBuilder instance)
        {
            builder = instance;
        }

        /// <summary>
        /// Hooks the guarantee to the point where the level is built and play is about to
        /// start, which is the first moment a Pickup can be placed.
        /// </summary>
        internal static void TryPatch(Harmony harmony)
        {
            System.Reflection.MethodInfo target =
                AccessTools.Method(typeof(BaseGameManager), "BeginPlay");
            if (target == null)
            {
                Plugin.Log.LogWarning("Item guarantee: BaseGameManager.BeginPlay was not found.");
                return;
            }

            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(ItemGuarantee), "BeginPlayPostfix")));
                Plugin.Log.LogInfo("Item guarantee patch applied to BaseGameManager.BeginPlay.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Item guarantee: could not patch BeginPlay: " + e.Message);
            }
        }

        private static void BeginPlayPostfix()
        {
            TryPlaceOnCurrentFloor();
        }

        internal static void TryPlaceOnCurrentFloor()
        {
            try
            {
                Place();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Item guarantee: failed: " + e);
            }
        }

        private static void Place()
        {
            LevelBuilder level = builder != null ? builder : UnityEngine.Object.FindObjectOfType<LevelBuilder>();
            if (level == null)
            {
                Plugin.Log.LogWarning("Item guarantee: no LevelBuilder is available.");
                return;
            }

            CoreGameManager manager = CoreGameManager.Instance;
            if (manager == null || manager.sceneObject == null)
            {
                Plugin.Log.LogWarning("Item guarantee: CoreGameManager has no active scene.");
                return;
            }

            SceneObject scene = manager.sceneObject;
            int floor = scene.levelNo;
            bool school = IsSchoolhouse(scene);

            // Every early exit is logged, because the guarantee previously bailed out
            // silently at one of these checks and there was no way to tell which.
            Plugin.Log.LogInfo("Item guarantee: evaluating scene '" + scene.name + "', floor=" + floor +
                               ", type=" + DescribeType(scene) + ", schoolhouse=" + school +
                               ", alreadyHandled=" + (handledLevel == floor) + ".");

            if (floor < FirstFloor || floor > LastFloor)
            {
                return;
            }

            if (handledLevel == floor)
            {
                return;
            }

            // Only the first five floors of a normal run: endless and editor levels reuse
            // level numbers, and those already get items from the ordinary pool.
            if (!school)
            {
                return;
            }

            List<ItemObject> candidates = Candidates();
            if (candidates.Count == 0)
            {
                Plugin.Log.LogWarning("Item guarantee: there are no items to place.");
                return;
            }

            var rng = new System.Random();

            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                ItemObject item = candidates[rng.Next(candidates.Count)];

                // lowEnd: false, highEnd: false - the room's own value range decides.
                level.PlaceItemInRandomRoom(item, false, false, rng);

                // The call has no return value, so success is confirmed by looking for the
                // item in the level afterwards.
                if (IsPresent(level, item))
                {
                    handledLevel = floor;
                    Plugin.Log.LogInfo("Item guarantee: placed '" + item.nameKey + "' on floor " + floor + ".");
                    return;
                }
            }

            Plugin.Log.LogWarning("Item guarantee: floor " + floor +
                                  " has no room that accepts any mod item; nothing was placed.");
        }

        /// <summary>
        /// True when the level already contains a pickup of this item, which is how a
        /// successful PlaceItemInRandomRoom is detected.
        /// </summary>
        private static bool IsPresent(LevelBuilder level, ItemObject item)
        {
            try
            {
                foreach (Pickup pickup in UnityEngine.Object.FindObjectsOfType<Pickup>())
                {
                    if (pickup != null && pickup.item == item)
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Item guarantee: could not verify placement: " + e.Message);
            }

            return false;
        }

        /// <summary>
        /// A readable description of the scene's level type, for the diagnostic line.
        /// </summary>
        private static string DescribeType(SceneObject scene)
        {
            try
            {
                if (scene.levelObject != null)
                {
                    return scene.levelObject.type.ToString();
                }

                if (scene.randomizedLevelObject != null && scene.randomizedLevelObject.Length > 0 &&
                    scene.randomizedLevelObject[0] != null && scene.randomizedLevelObject[0].selection != null)
                {
                    return scene.randomizedLevelObject[0].selection.type + " (randomized)";
                }

                return "none (levelObject and randomizedLevelObject are both empty)";
            }
            catch (Exception e)
            {
                return "unreadable (" + e.Message + ")";
            }
        }

        /// <summary>Items eligible for placement, excluding the cassette unlock.</summary>
        private static List<ItemObject> Candidates()
        {
            var result = new List<ItemObject>();

            foreach (KeyValuePair<string, ItemObject> pair in ItemRegistration.Items)
            {
                if (pair.Value != null && pair.Key != ItemRegistration.CassetteReference)
                {
                    result.Add(pair.Value);
                }
            }

            return result;
        }

        /// <summary>
        /// Whether the scene is a Schoolhouse level. Uses the level data's type, which is
        /// what distinguishes the main run's floors from endless and edited levels.
        /// </summary>
        private static bool IsSchoolhouse(SceneObject scene)
        {
            try
            {
                if (scene.levelObject != null)
                {
                    return scene.levelObject.type == LevelType.Schoolhouse;
                }

                if (scene.randomizedLevelObject != null && scene.randomizedLevelObject.Length > 0)
                {
                    WeightedLevelObject first = scene.randomizedLevelObject[0];
                    return first != null && first.selection != null &&
                           first.selection.type == LevelType.Schoolhouse;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Item guarantee: could not read the level type: " + e.Message);
            }

            return false;
        }
    }
}
