using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Swaps in the Mezzanine floor where it belongs and pushes the SH20XX rooms
    /// into the level about to be generated.
    ///
    /// History, so this does not get re-litigated:
    ///   * GeneratorManagement.Register does nothing here. Its dispatcher
    ///     (GeneratorManagement.Invoke) is not called by any assembly in this
    ///     install, so handlers registered against it never fire.
    ///   * A Prefix on GameInitializer.Initialize is too early - that method is the
    ///     only thing that assigns the private sceneObject field, so the field is
    ///     still null when a Prefix runs.
    ///
    /// LevelBuilder.StartGenerate is used instead: LevelLoader.StartGenerate and
    /// LevelGenerator.StartGenerate both chain to it, so it is guaranteed to run for
    /// every build, and LevelBuilder.ld is a public LevelGenerationParameters holding
    /// the assigned generation data.
    /// </summary>
    [HarmonyPatch(typeof(LevelBuilder), "StartGenerate")]
    internal static class LevelInjectionPatch
    {
        /// <summary>
        /// Mezzanine replaces the third floor, matching the "between F3 and F4"
        /// entry in Data/Levels/Mezzanine/Mezzanine_Room_01.json.
        /// </summary>
        private const int MezzanineFloor = 3;

        private const string SchoolPrefix = "Schoolhouse_Lvl";

        /// <summary>
        /// LevelBuilder.ec is protected, so it is read through reflection.
        /// </summary>
        private static readonly FieldInfo EcField = AccessTools.Field(typeof(LevelBuilder), "ec");

        private static int injections;
        private static int swaps;

        [HarmonyPostfix]
        private static void Postfix(LevelBuilder __instance)
        {
            if (__instance == null || __instance.scene == null)
            {
                Plugin.Log.LogWarning("LevelBuilder.StartGenerate fired without a scene; skipping.");
                return;
            }

            string sceneName = __instance.scene.name;
            EnvironmentController ec = EcField == null ? null : EcField.GetValue(__instance) as EnvironmentController;

            Plugin.Log.LogInfo("StartGenerate hook: scene='" + sceneName +
                               "' ld=" + (__instance.ld == null ? "null" : "set") +
                               " ec=" + (ec == null ? "null" : "set"));

            // The editor builds levels too; only touch real gameplay.
            if (ec != null)
            {
                return;
            }

            try
            {
                LevelObject source = SwapIfMezzanine(__instance, sceneName);

                if (RoomBuilder.InjectIntoLevelBuilder(__instance, source))
                {
                    injections++;
                    Plugin.Log.LogInfo("Rooms injected into '" + sceneName + "' (call " + injections + ").");
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("Level injection failed for '" + sceneName + "': " + e);
            }
        }

        /// <summary>
        /// Replaces the generation data with the Mezzanine level when this build is
        /// the Mezzanine floor. Returns the LevelObject the caller should inject rooms
        /// into, or null when no room injection should happen.
        /// </summary>
        private static LevelObject SwapIfMezzanine(LevelBuilder builder, string sceneName)
        {
            if (MezzanineLevelBuilder.Level == null || sceneName == null || !sceneName.StartsWith(SchoolPrefix))
            {
                return null;
            }

            int floor;
            if (!int.TryParse(sceneName.Substring(SchoolPrefix.Length), out floor) || floor != MezzanineFloor)
            {
                return null;
            }

            builder.ld = new LevelGenerationParameters();
            builder.ld.AssignData(MezzanineLevelBuilder.Level, null);

            swaps++;
            Plugin.Log.LogInfo("Mezzanine floor swapped in (swap " + swaps + ").");

            return MezzanineLevelBuilder.Level;
        }
    }
}
