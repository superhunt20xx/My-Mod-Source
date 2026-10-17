using System;
using System.Reflection;
using HarmonyLib;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Makes collecting 99 Cassettes unlock the "Back to the old school" mode and quit
    /// the game.
    ///
    /// Pickup.Collect is the right hook because it is the single place that decides
    /// between an inventory item and an instant-use one:
    ///
    ///   if (item.addToInventory) { itm.AddItem(...) } else { item.Use(pm) }
    ///
    /// The cassette is built with addToInventory = false, so picking it up runs Use and
    /// never takes a slot. This postfix then records the unlock and quits, which is the
    /// behaviour the mode is designed around.
    /// </summary>
    internal static class CassettePickupPatch
    {
        internal static void TryPatch(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(typeof(Pickup), "Collect");
            if (target == null)
            {
                Plugin.Log.LogWarning("Cassette patch: Pickup.Collect was not found.");
                return;
            }

            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(CassettePickupPatch), "CollectPostfix")));
                Plugin.Log.LogInfo("Cassette patch applied to Pickup.Collect.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Cassette patch: could not patch Pickup.Collect: " + e.Message);
            }
        }

        private static void CollectPostfix(Pickup __instance)
        {
            if (__instance == null || __instance.item == null)
            {
                return;
            }

            if (!IsCassette(__instance.item))
            {
                return;
            }

            CassetteUnlock.UnlockAndQuit();
        }

        /// <summary>
        /// Matches on the registered enum name, which is how the cassette was created
        /// through ItemBuilder.SetEnum(reference).
        /// </summary>
        private static bool IsCassette(ItemObject item)
        {
            try
            {
                return item.itemType.ToString() == ItemRegistration.CassetteReference;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not identify the picked-up item: " + e.Message);
                return false;
            }
        }
    }
}
