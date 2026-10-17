using System;
using ModdedModesAPI.ModesAPI;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Adds a "Mezzanine" entrance button to the main menu, next to Baldi.
    ///
    /// It is a normal mode button, so it goes through the same path Explore Normal
    /// uses: ModdedModesAPI's CreateModeButton handler runs CheckSeed(), Initialize,
    /// LoadLevel and SetMode. The only difference is what it loads - the Mezzanine
    /// SceneObject built by MezzanineLevelBuilder, which is a vanilla level layout
    /// re-skinned with the textures in Textures/Level/Mezzanine.
    ///
    /// Positioning: MainMenu has no Baldi field (he is plain scene content), so the
    /// button is placed by anchoring to the screen rect rather than to an object.
    /// OffsetFromBaldi is in ModdedModesAPI screen units and is the knob to turn if
    /// it needs to sit somewhere slightly different.
    /// </summary>
    internal static class MezzanineMenu
    {
        private const string ScreenKey = "SH20XX_MezzanineScreen";
        private const string LabelKey = "SH20XX_Mode_Mezzanine";
        private const string DescKey = "SH20XX_Mode_Mezzanine_Desc";
        private const string LabelFallback = "Mezzanine";
        private const string SeedLabelKey = "SH20XX_Mode_Seed";
        private const string SeedLabelFallback = "Seed";

        /// <summary>
        /// Button offset from the centre of the main screen. Baldi sits to the left of
        /// the title, so this leans right and down to land beside him.
        /// </summary>
        private static readonly Vector2 OffsetFromBaldi = new Vector2(-260f, -60f);

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
                Plugin.Log.LogInfo("ModdedModesAPI was not detected; the Mezzanine menu button is skipped.");
                return;
            }

            registered = true;

            try
            {
                CustomModesHandler.OnMainMenuInitialize += Build;
                Plugin.Log.LogInfo("Registered the Mezzanine menu button callback.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not register the Mezzanine menu button: " + e);
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

            try
            {
                SceneObject scene = MezzanineLevelBuilder.Scene;
                if (scene == null)
                {
                    Plugin.Log.LogError(
                        "Mezzanine: the level scene was not built yet, so the menu button is skipped. " +
                        "Move MezzanineMenu.Register after LevelRegistration.Register.");
                    return;
                }

                ModeObject screen = ModeObject.CreateModeObjectOverExistingScreen(SelectionScreen.MainScreen);
                if (screen == null)
                {
                    Plugin.Log.LogError("Mezzanine: CreateModeObjectOverExistingScreen returned null.");
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
                    Plugin.Log.LogWarning("Mezzanine: could not create the seed input: " + e.Message);
                }

                StandardMenuButton button = builder.CreateModeButton(scene, false, Lives, Mode.Main, null);
                if (button != null)
                {
                    button.transform.localPosition = new Vector3(
                        OffsetFromBaldi.x, OffsetFromBaldi.y, button.transform.localPosition.z);
                }

                builder.AddDescriptionText(button, DescKey);

                Plugin.Log.LogInfo("Mezzanine entrance built on scene '" + scene.name +
                                   "' at offset " + OffsetFromBaldi + ".");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not build the Mezzanine entrance: " + e);
            }
        }
    }
}
