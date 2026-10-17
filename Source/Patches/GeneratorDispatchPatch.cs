using System;
using System.Reflection;
using HarmonyLib;
using MTM101BaldAPI.Registers;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Re-enables MTM101BaldAPI's level-generation dispatch.
    ///
    /// Mods register level-generation callbacks through
    /// GeneratorManagement.Register(plugin, type, action), and
    /// GeneratorManagement.Invoke(name, floorNumber, sceneObject) is what actually
    /// runs them. In this install Invoke has no callers anywhere:
    ///
    ///   GeneratorManagement::Invoke  <- 0 references in MTM101BaldAPI.dll
    ///                                   and Assembly-CSharp.dll
    ///
    /// The practical effect is that every registered generation callback is dead
    /// code, so content that only spawns through that hook (custom vending machines,
    /// for example) never appears in a level. This postfix calls Invoke once per
    /// generation so those registrations start working again.
    ///
    /// It hooks LevelBuilder.StartGenerate because that is where the LevelData is
    /// assembled, and the postfix runs after the body has initialised its fields.
    /// </summary>
    internal static class GeneratorDispatchPatch
    {
        private static bool dispatchedThisLevel;
        private static bool reported;

        internal static void TryPatch(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(typeof(LevelBuilder), "StartGenerate");
            if (target == null)
            {
                Plugin.Log.LogWarning("Generator dispatch: LevelBuilder.StartGenerate was not found.");
                return;
            }

            try
            {
                harmony.Patch(target,
                    prefix: new HarmonyMethod(
                        AccessTools.Method(typeof(GeneratorDispatchPatch), "StartGeneratePrefix")),
                    postfix: new HarmonyMethod(
                        AccessTools.Method(typeof(GeneratorDispatchPatch), "StartGeneratePostfix")));
                Plugin.Log.LogInfo("Generator dispatch patch applied to LevelBuilder.StartGenerate.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Generator dispatch: could not patch StartGenerate: " + e.Message);
            }
        }

        /// <summary>
        /// A new generation is starting, so allow this level's dispatch. Without this
        /// the once-per-level guard would keep the callbacks from ever running again.
        /// </summary>
        private static void StartGeneratePrefix(LevelBuilder __instance)
        {
            dispatchedThisLevel = false;

            // Report what the builder looks like at this moment, because "scene is null
            // here" is exactly the failure that stopped the item pools from ever being
            // filled.
            if (__instance != null)
            {
                try
                {
                    Plugin.Log.LogInfo("Generator dispatch: StartGenerate begins; builder.scene=" +
                                       (__instance.scene == null ? "null" : __instance.scene.name) +
                                       ", levelObject=" +
                                       (__instance.scene == null || __instance.scene.levelObject == null
                                            ? "null"
                                            : __instance.scene.levelObject.name));
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Generator dispatch: could not inspect the builder: " + e.Message);
                }
            }

            // Dispatch here rather than only in the postfix: the item pools must be filled
            // before the level data is assembled, otherwise the pool copy has already
            // happened and the additions are ignored.
            Dispatch(__instance);
        }

        private static void StartGeneratePostfix(LevelBuilder __instance)
        {
            // Second chance for anything the prefix could not do.
            Dispatch(__instance);
        }

        /// <summary>
        /// Runs the registered generation callbacks once per level.
        /// </summary>
        private static void Dispatch(LevelBuilder builder)
        {
            if (builder == null || dispatchedThisLevel)
            {
                return;
            }

            SceneObject scene = SceneOf(builder);
            if (scene == null)
            {
                return;
            }

            try
            {
                dispatchedThisLevel = true;
                GeneratorManagement.Invoke(scene.name, scene.levelNo, scene);

                if (!reported)
                {
                    reported = true;
                    Plugin.Log.LogInfo("Generator dispatch: ran the registered generation callbacks for '" +
                                       scene.name + "' (floor " + scene.levelNo + ").");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Generator dispatch: Invoke failed for '" + scene.name + "': " + e);
            }
        }

        /// <summary>
        /// Finds the SceneObject being generated.
        ///
        /// The builder's own public "scene" field is the right source. This used to read
        /// CoreGameManager.Instance.sceneObject first, which is null while a level is being
        /// built - so the postfix returned early on every generation and GeneratorManagement
        /// .Invoke never ran. That is why no item ever reached a level's item pool.
        /// </summary>
        private static SceneObject SceneOf(LevelBuilder builder)
        {
            if (builder == null)
            {
                return null;
            }

            try
            {
                if (builder.scene != null)
                {
                    return builder.scene;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Generator dispatch: could not read the builder's scene: " + e.Message);
            }

            try
            {
                CoreGameManager manager = CoreGameManager.Instance;
                if (manager != null && manager.sceneObject != null)
                {
                    return manager.sceneObject;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Generator dispatch: could not read the active scene: " + e.Message);
            }

            return null;
        }

        /// <summary>
        /// Clears the once-per-level guard. Called when a new level starts so the
        /// callbacks run again for the next one.
        /// </summary>
        internal static void Reset()
        {
            dispatchedThisLevel = false;
        }
    }
}
