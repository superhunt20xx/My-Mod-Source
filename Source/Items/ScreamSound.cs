using System;
using System.IO;
using MTM101BaldAPI.AssetTools;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// The screaming-chicken sound shared by the four toy items. Using one of them
    /// plays the clip and makes a noise where the player stands, which is what pulls
    /// Baldi and the other characters toward them.
    ///
    /// The clip is loaded once and reused, because building a SoundObject per use would
    /// re-read a 90 KB file every time.
    /// </summary>
    internal static class ScreamSound
    {
        private const string FileName = "chicken_scream.mp3";

        /// <summary>Noise radius fed to the level. Well past Baldi's own hearing range.</summary>
        internal const int NoiseValue = 110;

        private const float Volume = 1f;

        private static SoundObject sound;
        private static bool attempted;

        /// <summary>
        /// Plays the scream at the given position and makes the noise that attracts
        /// characters. Returns false when the clip could not be loaded.
        /// </summary>
        internal static bool Play(PlayerManager player)
        {
            if (player == null || player.ec == null)
            {
                return false;
            }

            SoundObject scream = Load();
            if (scream == null)
            {
                return false;
            }

            try
            {
                AudioManager audio = AudioOf(player.ec);
                if (audio != null)
                {
                    audio.PlaySingle(scream);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not play the scream: " + e.Message);
            }

            // The sound alone would be cosmetic; this is what actually draws attention.
            try
            {
                player.ec.MakeNoise(player.transform.position, NoiseValue, false);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not emit noise for the scream: " + e.Message);
            }

            return true;
        }

        /// <summary>
        /// Reads EnvironmentController.audMan, which is internal to Assembly-CSharp.
        /// </summary>
        private static AudioManager AudioOf(EnvironmentController ec)
        {
            if (ec == null)
            {
                return null;
            }

            var field = HarmonyLib.AccessTools.Field(typeof(EnvironmentController), "audMan");
            return field == null ? null : field.GetValue(ec) as AudioManager;
        }

        private static SoundObject Load()
        {
            if (attempted)
            {
                return sound;
            }

            attempted = true;

            try
            {
                string path = ModPaths.FindFile(FileName, ModPaths.Audio("Sounds"), ModPaths.Audio());
                if (path == null)
                {
                    Plugin.Log.LogError("The scream sound '" + FileName + "' was not found.");
                    return null;
                }

                AudioType type = AssetLoader.GetAudioType(path);
                AudioClip clip = AssetLoader.AudioClipFromFile(path, type);
                if (clip == null)
                {
                    Plugin.Log.LogError("Could not decode the scream sound at " + path + ".");
                    return null;
                }

                sound = ScriptableObject.CreateInstance<SoundObject>();
                sound.name = "SH20XX_ChickenScream";
                sound.soundClip = clip;
                sound.soundKey = "SH20XX_ChickenScream";
                sound.volumeMultiplier = Volume;
                sound.subtitle = false;
                sound.color = Color.white;

                Plugin.Log.LogInfo("Loaded the scream sound ('" + Path.GetFileName(path) + "', " + type + ").");
                return sound;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not load the scream sound: " + e);
                return null;
            }
        }
    }
}
