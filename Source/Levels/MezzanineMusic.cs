using System;
using System.IO;
using MTM101BaldAPI.AssetTools;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Plays the Mezzanine background music.
    ///
    /// Audio\Music\Mezzanine.mid is really an MP3 (ID3 header, "Resting Place" by
    /// Forsaken Sound Team), so MidiPlayerTK can never play it - and it cannot even
    /// be loaded as audio, because AssetLoader.GetAudioType picks the decoder from
    /// the file extension and rejects ".mid" outright.
    ///
    /// The fix is to present the same bytes under a ".mp3" extension: a copy is made
    /// next to the original, loaded as AudioType.MPEG, and played through a dedicated
    /// looping AudioSource with the engine's MIDI music stopped first so the two do
    /// not overlap.
    /// </summary>
    internal sealed class MezzanineMusic : MonoBehaviour
    {
        /// <summary>
        /// Accepted source names, in order. The file has been renamed once, so both
        /// spellings are honoured rather than breaking on a rename.
        /// </summary>
        private static readonly string[] SourceFileNames = { "Mezzanine.mid", "Mezzanine_music.mid" };

        private const string DecodableFileName = "Mezzanine_music.mp3";
        private const float Volume = 0.55f;

        private static MezzanineMusic instance;

        private AudioSource source;
        private AudioClip clip;

        /// <summary>
        /// Creates the singleton host. Called once during content registration.
        /// </summary>
        internal static void Install(Plugin mod)
        {
            if (instance != null)
            {
                return;
            }

            GameObject host = new GameObject("SH20XX_MezzanineMusic");
            host.transform.SetParent(mod.transform, false);
            instance = host.AddComponent<MezzanineMusic>();
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = Volume;
        }

        /// <summary>
        /// Loads the track now rather than lazily at level start, so any decoding
        /// problem shows up in the startup log instead of mid-game.
        /// </summary>
        internal static void Prepare()
        {
            if (instance != null)
            {
                instance.LoadClip();
            }
        }

        private void LoadClip()
        {
            if (clip != null)
            {
                return;
            }

            string sourcePath = null;
            for (int i = 0; i < SourceFileNames.Length && sourcePath == null; i++)
            {
                sourcePath = ModPaths.FindFile(SourceFileNames[i], ModPaths.Audio("Music"), ModPaths.Audio());
            }

            if (sourcePath == null)
            {
                Plugin.Log.LogWarning("Mezzanine music was not found (tried " +
                                      string.Join(", ", SourceFileNames) + ").");
                return;
            }

            string decodablePath = EnsureDecodableCopy(sourcePath);
            if (decodablePath == null)
            {
                return;
            }

            try
            {
                AudioType type = AssetLoader.GetAudioType(decodablePath);
                clip = AssetLoader.AudioClipFromFile(decodablePath, type);

                if (clip == null)
                {
                    Plugin.Log.LogError("Mezzanine music could not be decoded: " + decodablePath);
                    return;
                }

                source.clip = clip;
                Plugin.Log.LogInfo("Loaded Mezzanine music '" + Path.GetFileName(sourcePath) + "' as " + type +
                                   " (" + Mathf.RoundToInt(clip.length) + "s).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not load the Mezzanine music: " + e.Message);
            }
        }

        /// <summary>
        /// Copies the track to a ".mp3" name so the audio loader will decode it.
        /// </summary>
        private static string EnsureDecodableCopy(string sourcePath)
        {
            string target = Path.Combine(Path.GetDirectoryName(sourcePath), DecodableFileName);

            try
            {
                var sourceInfo = new FileInfo(sourcePath);
                var targetInfo = new FileInfo(target);

                if (!targetInfo.Exists || targetInfo.Length != sourceInfo.Length)
                {
                    File.Copy(sourcePath, target, true);
                    Plugin.Log.LogInfo("Created decodable audio copy '" + DecodableFileName +
                                       "' (the source is an MP3 named .mid).");
                }

                return target;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not create the decodable audio copy: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Starts the Mezzanine track on top of a freshly built level.
        /// </summary>
        internal static void Begin()
        {
            if (instance == null)
            {
                return;
            }

            try
            {
                MusicManager music = MusicManager.Instance;
                if (music != null && music.MidiPlaying)
                {
                    music.StopMidi();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not stop the MIDI music: " + e.Message);
            }

            if (instance.source != null && instance.source.clip != null && !instance.source.isPlaying)
            {
                instance.source.Play();
                Plugin.Log.LogInfo("Mezzanine music started.");
            }
        }

        internal static void Stop()
        {
            if (instance != null && instance.source != null && instance.source.isPlaying)
            {
                instance.source.Stop();
            }
        }
    }
}
