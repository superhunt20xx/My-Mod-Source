using System.Collections;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.Registers;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// SH20XX.BBPP - Baldi's Basics Plus 0.14.5 mod plugin.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("mtm101.rulerp.bbplus.baldidevapi", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("mtm101.rulerp.baldiplus.levelloader", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "SH20XX.BBPP";
        internal const string PluginName = "SH20XX BBPP";
        internal const string PluginVersion = "1.0.0";

        internal static Plugin Instance { get; private set; }

        internal static ManualLogSource Log { get; private set; }

        /// <summary>
        /// True once PlusLevelLoader has been confirmed present at runtime.
        /// </summary>
        internal static bool HasPlusLevelLoader { get; private set; }

        private Harmony harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            ModPaths.Initialize(this);
            ModLocalization.Load(this);

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll();

            // Door materials need an explicit patch: the attribute-based approach on
            // StandardDoor.Start silently never fired, so both candidates are patched
            // by hand and the result is logged.
            DoorMaterialPatch.TryPatch(harmony);

            // These are all patched by hand for the same reason: a failed
            // [HarmonyPatch] registration is silent, and one such failure already cost
            // a debugging cycle.
            SeedUnlockPatch.TryPatch(harmony);

            // OldElevatorMode is deliberately NOT patched.
            //
            // It hid the elevator's green button every frame to recreate the pre-0.14
            // elevator, and because it hid it unconditionally the button also vanished on
            // the shop floor - where it is the only way in. The button is meant to stay
            // visible, so the patch is off and the game keeps its own 0.14 behaviour.
            //
            // OldElevatorMode.TryPatch(harmony);

            ExploreNormalModePatch.TryPatch(harmony);
            GeneratorDispatchPatch.TryPatch(harmony);

            // Cassette unlock: the pickup is what records the unlock and quits, and the
            // spawn places it in a faculty room on floor 5.
            CassettePickupPatch.TryPatch(harmony);
            CassetteSpawn.TryPatch(harmony);

            // Baldi's 56% chance to think for 10 seconds (the Thinking... sticker).
            BaldiThinkPatch.TryPatch(harmony);

            // Guarantees one mod item on each of the school's first five floors.
            ItemGuarantee.TryPatch(harmony);

            // Adds a Credits page to the options menu.
            OptionsCreditsPage.TryPatch(harmony);

            // Adds the mod's own settings page (Baldi / NPC spawn toggles), and makes
            // those toggles actually affect a normal run.
            OptionsSettingsPage.TryPatch(harmony);
            NpcSpawnFilter.TryPatch(harmony);

            // Shows a random line from splashes.txt above the version number on the menu.
            MenuSplash.TryPatch(harmony);

            // Every piece of content touches game assets, so wait for them to load.
            LoadingEvents.RegisterOnAssetsLoaded(Info, RegisterContent, LoadingEventOrder.Post);

            StartCoroutine(RegisterWarningSoon());

            Log.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        /// <summary>
        /// Registers the alpha warning a moment after Awake. The decision depends on whether
        /// the separate CustomWarningPage plugin is present, and BepInEx may load that
        /// plugin after this one - checking in Awake missed it and always kept the notice.
        /// The warning scene only appears after the game has loaded its assets, so one
        /// second is comfortably early.
        /// </summary>
        private IEnumerator RegisterWarningSoon()
        {
            yield return new WaitForSecondsRealtime(1f);
            RegisterWarning();
        }

        /// <summary>
        /// Queues the mod's alpha warning. The warning screen renders into a TMP_Text,
        /// so rich text tags are honoured and "alpha" is shown in red.
        ///
        /// When the separate CustomWarningPage plugin is installed and has text to show,
        /// its page is the warning the player reads; showing the alpha notice after it
        /// would just add a second page, so this one is skipped. The check is done by
        /// reflection: there is no compile-time or load-time dependency between the two
        /// plugins, and the notice comes back as soon as CustomWarningPage is removed.
        /// </summary>
        private void RegisterWarning()
        {
            if (CustomWarningPageActive())
            {
                Log.LogInfo("A custom warning page is active; the alpha warning page is skipped.");
                return;
            }

            const string message =
                "The current mod is in <color=#FF0000><b>alpha</b></color>, so there might be some bugs. " +
                "Please bear with us.";

            try
            {
                MTM101BaldiDevAPI.AddWarningScreen(message, false);
                Log.LogInfo("Registered the alpha warning screen.");
            }
            catch (System.Exception e)
            {
                Log.LogError("Could not register the warning screen: " + e);
            }
        }

        /// <summary>
        /// Looks for CustomWarningPage.WarningPageAPI.IsCustomized in the loaded
        /// assemblies. Reflection keeps the two plugins independent: neither references
        /// the other, and this returns false when the plugin is not installed.
        /// </summary>
        private static bool CustomWarningPageActive()
        {
            try
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    System.Type api = assembly.GetType("CustomWarningPage.WarningPageAPI", false);
                    if (api == null)
                    {
                        continue;
                    }

                    System.Reflection.PropertyInfo property = api.GetProperty(
                        "IsCustomized",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                    object value = property == null ? null : property.GetValue(null, null);
                    if (value is bool && (bool)value)
                    {
                        return true;
                    }
                }
            }
            catch (System.Exception e)
            {
                Log.LogWarning("Could not probe for CustomWarningPage: " + e.Message);
            }

            return false;
        }

        private static void RegisterContent()
        {
            // Probe here rather than in Awake: BepInEx has finished loading the other
            // plugins by now, so PlusLevelLoader is guaranteed to be in the AppDomain.
            // Probing during Awake latched a false negative and silently disabled
            // every .rbpl feature.
            HasPlusLevelLoader = ModPaths.DetectPlusLevelLoader();
            Log.LogInfo("PlusLevelLoader available: " + HasPlusLevelLoader);

            try
            {
                MezzanineMusic.Install(Instance);
                ItemRegistration.Register(Instance);
                NpcRegistration.Register(Instance);
                EventRegistration.Register(Instance);
                RoomRegistration.Register(Instance);
                StudioEditorRegistration.Register(Instance);

                // The Mezzanine scene must exist before the menu callbacks that load it.
                LevelRegistration.Register(Instance);

                ExploreNormalMode.Register(Instance);
                MezzanineMenu.Register(Instance);
                BackToOldSchoolMode.Register(Instance);
                ThinkSticker.Register(Instance);

                // Puts the registered items into each level's room pool and shop stock.
                // Without this they exist but nothing ever places or sells them.
                ItemGeneration.Register(Instance);

                // Registers the achievements with LibraryLib so they show up under Items in
                // the achievements menu. Skipped when LibraryLib is absent.
                AchievementRegistration.Register(Instance);

                MezzanineMusic.Prepare();
                MainMenuRegistration.Register();

                Log.LogInfo("SH20XX.BBPP content registered.");
            }
            catch (System.Exception e)
            {
                Log.LogError("SH20XX.BBPP failed to register its content:\n" + e);
            }
        }

        /// <summary>
        /// Note: room injection is done by <see cref="LevelInjectionPatch"/> via a
        /// Harmony patch on GameInitializer.Initialize. It deliberately does not use
        /// GeneratorManagement.Register, because that API's dispatcher
        /// (GeneratorManagement.Invoke) is never called in this install, so handlers
        /// registered against it silently never fire.
        /// </summary>
        private void OnDestroy()
        {
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }
        }
    }
}
