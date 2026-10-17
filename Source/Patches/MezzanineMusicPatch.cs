using HarmonyLib;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Starts and stops the Mezzanine background music.
    ///
    /// This patches BaseGameManager.BeginPlay, not EnvironmentController.BeginPlay,
    /// and the difference matters: BaseGameManager.BeginPlay calls
    /// EnvironmentController.BeginPlay from *inside* itself, and
    /// MainGameManager.BeginPlay then calls MusicManager.PlayMidi("school") after
    /// base.BeginPlay() returns. Starting the track any earlier would be overwritten
    /// by the vanilla music. BaseGameManager.BeginPlay is the last thing to run
    /// before the level is actually playable.
    /// </summary>
    [HarmonyPatch(typeof(BaseGameManager), "BeginPlay")]
    internal static class MezzanineMusicPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                CoreGameManager core = CoreGameManager.Instance;
                if (core == null || core.sceneObject == null)
                {
                    return;
                }

                if (core.sceneObject.name == "SH20XX_Mezzanine" ||
                    core.sceneObject.levelTitle == MezzanineLevelBuilder.LevelName)
                {
                    MezzanineMusic.Begin();
                }
                else
                {
                    MezzanineMusic.Stop();
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("Mezzanine music patch failed: " + e);
            }
        }
    }
}
