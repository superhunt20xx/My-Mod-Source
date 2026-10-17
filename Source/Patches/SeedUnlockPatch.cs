using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Makes the main menu seed input usable in every mode, including free play.
    ///
    /// Two separate things have to line up, and only the first is a visible gate:
    ///
    /// 1. Visibility. MainMenu.Start does:
    ///      if (PlayerFileManager.Instance.clearedLevels[2]) seedInput.SetActive(true);
    ///    so the field only appears after the third level is cleared. Unlocking it is
    ///    safe rather than hacky because SetActive(true) is idempotent: when the level
    ///    is already cleared the game's own call and this postfix simply agree.
    ///
    /// 2. Consumption. GameLoader.CheckSeed is the only member that reads SeedInput
    ///    and copies it into GameLoader.useSeed / GameLoader.seed, and it has no
    ///    callers at all - verified by IL across Assembly-CSharp and every installed
    ///    plugin, plus a reflection-string search. GameLoader.Initialize is what reads
    ///    those two fields, so without this the field would appear and then be ignored.
    /// </summary>
    internal static class SeedUnlockPatch
    {
        private static readonly FieldInfo MenuSeedInputField =
            AccessTools.Field(typeof(MainMenu), "seedInput");

        private static readonly FieldInfo LoaderSeedInputField =
            AccessTools.Field(typeof(GameLoader), "seedInput");

        private static bool reportedUnlock;
        private static bool reportedApply;

        /// <summary>
        /// Called from Awake. Both targets are patched by hand so that a failure is
        /// reported instead of being swallowed, which is how an earlier door patch was
        /// silently lost.
        /// </summary>
        internal static void TryPatch(Harmony harmony)
        {
            Patch(harmony, AccessTools.Method(typeof(MainMenu), "Start"), "MainMenu.Start", "MenuPostfix", false);
            Patch(harmony, AccessTools.Method(typeof(GameLoader), "Initialize"), "GameLoader.Initialize", "LoaderPrefix", true);
        }

        private static void Patch(Harmony harmony, MethodInfo target, string label, string hookName, bool asPrefix)
        {
            if (target == null)
            {
                Plugin.Log.LogWarning("Seed patch: " + label + " was not found.");
                return;
            }

            MethodInfo hook = AccessTools.Method(typeof(SeedUnlockPatch), hookName);
            if (hook == null)
            {
                Plugin.Log.LogWarning("Seed patch: hook " + hookName + " was not found.");
                return;
            }

            try
            {
                var method = new HarmonyMethod(hook);
                if (asPrefix)
                {
                    harmony.Patch(target, prefix: method);
                }
                else
                {
                    harmony.Patch(target, postfix: method);
                }

                Plugin.Log.LogInfo("Seed patch applied to " + label + " (" +
                                   (asPrefix ? "prefix" : "postfix") + ").");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Seed patch could not patch " + label + ": " + e.Message);
            }
        }

        private static void MenuPostfix(MainMenu __instance)
        {
            if (__instance == null || MenuSeedInputField == null)
            {
                return;
            }

            GameObject seedInput = MenuSeedInputField.GetValue(__instance) as GameObject;
            if (seedInput == null)
            {
                Plugin.Log.LogWarning("MainMenu.seedInput was not found; the seed field stays locked.");
                return;
            }

            bool wasActive = seedInput.activeSelf;
            if (!wasActive)
            {
                seedInput.SetActive(true);
            }

            if (!reportedUnlock)
            {
                reportedUnlock = true;
                Plugin.Log.LogInfo("Seed input unlocked in the main menu (was " +
                                   (wasActive ? "already shown" : "hidden") + ").");
            }
        }

        /// <summary>
        /// Copies the menu's seed into the loader right before it is consumed.
        /// </summary>
        private static void LoaderPrefix(GameLoader __instance)
        {
            if (__instance == null || LoaderSeedInputField == null)
            {
                return;
            }

            try
            {
                object input = LoaderSeedInputField.GetValue(__instance);
                if (input == null)
                {
                    // No seed widget in this flow (for example endless mode sets the
                    // fields itself), so leave the loader's own values alone.
                    return;
                }

                __instance.CheckSeed();

                if (!reportedApply)
                {
                    reportedApply = true;
                    Plugin.Log.LogInfo("Seed consumed from the main menu: useSeed=" +
                                       __instance.useSeed + " seed=" + __instance.seed);
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Could not apply the seed input: " + e.Message);
            }
        }
    }
}
