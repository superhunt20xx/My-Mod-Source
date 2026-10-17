using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Re-applies the owning room's door materials once a door exists.
    ///
    /// The game reads the materials like this in StandardDoor.Start:
    ///   overlayShut[0/1] = aTile/bTile.room.doorMats.shut
    ///   overlayOpen[0/1] = aTile/bTile.room.doorMats.open
    /// then calls UpdateTextures(). StandardDoor::Start is the only writer of those
    /// arrays in the whole game (MTM101's ApplyDoorMaterials writes them too but has
    /// no callers anywhere), so re-applying after it is enough.
    ///
    /// StandardDoor.Start turned out to be an unreliable hook: it is private, and a
    /// patch on it never fired in practice (no log output at all). Door.Initialize is
    /// the reliable seam - it is public and virtual, and
    /// LevelBuilder.BuildDoorPlacement calls it explicitly right after SetupDoor.
    /// DoorMaterialPatch.TryPatch also keeps the Start patch as a second attempt, so
    /// whichever runs last wins with the correct materials.
    ///
    /// The mask is deliberately untouched: the game never assigns it either, so it
    /// correctly stays the prefab's.
    /// </summary>
    internal static class DoorMaterialPatch
    {
        private const int LogLimit = 20;

        private static int fired;
        private static int patched;

        /// <summary>
        /// Applies the patches and reports exactly which ones took.
        /// </summary>
        internal static void TryPatch(Harmony harmony)
        {
            Patch(harmony, AccessTools.Method(typeof(Door), "Initialize"), "Door.Initialize");
            Patch(harmony, AccessTools.Method(typeof(StandardDoor), "Start"), "StandardDoor.Start");
        }

        private static void Patch(Harmony harmony, MethodInfo target, string label)
        {
            if (target == null)
            {
                Plugin.Log.LogWarning("Door material patch: " + label + " was not found.");
                return;
            }

            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(DoorMaterialPatch), "Postfix")));
                Plugin.Log.LogInfo("Door material patch applied to " + label + ".");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Door material patch could not patch " + label + ": " + e.Message);
            }
        }

        [HarmonyPostfix]
        private static void Postfix(Door __instance)
        {
            StandardDoor door = __instance as StandardDoor;
            if (door == null)
            {
                return;
            }

            fired++;

            try
            {
                bool applied = Apply(door);
                if (applied)
                {
                    patched++;
                }

                if (fired <= LogLimit)
                {
                    Plugin.Log.LogInfo("Door patch #" + fired + ": " + Describe(door) +
                                       (applied ? " -> materials applied" : " -> nothing to apply"));
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("Could not apply room door materials: " + e);
            }
        }

        private static string Describe(StandardDoor door)
        {
            Cell aTile = door.aTile;
            Cell bTile = door.bTile;

            string a = aTile == null ? "aTile=null"
                : "a.mats=" + (aTile.room == null || aTile.room.doorMats == null ? "null" : aTile.room.doorMats.name);

            string b = bTile == null ? "bTile=null"
                : "b.mats=" + (bTile.room == null || bTile.room.doorMats == null ? "null" : bTile.room.doorMats.name);

            return a + " | " + b;
        }

        internal static bool Apply(StandardDoor door)
        {
            Cell aTile = door.aTile;
            Cell bTile = door.bTile;

            StandardDoorMats aMats = aTile == null || aTile.room == null ? null : aTile.room.doorMats;
            StandardDoorMats bMats = bTile == null || bTile.room == null ? null : bTile.room.doorMats;

            bool changed = false;

            if (door.overlayShut != null && door.overlayShut.Length >= 2)
            {
                if (aMats != null && aMats.shut != null)
                {
                    door.overlayShut[1] = aMats.shut;
                    changed = true;
                }

                if (bMats != null && bMats.shut != null)
                {
                    door.overlayShut[0] = bMats.shut;
                    changed = true;
                }
            }

            if (door.overlayOpen != null && door.overlayOpen.Length >= 2)
            {
                if (aMats != null && aMats.open != null)
                {
                    door.overlayOpen[1] = aMats.open;
                    changed = true;
                }

                if (bMats != null && bMats.open != null)
                {
                    door.overlayOpen[0] = bMats.open;
                    changed = true;
                }
            }

            if (changed)
            {
                door.UpdateTextures();
            }

            return changed;
        }

        internal static string Summary()
        {
            return "door patch fired " + fired + " time(s), materials applied on " + patched + ".";
        }
    }
}
