using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ModdedModesAPI.ModesAPI;
using TMPro;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Adds an "Explore Normal" mode screen through ModdedModesAPI.
    ///
    /// The lifecycle it drives (all traced through the IL):
    ///
    ///   CustomModesHandler.OnMainMenuInitialize
    ///     -> create a screen, a mode button and a seed input
    ///
    ///   pressing the mode button (ModdedModesAPI's own handler)
    ///     -> loader.seedInput = screen's SeedInput, loader.CheckSeed()
    ///     -> loader.Initialize(lives)
    ///     -> loader.AssignElevatorScreen(...)
    ///     -> loader.LoadLevel(scene)
    ///     -> loader.SetMode(mode)
    ///     -> loader.SetSave(createsASave)
    ///
    /// The LifeMode needs two extra steps, because GameLoader.Initialize overwrites
    /// CoreGameManager.lifeMode with Normal(0):
    ///   * GameLoader.ApplyGameSettingsForHideSeek is the game's own routine for
    ///     pushing PlayerFileManager.lifeMode into CoreGameManager (it also picks
    ///     Mode.Free for Intense/Explorer). It is public, parameterless, and the
    ///     custom-mode path never calls it.
    ///   * So PlayerFileManager.lifeMode is set first, and ApplyGameSettingsForHideSeek
    ///     is invoked again once GameLoader.Initialize has finished.
    ///
    /// Seed input works out of the box: ModeObject.allowSeedInputCreation defaults to
    /// true, and ModdedModesAPI calls CheckSeed() itself when a screen has one.
    /// </summary>
    internal static class ExploreNormalMode
    {
        internal const string ScreenKey = "SH20XX_ExploreNormal";
        private const string ModeLabelKey = "SH20XX_Mode_ExploreNormal";
        private const string ModeLabelFallback = "Explore Normal";
        private const string SeedLabelKey = "SH20XX_Mode_Seed";
        private const string SeedLabelFallback = "Seed";

        private static bool registered;
        private static bool built;

        /// <summary>
        /// Set while the Explore Normal button is starting a game, so
        /// <see cref="ExploreNormalModePatch"/> knows to re-apply the LifeMode.
        /// </summary>
        internal static bool PendingExplorer;

        internal static void Register(Plugin mod)
        {
            if (registered || mod == null)
            {
                return;
            }

            if (!Available())
            {
                Plugin.Log.LogInfo("ModdedModesAPI was not detected; the Explore Normal mode is skipped.");
                return;
            }

            registered = true;

            try
            {
                CustomModesHandler.OnMainMenuInitialize += Build;
                Plugin.Log.LogInfo("Registered the Explore Normal mode callback.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not register the Explore Normal mode: " + e);
            }
        }

        private static bool Available()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetType("ModdedModesAPI.ModesAPI.CustomModesHandler", false) != null)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ build

        private static void Build()
        {
            if (built)
            {
                return;
            }

            built = true;

            try
            {
                ModeObject screen = ModeObject.CreateModeObjectOverExistingScreen(SelectionScreen.MainScreen);
                if (screen == null)
                {
                    Plugin.Log.LogError("Explore Normal: CreateModeObjectOverExistingScreen returned null.");
                    return;
                }

                screen.ScreenTransform.gameObject.name = ScreenKey;

                SceneObject scene = FindStartingScene();
                if (scene == null)
                {
                    Plugin.Log.LogError("Explore Normal: no starting SceneObject was found; the mode needs one.");
                    return;
                }

                ButtonBuilder builder = screen.StandardButtonBuilder;

                // A seed input is optional to ModdedModesAPI, but this mode is meant to
                // expose one, and ModdedModesAPI wires CheckSeed() for us when present.
                SeedInput seedInput = null;
                try
                {
                    builder.CreateSeedInput(out seedInput);
                    ApplySeedLabel(seedInput);
                    Plugin.Log.LogInfo("Explore Normal: seed input created.");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Explore Normal: could not create the seed input: " + e.Message);
                }

                // The button's own press handler starts the game; this only sets
                // PendingExplorer so the LifeMode can be applied afterwards.
                StandardMenuButton button = builder.CreateModeButton(scene, false, 3, Mode.Main, null);
                if (button != null)
                {
                    button.OnPress.AddListener(() => PendingExplorer = true);
                }

                builder.AddDescriptionText(button, ModeLabelKey);

                Plugin.Log.LogInfo("Explore Normal mode built on scene '" + scene.name + "'.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not build the Explore Normal mode: " + e);
            }
        }

        /// <summary>
        /// SeedInput.tmp is internal to Assembly-CSharp, so it is set reflectively.
        /// Failure here is cosmetic only: ModdedModesAPI already writes its own label.
        /// </summary>
        private static void ApplySeedLabel(SeedInput seedInput)
        {
            if (seedInput == null)
            {
                return;
            }

            try
            {
                FieldInfo tmpField = AccessTools.Field(seedInput.GetType(), "tmp");
                TMP_Text text = tmpField == null ? null : tmpField.GetValue(seedInput) as TMP_Text;
                if (text != null)
                {
                    text.text = Label(SeedLabelKey, SeedLabelFallback) + ": Random";
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Explore Normal: could not relabel the seed input: " + e.Message);
            }
        }

        /// <summary>
        /// The game's starting scene for a normal run: the first Schoolhouse floor.
        /// </summary>
        private static SceneObject FindStartingScene()
        {
            // Only consider real, loadable scenes, and never our own content. This mod
            // builds five Mezzanine floors before this runs, and the old name-only
            // search used to latch onto 'SH20XX_Mezzanine_5' as the starting level.
            List<SceneObject> scenes = Resources.FindObjectsOfTypeAll<SceneObject>()
                .Where(s => s != null)
                .Where(s => !string.IsNullOrEmpty(s.name))
                .Where(s => s.name.IndexOf("SH20XX", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(s => s.levelObject != null ||
                            (s.randomizedLevelObject != null && s.randomizedLevelObject.Length > 0))
                .ToList();

            if (scenes.Count == 0)
            {
                Plugin.Log.LogWarning("Explore Normal: no usable SceneObject was found at all.");
                return null;
            }

            // 1. The proper answer: level data that actually is a Schoolhouse level.
            SceneObject schoolhouse = scenes.FirstOrDefault(s => LevelTypeOf(s) == LevelType.Schoolhouse);
            if (schoolhouse != null)
            {
                Plugin.Log.LogInfo("Explore Normal: starting scene '" + schoolhouse.name + "' (Schoolhouse type).");
                return schoolhouse;
            }

            // 2. Fall back to a Schoolhouse-looking name or title.
            schoolhouse = scenes.FirstOrDefault(s =>
                Contains(s.name, "Schoolhouse") || Contains(s.levelTitle, "Schoolhouse"));
            if (schoolhouse != null)
            {
                Plugin.Log.LogInfo("Explore Normal: starting scene '" + schoolhouse.name + "' (name match).");
                return schoolhouse;
            }

            // 3. Fail visibly instead of silently starting on something unrelated.
            Plugin.Log.LogWarning("Explore Normal: no Schoolhouse scene found; the mode has no start level.");
            return null;
        }

        /// <summary>
        /// LevelType of a scene's level data, checking the fixed level first and then
        /// the randomized pool.
        /// </summary>
        private static LevelType LevelTypeOf(SceneObject scene)
        {
            if (scene.levelObject != null)
            {
                return scene.levelObject.type;
            }

            if (scene.randomizedLevelObject != null)
            {
                for (int i = 0; i < scene.randomizedLevelObject.Length; i++)
                {
                    WeightedLevelObject entry = scene.randomizedLevelObject[i];
                    if (entry != null && entry.selection != null)
                    {
                        return entry.selection.type;
                    }
                }
            }

            return LevelType.Schoolhouse;
        }

        private static bool Contains(string value, string needle)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Finds the active GameLoader. ModdedModesAPI caches one in its internal
        /// ResourceStorage type, which is not accessible, so the scene is searched.
        /// </summary>
        private static GameLoader FindLoader()
        {
            List<GameLoader> loaders = Resources.FindObjectsOfTypeAll<GameLoader>()
                .Where(l => l != null && l.gameObject.scene.IsValid())
                .ToList();

            return loaders.FirstOrDefault(l => l.useSeed || l.seed != 0) ?? loaders.FirstOrDefault();
        }

        private static string Label(string key, string fallback)
        {
            return ModLocalization.Get(key, fallback);
        }

        // ------------------------------------------------------------------ activation

        /// <summary>
        /// Applies the Explorer life mode once GameLoader.Initialize has reset it.
        /// </summary>
        internal static void ApplyExplorerLifeMode()
        {
            try
            {
                PlayerFileManager files = PlayerFileManager.Instance;
                if (files == null)
                {
                    return;
                }

                files.lifeMode = LifeMode.Explorer;

                GameLoader loader = FindLoader();
                if (loader == null)
                {
                    Plugin.Log.LogWarning("Explore Normal: no GameLoader was found; life mode not applied.");
                    return;
                }

                // Re-runs the game's own routine so lifeMode, Mode and the challenge
                // flags all come across from PlayerFileManager.
                loader.ApplyGameSettingsForHideSeek();

                Plugin.Log.LogInfo("Explore Normal: life mode set to Explorer (" +
                                   "lives=" + CoreGameManager.Instance.Lives +
                                   ", mode=" + CoreGameManager.Instance.currentMode + ").");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Explore Normal: could not apply the Explorer life mode: " + e);
            }
        }
    }

    /// <summary>
    /// GameLoader.Initialize hard-sets CoreGameManager.lifeMode to Normal, so the
    /// Explorer life mode has to be re-applied after it returns.
    /// </summary>
    internal static class ExploreNormalModePatch
    {
        internal static void TryPatch(HarmonyLib.Harmony harmony)
        {
            var target = HarmonyLib.AccessTools.Method(typeof(GameLoader), "Initialize");
            if (target == null)
            {
                Plugin.Log.LogWarning("Explore Normal: GameLoader.Initialize was not found.");
                return;
            }

            try
            {
                harmony.Patch(target, postfix: new HarmonyLib.HarmonyMethod(
                    HarmonyLib.AccessTools.Method(typeof(ExploreNormalModePatch), "InitializePostfix")));
                Plugin.Log.LogInfo("Explore Normal patch applied to GameLoader.Initialize.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Explore Normal: could not patch GameLoader.Initialize: " + e.Message);
            }
        }

        private static void InitializePostfix()
        {
            if (!ExploreNormalMode.PendingExplorer)
            {
                return;
            }

            ExploreNormalMode.PendingExplorer = false;
            ExploreNormalMode.ApplyExplorerLifeMode();
        }
    }
}
