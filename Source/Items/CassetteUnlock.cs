using System;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// The 99 Cassettes unlock, which gates the "Back to the old school" mode.
    ///
    /// The flow the mode is built around:
    ///
    ///   1. 99 Cassettes spawns only in a faculty room on the fifth floor of the
    ///      Schoolhouse, exactly once per run.
    ///   2. Picking it up does not use an inventory slot - the pickup itself is the
    ///      event. It records the unlock and then quits the game.
    ///   3. On the next launch the mode is available in the mode list.
    ///
    /// The flag lives in PlayerPrefs rather than the save file on purpose: the run is
    /// abandoned by quitting, so there is no save to write to at that moment, and
    /// PlayerPrefs is flushed immediately.
    /// </summary>
    internal static class CassetteUnlock
    {
        /// <summary>PlayerPrefs key holding the unlocked state.</summary>
        private const string UnlockKey = "SH20XX_99CassettesUnlocked";

        /// <summary>Floor the cassette appears on.</summary>
        internal const int CassetteFloor = 5;

        private static bool? cached;

        /// <summary>True once 99 Cassettes has been collected at least once.</summary>
        internal static bool IsUnlocked
        {
            get
            {
                if (!cached.HasValue)
                {
                    bool value = false;
                    try
                    {
                        value = PlayerPrefs.GetInt(UnlockKey, 0) == 1;
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning("Could not read the cassette unlock flag: " + e.Message);
                    }

                    cached = value;
                }

                return cached.Value;
            }
        }

        /// <summary>
        /// Records the unlock and quits, which is what picking the cassette up does.
        /// </summary>
        internal static void UnlockAndQuit()
        {
            try
            {
                PlayerPrefs.SetInt(UnlockKey, 1);
                PlayerPrefs.Save();
                cached = true;
                Plugin.Log.LogInfo("99 Cassettes collected: 'Back to the old school' unlocked. Quitting.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not record the cassette unlock: " + e);
            }

            try
            {
                Application.Quit();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not quit after the cassette unlock: " + e);
            }
        }
    }
}
