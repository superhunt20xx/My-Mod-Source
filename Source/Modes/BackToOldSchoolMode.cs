using System;
using ModdedModesAPI.ModesAPI;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// The "Back to the old school" mode, unlocked by collecting 99 Cassettes.
    ///
    /// The unlock is checked when the mode list is built, not when the button is
    /// pressed, so a player who has not found the cassette never sees the mode at all.
    /// CassettePickupPatch sets the flag; see CassetteUnlock for why it lives in
    /// PlayerPrefs.
    /// </summary>
    internal static class BackToOldSchoolMode
    {
        private const string ScreenKey = "SH20XX_OldSchoolScreen";
        private const string LabelKey = "SH20XX_Mode_OldSchool";
        private const string DescKey = "SH20XX_Mode_OldSchool_Desc";
        private const int Lives = 3;

        private static bool registered;
        private static bool built;

        internal static void Register(Plugin mod)
        {
            if (registered || mod == null)
            {
                return;
            }

            if (!Available())
            {
                Plugin.Log.LogInfo("ModdedModesAPI was not detected; 'Back to the old school' is skipped.");
                return;
            }

            registered = true;

            try
            {
                CustomModesHandler.OnMainMenuInitialize += Build;
                Plugin.Log.LogInfo("Registered the 'Back to the old school' mode callback.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not register 'Back to the old school': " + e);
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

        private static void Build()
        {
            if (built)
            {
                return;
            }

            built = true;

            // The gate: no cassette, no mode.
            if (!CassetteUnlock.IsUnlocked)
            {
                Plugin.Log.LogInfo("'Back to the old school' is still locked (99 Cassettes not collected).");
                return;
            }

            try
            {
                SceneObject scene = MezzanineLevelBuilder.Scene;
                if (scene == null)
                {
                    Plugin.Log.LogError("'Back to the old school': no scene is available to load.");
                    return;
                }

                ModeObject screen = ModeObject.CreateModeObjectOverExistingScreen(SelectionScreen.MainScreen);
                if (screen == null)
                {
                    Plugin.Log.LogError("'Back to the old school': CreateModeObjectOverExistingScreen returned null.");
                    return;
                }

                screen.ScreenTransform.gameObject.name = ScreenKey;

                ButtonBuilder builder = screen.StandardButtonBuilder;

                SeedInput seedInput = null;
                try
                {
                    builder.CreateSeedInput(out seedInput);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("'Back to the old school': could not create the seed input: " + e.Message);
                }

                StandardMenuButton button = builder.CreateModeButton(scene, false, Lives, Mode.Main, null);
                builder.AddDescriptionText(button, DescKey);

                Plugin.Log.LogInfo("'Back to the old school' mode built (unlocked).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not build 'Back to the old school': " + e);
            }
        }
    }
}
