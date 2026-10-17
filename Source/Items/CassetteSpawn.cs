using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Places 99 Cassettes in a faculty room on the fifth floor of the Schoolhouse,
    /// exactly once per run.
    ///
    /// The pieces this relies on, all confirmed against Assembly-CSharp:
    ///
    ///   RoomController.category : RoomCategory   - public, Faculty = 4
    ///   RoomController.ec                        - the level's EnvironmentController
    ///   RoomController.RandomEntitySafeCellNoGarbage()  - a guaranteed-valid cell
    ///   EnvironmentController.CreateItem(room, item, pos)  - instantiates a Pickup
    ///
    /// It runs after the level has been built, because rooms only exist by then, and it
    /// is keyed on the floor number so the cassette appears on floor 5 and nowhere else.
    ///
    /// The cassette's generation json weights are all 0, so this is the only way it can
    /// ever reach a level - it is deliberately not part of any random pool.
    /// </summary>
    internal static class CassetteSpawn
    {
        private static bool spawnedThisRun;

        internal static void TryPatch(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(typeof(BaseGameManager), "BeginPlay");
            if (target == null)
            {
                Plugin.Log.LogWarning("Cassette spawn: BaseGameManager.BeginPlay was not found.");
                return;
            }

            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(CassetteSpawn), "BeginPlayPostfix")));
                Plugin.Log.LogInfo("Cassette spawn patch applied to BaseGameManager.BeginPlay.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Cassette spawn: could not patch BeginPlay: " + e.Message);
            }
        }

        private static void BeginPlayPostfix()
        {
            if (spawnedThisRun)
            {
                return;
            }

            CoreGameManager manager = CoreGameManager.Instance;
            if (manager == null || manager.sceneObject == null)
            {
                return;
            }

            SceneObject scene = manager.sceneObject;
            if (scene.levelNo != CassetteUnlock.CassetteFloor)
            {
                return;
            }

            ItemObject cassette;
            if (!ItemRegistration.Items.TryGetValue(ItemRegistration.CassetteReference, out cassette) || cassette == null)
            {
                Plugin.Log.LogWarning("Cassette spawn: the 99 Cassettes item is not registered.");
                return;
            }

            try
            {
                RoomController room = FindFacultyRoom();
                if (room == null)
                {
                    Plugin.Log.LogWarning("Cassette spawn: floor " + scene.levelNo +
                                          " has no Faculty room, so the cassette was not placed.");
                    return;
                }

                Cell cell = room.RandomEntitySafeCellNoGarbage();
                if (cell == null)
                {
                    Plugin.Log.LogWarning("Cassette spawn: the Faculty room has no safe cell.");
                    return;
                }

                Pickup pickup = room.ec.CreateItem(room, cassette, new Vector2(cell.position.x, cell.position.z));
                if (pickup != null)
                {
                    spawnedThisRun = true;
                    Plugin.Log.LogInfo("99 Cassettes placed in the Faculty room on floor " + scene.levelNo +
                                       " at " + cell.position.x + "," + cell.position.z + ".");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Cassette spawn failed: " + e);
            }
        }

        /// <summary>
        /// Finds the first room tagged Faculty. Rooms are located through the scene
        /// rather than a level-wide list, and RoomController exists for every room.
        /// </summary>
        private static RoomController FindFacultyRoom()
        {
            RoomController[] rooms = UnityEngine.Object.FindObjectsOfType<RoomController>();
            if (rooms == null)
            {
                return null;
            }

            for (int i = 0; i < rooms.Length; i++)
            {
                RoomController room = rooms[i];
                if (room == null || room.ec == null)
                {
                    continue;
                }

                if (room.category == RoomCategory.Faculty)
                {
                    return room;
                }
            }

            return null;
        }
    }
}
