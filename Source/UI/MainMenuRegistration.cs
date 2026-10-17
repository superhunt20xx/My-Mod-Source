using System;
using System.Collections.Generic;
using System.IO;
using CustomMainMenusAPI;
using MTM101BaldAPI.AssetTools;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Registers the SH20XX title screen (background image + main menu music).
    ///
    /// Main menu music needs care. CustomMainMenusAPI writes the track string
    /// straight into MusicPlayer.track, which MusicManager.PlayMidi forwards to
    /// MidiPlayerTK as MPTK_MidiName. MidiPlayerTK resolves that against its own
    /// midi list, so it must be a bare track id like "school" or "Elevator" - a
    /// file path never resolves and the player silently falls back to the first
    /// entry in its list (which is what "wrong music" sounds like).
    ///
    /// MTM101BaldAPI handles this: AssetLoader.MidiFromFile(path, id) registers the
    /// file under "custom_&lt;id&gt;" and returns that key, and its MusicPrefix patch
    /// plays it. So the returned key is what has to be passed along.
    /// </summary>
    internal static class MainMenuRegistration
    {
        internal const string MenuName = "BBPP";

        /// <summary>
        /// A Standard MIDI File starts with the chunk type "MThd".
        /// </summary>
        private static readonly byte[] MidiHeader = { 0x4D, 0x54, 0x68, 0x64 };

        internal static void Register()
        {
            string imagePath = ModPaths.FindFile(
                "BBPP_Mainmenu.png",
                ModPaths.Textures("UI"),
                ModPaths.Textures());

            if (imagePath == null)
            {
                Plugin.Log.LogWarning("Main menu image was not found; the custom menu is skipped.");
                return;
            }

            Sprite menuSprite = AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(imagePath), 1f);
            if (menuSprite == null)
            {
                Plugin.Log.LogWarning("Main menu image could not be read; the custom menu is skipped.");
                return;
            }

            string midiKey = RegisterMidi("Main", "Main.mid");

            try
            {
                if (string.IsNullOrEmpty(midiKey))
                {
                    MainMenuObject.CreateMenuObject(MenuName, menuSprite);
                    Plugin.Log.LogWarning("Registered main menu '" + MenuName + "' without custom music.");
                }
                else
                {
                    MainMenuObject.CreateMenuObject(MenuName, menuSprite, midiKey);
                    Plugin.Log.LogInfo("Registered main menu '" + MenuName + "' with midi key '" + midiKey + "'.");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not register the main menu: " + e.Message);
            }
        }

        /// <summary>
        /// Loads a .mid from Audio/Music, registers it with MidiPlayerTK through
        /// MTM101BaldAPI and returns the key to hand to PlayMidi.
        /// </summary>
        internal static string RegisterMidi(string id, string fileName)
        {
            string path = ModPaths.FindFile(fileName, ModPaths.Audio("Music"), ModPaths.Audio());
            if (path == null)
            {
                Plugin.Log.LogWarning("MIDI file '" + fileName + "' was not found.");
                return null;
            }

            if (!LooksLikeMidi(path))
            {
                // Main.mid is really an MP3 (ID3 header), which MidiPlayerTK cannot
                // load. It is presented under a .mp3 name instead and played as a clip.
                return RegisterAudioFallback(id, path);
            }

            try
            {
                string key = AssetLoader.MidiFromFile(path, id);
                Plugin.Log.LogInfo("Registered MIDI '" + fileName + "' as '" + key + "'.");
                return key;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not register MIDI '" + fileName + "': " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Loads a non-MIDI music file (an MP3 named .mid) as an AudioClip and loops
        /// it. Returns null because there is no MIDI key to hand to PlayMidi.
        /// </summary>
        private static string RegisterAudioFallback(string id, string path)
        {
            try
            {
                string decodable = Path.Combine(Path.GetDirectoryName(path), id + "_music.mp3");
                var source = new FileInfo(path);
                var target = new FileInfo(decodable);

                if (!target.Exists || target.Length != source.Length)
                {
                    File.Copy(path, decodable, true);
                }

                AudioType type = AssetLoader.GetAudioType(decodable);
                AudioClip clip = AssetLoader.AudioClipFromFile(decodable, type);

                if (clip == null)
                {
                    Plugin.Log.LogError("Could not decode menu music: " + path);
                    return null;
                }

                GameObject host = new GameObject("SH20XX_MenuMusic");
                UnityEngine.Object.DontDestroyOnLoad(host);

                AudioSource audio = host.AddComponent<AudioSource>();
                audio.clip = clip;
                audio.loop = true;
                audio.playOnAwake = true;
                audio.spatialBlend = 0f;
                audio.volume = 0.55f;

                Plugin.Log.LogInfo("Menu music '" + Path.GetFileName(path) + "' loaded as " + type +
                                   " and looping (MidiPlayerTK cannot play it).");
                return null;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not load the menu music: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Guards against files that are named .mid but are really another format
        /// (an MP3 with an ID3 header, for example), which MidiPlayerTK cannot play.
        /// </summary>
        private static bool LooksLikeMidi(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    for (int i = 0; i < MidiHeader.Length; i++)
                    {
                        int b = stream.ReadByte();
                        if (b != MidiHeader[i])
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not inspect '" + path + "': " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// The menu title is pushed through the localization table by ModLocalization,
        /// this helper just exposes the entries that need to exist.
        /// </summary>
        internal static IEnumerable<KeyValuePair<string, string>> LocalizationEntries()
        {
            yield return new KeyValuePair<string, string>("SH20XX_MainMenu_Title", "SH20XX");
            yield return new KeyValuePair<string, string>("SH20XX_MainMenu_Start", "Start");
        }
    }
}
