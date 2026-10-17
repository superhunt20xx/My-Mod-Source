using System;
using HarmonyLib;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Applies the mod's spawn settings to a normal run.
    ///
    /// Two separate places decide what appears in a level:
    ///
    ///   LevelBuilder.AddNpcsFromPreviousLevels : builds the NPC list for the floor,
    ///                                            including the scene's forcedNpcs
    ///                                            (which is where Baldi comes from)
    ///   EnvironmentController.SpawnNPC         : actually instantiates one NPC
    ///
    /// Filtering at both points means a setting holds whether an NPC arrives through the
    /// forced list or through the random pool. Both patches are no-ops outside a normal
    /// run, so endless, field trips and the editor are untouched.
    /// </summary>
    internal static class NpcSpawnFilter
    {
        internal static void TryPatch(Harmony harmony)
        {
            try
            {
                var build = AccessTools.Method(typeof(LevelBuilder), "AddNpcsFromPreviousLevels");
                if (build != null)
                {
                    harmony.Patch(build, postfix: new HarmonyMethod(
                        AccessTools.Method(typeof(NpcSpawnFilter), "AddNpcsPostfix")));
                    Plugin.Log.LogInfo("NPC filter: AddNpcsFromPreviousLevels patched.");
                }
                else
                {
                    Plugin.Log.LogWarning("NPC filter: AddNpcsFromPreviousLevels was not found.");
                }

                var spawn = AccessTools.Method(typeof(EnvironmentController), "SpawnNPC",
                    new[] { typeof(NPC), typeof(IntVector2) });
                if (spawn != null)
                {
                    harmony.Patch(spawn, prefix: new HarmonyMethod(
                        AccessTools.Method(typeof(NpcSpawnFilter), "SpawnNpcPrefix")));
                    Plugin.Log.LogInfo("NPC filter: EnvironmentController.SpawnNPC patched.");
                }
                else
                {
                    Plugin.Log.LogWarning("NPC filter: EnvironmentController.SpawnNPC was not found.");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("NPC filter: could not patch the spawn points: " + e);
            }
        }

        /// <summary>
        /// Removes the NPCs the settings forbid from the list the level just built.
        /// Runs after the vanilla method so the list is already complete.
        /// </summary>
        private static void AddNpcsPostfix(LevelBuilder __instance)
        {
            if (!ModSettings.IsNormalRun())
            {
                return;
            }

            if (ModSettings.AllowBaldi && ModSettings.AllowNpcs)
            {
                return;
            }

            try
            {
                // The list the builder populated lives on the environment controller.
                var field = AccessTools.Field(typeof(LevelBuilder), "ec");
                EnvironmentController ec = field == null ? null : field.GetValue(__instance) as EnvironmentController;
                if (ec == null)
                {
                    return;
                }

                var npcsField = AccessTools.Field(typeof(EnvironmentController), "npcs");
                var list = npcsField == null ? null : npcsField.GetValue(ec) as System.Collections.Generic.List<NPC>;
                if (list == null)
                {
                    return;
                }

                int removed = list.RemoveAll(IsForbidden);
                if (removed > 0)
                {
                    Plugin.Log.LogInfo("NPC filter: removed " + removed +
                                       " NPC(s) from the floor (Baldi=" + ModSettings.AllowBaldi +
                                       ", others=" + ModSettings.AllowNpcs + ").");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("NPC filter: could not filter the NPC list: " + e.Message);
            }
        }

        private static bool IsForbidden(NPC npc)
        {
            if (npc == null)
            {
                return false;
            }

            bool isBaldi = npc is Baldi;
            if (isBaldi)
            {
                return !ModSettings.AllowBaldi;
            }

            return !ModSettings.AllowNpcs;
        }

        /// <summary>
        /// Blocks the spawn of anything the settings forbid. Returning false skips the
        /// original method entirely, which is the only way to stop a spawn here since it
        /// has no failure return of its own.
        /// </summary>
        private static bool SpawnNpcPrefix(NPC npc)
        {
            if (!ModSettings.IsNormalRun())
            {
                return true;
            }

            if (npc is Baldi && !ModSettings.AllowBaldi)
            {
                Plugin.Log.LogInfo("NPC filter: blocked a Baldi spawn (disabled in settings).");
                return false;
            }

            if (!(npc is Baldi) && !ModSettings.AllowNpcs)
            {
                return false;
            }

            return true;
        }
    }
}
