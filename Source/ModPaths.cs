using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Resolves every path the mod needs, and caches texture / audio / JSON loads.
    /// </summary>
    internal static class ModPaths
    {
        private const string MOD_FOLDER_NAME = "SH20XX.BBPP";

        private static readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, AudioClip> audioCache = new Dictionary<string, AudioClip>();

        internal static BaseUnityPlugin Plugin { get; private set; }

        internal static string Root { get; private set; }

        internal static void Initialize(BaseUnityPlugin plugin)
        {
            Plugin = plugin;
            Root = Path.Combine(Application.streamingAssetsPath, "Modded", MOD_FOLDER_NAME);
        }

        internal static string Data(params string[] parts)
        {
            return Combine(Root, "Data", parts);
        }

        internal static string Textures(params string[] parts)
        {
            return Combine(Root, "Textures", parts);
        }

        internal static string Audio(params string[] parts)
        {
            return Combine(Root, "Audio", parts);
        }

        internal static string Combine(string root, string first, params string[] rest)
        {
            string result = Path.Combine(root, first);
            for (int i = 0; i < rest.Length; i++)
            {
                result = Path.Combine(result, rest[i]);
            }

            return result;
        }

        internal static string Relative(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || string.IsNullOrEmpty(Root))
            {
                return absolutePath;
            }

            string root = Root.Replace('\\', '/').TrimEnd('/') + "/";
            string path = absolutePath.Replace('\\', '/');
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : path;
        }

        internal static bool DetectPlusLevelLoader()
        {
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetType("PlusLevelLoader.PlusLevelLoaderPlugin", false) != null)
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                SH20XX.BBPP.Plugin.Log.LogWarning("Failed to probe for PlusLevelLoader: " + e.Message);
            }

            return false;
        }

        // ------------------------------------------------------------------ textures

        internal static Texture2D LoadTexture(string path)
        {
            // A null/empty path is a "this room has no such texture" case, not an
            // error, so it is not logged.
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (!File.Exists(path))
            {
                SH20XX.BBPP.Plugin.Log.LogWarning("Missing texture: " + path);
                return null;
            }

            Texture2D cached;
            if (textureCache.TryGetValue(path, out cached))
            {
                return cached;
            }

            try
            {
                Texture2D tex = MTM101BaldAPI.AssetTools.AssetLoader.TextureFromFile(path);
                if (tex != null)
                {
                    tex.name = Path.GetFileNameWithoutExtension(path);
                }

                textureCache[path] = tex;
                return tex;
            }
            catch (Exception e)
            {
                SH20XX.BBPP.Plugin.Log.LogError("Failed to load texture '" + path + "': " + e.Message);
                textureCache[path] = null;
                return null;
            }
        }

        internal static Sprite LoadSprite(string path, float pixelsPerUnit)
        {
            Texture2D tex = LoadTexture(path);
            return tex == null ? null : MTM101BaldAPI.AssetTools.AssetLoader.SpriteFromTexture2D(tex, pixelsPerUnit);
        }

        // ------------------------------------------------------------------ audio

        internal static AudioClip LoadAudio(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                SH20XX.BBPP.Plugin.Log.LogWarning("Missing audio clip: " + path);
                return null;
            }

            AudioClip cached;
            if (audioCache.TryGetValue(path, out cached))
            {
                return cached;
            }

            try
            {
                AudioClip clip = MTM101BaldAPI.AssetTools.AssetLoader.AudioClipFromFile(path);
                audioCache[path] = clip;
                return clip;
            }
            catch (Exception e)
            {
                SH20XX.BBPP.Plugin.Log.LogError("Failed to load audio '" + path + "': " + e.Message);
                audioCache[path] = null;
                return null;
            }
        }

        // ------------------------------------------------------------------ lookup

        /// <summary>
        /// Looks a file up in several candidate folders and returns the first hit.
        /// </summary>
        internal static string FindFile(string fileName, params string[] folders)
        {
            foreach (string folder in folders)
            {
                string candidate = Path.Combine(folder, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        internal static IEnumerable<string> EnumerateFiles(string folder, string searchPattern, bool recursive)
        {
            if (!Directory.Exists(folder))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateFiles(folder, searchPattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }
    }
}
