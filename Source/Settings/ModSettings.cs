using System;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// The mod's own gameplay settings, shown on the mod's options page.
    ///
    /// Stored in PlayerPrefs rather than PlayerFileManager: that class is the game's own
    /// save shape and adding fields to it is not something the public API covers, while
    /// PlayerPrefs is the usual place for a mod's own flags and survives restarts.
    /// </summary>
    internal static class ModSettings
    {
        private const string BaldiKey = "SH20XX_AllowBaldi";
        private const string NpcsKey = "SH20XX_AllowNpcs";

        /// <summary>Whether Baldi may appear in a normal run. Defaults to on.</summary>
        internal static bool AllowBaldi
        {
            get { return PlayerPrefs.GetInt(BaldiKey, 1) != 0; }
            set { PlayerPrefs.SetInt(BaldiKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Whether other NPCs may spawn. Defaults to on.</summary>
        internal static bool AllowNpcs
        {
            get { return PlayerPrefs.GetInt(NpcsKey, 1) != 0; }
            set { PlayerPrefs.SetInt(NpcsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>
        /// Whether a scene is part of a normal run rather than endless, a field trip or the
        /// editor. Only normal runs are affected by these settings.
        ///
        /// levelNo is 1..5 for the school floors of a normal run, and endless levels reuse
        /// small numbers, so the scene name is checked as well.
        /// </summary>
        internal static bool IsNormalRun()
        {
            try
            {
                CoreGameManager manager = CoreGameManager.Instance;
                if (manager == null || manager.sceneObject == null)
                {
                    return false;
                }

                SceneObject scene = manager.sceneObject;

                // MainLevel_1 .. MainLevel_5 are the school floors of a normal run.
                if (!string.IsNullOrEmpty(scene.name) &&
                    scene.name.StartsWith("MainLevel_", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Mod settings: could not tell whether this is a normal run: " + e.Message);
                return false;
            }
        }
    }
}
