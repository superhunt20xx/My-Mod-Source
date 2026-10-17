using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Attaches BaldiThink to Baldi when a level starts, which is what gives him the
    /// 56% chance to zone out for 10 seconds.
    ///
    /// Baldi extends NPC, whose public spriteRenderer array the behaviour drives. The
    /// patch hooks NPC.Initialize rather than Baldi.Initialize so it also covers the
    /// chase-broken and tutorial variants, which are separate state machines on the same
    /// component.
    /// </summary>
    internal static class BaldiThinkPatch
    {
        /// <summary>Guards against adding a second behaviour to the same NPC.</summary>
        private static void Attach(NPC npc)
        {
            if (npc == null)
            {
                return;
            }

            // Only Baldi zones out; other NPCs are left alone.
            if (!(npc is Baldi))
            {
                return;
            }

            try
            {
                if (npc.GetComponent<BaldiThink>() != null)
                {
                    return;
                }

                BaldiThink think = npc.gameObject.AddComponent<BaldiThink>();
                think.Initialize(npc, ThinkSticker.Frames);

                Plugin.Log.LogInfo("BaldiThink attached to '" + npc.name + "'.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("BaldiThink: could not attach: " + e.Message);
            }
        }

        internal static void TryPatch(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(typeof(NPC), "Initialize");
            if (target == null)
            {
                Plugin.Log.LogWarning("BaldiThink: NPC.Initialize was not found.");
                return;
            }

            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(BaldiThinkPatch), "InitializePostfix")));
                Plugin.Log.LogInfo("BaldiThink patch applied to NPC.Initialize.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("BaldiThink: could not patch NPC.Initialize: " + e.Message);
            }
        }

        private static void InitializePostfix(NPC __instance)
        {
            Attach(__instance);
        }
    }
}
