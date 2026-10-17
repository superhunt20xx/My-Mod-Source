using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Runtime bridge to PlusLevelLoader.
    ///
    /// .rbpl files are PlusLevelLoader's format: it owns the texture alias table,
    /// the room settings table and the prefab alias table that a level needs.
    /// Rebuilding all of that would duplicate the loader, so instead the SH20XX
    /// rooms are pushed into the loader's own tables and the loader then reads the
    /// .rbpl files normally.
    ///
    /// The bridge is reflection based on purpose: the mod keeps compiling (and
    /// loading) even when PlusLevelLoader is missing or updates its internals.
    /// </summary>
    internal static class PlusLevelLoaderBridge
    {
        private const string PluginTypeName = "PlusLevelLoader.PlusLevelLoaderPlugin";
        private const string RoomSettingsTypeName = "PlusLevelLoader.RoomSettings";
        private const string LoaderTypeName = "PlusLevelLoader.CustomLevelLoader";
        private const string FormatLevelTypeName = "PlusLevelFormat.Level";

        private static readonly HashSet<string> knownRoomAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> knownPrefabAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static Type pluginType;
        private static object pluginInstance;
        private static bool resolved;

        /// <summary>
        /// True only once the loader type AND its live plugin instance are both
        /// reachable. Resolution is retried on every call, because a probe that runs
        /// before BepInEx has finished loading the other plugins would otherwise
        /// latch a false negative for the whole session.
        /// </summary>
        internal static bool Available
        {
            get
            {
                Resolve();
                return pluginInstance != null;
            }
        }

        private static void Resolve()
        {
            if (pluginInstance != null)
            {
                return;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type found;
                try
                {
                    found = assembly.GetType(PluginTypeName, false);
                }
                catch (Exception)
                {
                    continue;
                }

                if (found == null)
                {
                    continue;
                }

                pluginType = found;

                PropertyInfo instance = found.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                                        ?? found.GetProperty("Instance", BindingFlags.NonPublic | BindingFlags.Static);

                if (instance != null)
                {
                    try
                    {
                        pluginInstance = instance.GetValue(null, null);
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning("PlusLevelLoader instance could not be read: " + e.Message);
                    }
                }

                if (pluginInstance != null)
                {
                    Plugin.Log.LogInfo("PlusLevelLoader bridge connected.");
                    resolved = true;
                    return;
                }
            }
        }

        private static object GetTable(string fieldName)
        {
            Resolve();
            if (pluginInstance == null || pluginType == null)
            {
                return null;
            }

            FieldInfo field = pluginType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(pluginInstance);
        }

        // ------------------------------------------------------------------ textures

        /// <summary>
        /// Publishes every SH20XX texture under the alias the .rbpl files use
        /// (the file name without its extension).
        /// </summary>
        internal static void PublishTextures(IEnumerable<string> texturePaths)
        {
            IDictionary<string, Texture2D> aliases = GetTable("textureAliases") as IDictionary<string, Texture2D>;
            if (aliases == null)
            {
                return;
            }

            int added = 0;
            foreach (string path in texturePaths)
            {
                string alias = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(alias) || aliases.ContainsKey(alias))
                {
                    continue;
                }

                Texture2D texture = ModPaths.LoadTexture(path);
                if (texture == null)
                {
                    continue;
                }

                try
                {
                    aliases[alias] = texture;
                    added++;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Could not alias texture '" + alias + "': " + e.Message);
                }
            }

            if (added > 0)
            {
                Plugin.Log.LogInfo("Published " + added + " texture alias(es) to PlusLevelLoader.");
            }
        }

        // ------------------------------------------------------------------ rooms

        /// <summary>
        /// Registers a room alias such as "XPC" so .rbpl rooms that reference it resolve.
        /// </summary>
        internal static bool PublishRoom(string alias, RoomCategory category, RoomType type, Color color, string doorMaterialName)
        {
            IDictionary<string, object> rooms = GetTable("roomSettings") as IDictionary<string, object>;
            if (rooms == null)
            {
                return false;
            }

            if (rooms.ContainsKey(alias) || !knownRoomAliases.Add(alias))
            {
                return true;
            }

            Type settingsType = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                settingsType = assembly.GetType(RoomSettingsTypeName, false);
                if (settingsType != null)
                {
                    break;
                }
            }

            if (settingsType == null)
            {
                Plugin.Log.LogWarning("PlusLevelLoader.RoomSettings was not found; room alias '" + alias + "' is skipped.");
                return false;
            }

            StandardDoorMats doors = ResolveDoorMaterials(doorMaterialName);
            Material mapMaterial = null;

            ConstructorInfo constructor = settingsType.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (constructor == null)
            {
                return false;
            }

            object[] arguments = BuildArguments(constructor, category, type, color, doors, mapMaterial);
            if (arguments == null)
            {
                return false;
            }

            try
            {
                rooms[alias] = constructor.Invoke(arguments);
                Plugin.Log.LogInfo("Registered PlusLevelLoader room alias '" + alias + "' as " + category + "/" + type + ".");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Failed to register room alias '" + alias + "': " + e.Message);
                return false;
            }
        }

        private static object[] BuildArguments(ConstructorInfo constructor, RoomCategory category, RoomType type, Color color, StandardDoorMats doors, Material mapMaterial)
        {
            ParameterInfo[] parameters = constructor.GetParameters();
            object[] arguments = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;
                if (parameterType == typeof(RoomCategory))
                {
                    arguments[i] = category;
                }
                else if (parameterType == typeof(RoomType))
                {
                    arguments[i] = type;
                }
                else if (parameterType == typeof(Color))
                {
                    arguments[i] = color;
                }
                else if (parameterType == typeof(StandardDoorMats))
                {
                    arguments[i] = doors;
                }
                else if (parameterType == typeof(Material))
                {
                    arguments[i] = mapMaterial;
                }
                else if (parameterType == typeof(RoomFunctionContainer))
                {
                    arguments[i] = null;
                }
                else if (parameterType == typeof(string))
                {
                    arguments[i] = parameters[i].Name;
                }
                else
                {
                    Plugin.Log.LogWarning("Unsupported RoomSettings parameter '" + parameters[i].Name + "' of type " + parameterType.Name + ".");
                    return null;
                }
            }

            return arguments;
        }

        private static StandardDoorMats ResolveDoorMaterials(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
            {
                return null;
            }

            StandardDoorMats[] all = Resources.FindObjectsOfTypeAll<StandardDoorMats>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && string.Equals(all[i].name, materialName, StringComparison.OrdinalIgnoreCase))
                {
                    return all[i];
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ prefabs

        /// <summary>
        /// Adds prefab aliases for game objects whose name matches what the .rbpl
        /// files reference. Anything PlusLevelLoader already knows is left alone.
        /// </summary>
        internal static void PublishPrefabAliases(IEnumerable<string> prefabNames)
        {
            IDictionary<string, GameObject> aliases = GetTable("prefabAliases") as IDictionary<string, GameObject>;
            if (aliases == null)
            {
                return;
            }

            HashSet<string> wanted = new HashSet<string>(prefabNames, StringComparer.OrdinalIgnoreCase);
            if (wanted.Count == 0)
            {
                return;
            }

            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            int added = 0;
            var unresolved = new List<string>();

            foreach (string name in wanted)
            {
                if (aliases.ContainsKey(name) || !knownPrefabAliases.Add(name))
                {
                    continue;
                }

                GameObject match = null;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && string.Equals(all[i].name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        match = all[i];
                        break;
                    }
                }

                if (match == null)
                {
                    unresolved.Add(name);
                    continue;
                }

                try
                {
                    aliases[name] = match;
                    added++;
                }
                catch (Exception)
                {
                    unresolved.Add(name);
                }
            }

            if (added > 0)
            {
                Plugin.Log.LogInfo("Published " + added + " prefab alias(es) to PlusLevelLoader.");
            }

            if (unresolved.Count > 0)
            {
                Plugin.Log.LogWarning(
                    "These prefab names are referenced by SH20XX .rbpl rooms but were not found in the loaded game objects: " +
                    string.Join(", ", unresolved.ToArray()));
            }
        }

        // ------------------------------------------------------------------ levels

        /// <summary>
        /// Turns a .rbpl file into a SceneObject using PlusLevelLoader's own loader.
        /// </summary>
        internal static SceneObject LoadLevel(string rbplPath)
        {
            Resolve();
            if (pluginInstance == null)
            {
                Plugin.Log.LogWarning("Cannot load '" + rbplPath + "' because PlusLevelLoader is unavailable.");
                return null;
            }

            Type loaderType = null;
            Type levelType = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (loaderType == null)
                {
                    loaderType = assembly.GetType(LoaderTypeName, false);
                }

                if (levelType == null)
                {
                    levelType = assembly.GetType(FormatLevelTypeName, false);
                }
            }

            if (loaderType == null || levelType == null)
            {
                Plugin.Log.LogWarning("PlusLevelFormat/PlusLevelLoader types were not found.");
                return null;
            }

            try
            {
                MethodInfo readLevel = null;
                foreach (Type owner in levelType.Assembly.GetTypes())
                {
                    foreach (MethodInfo method in owner.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        ParameterInfo[] parameters = method.GetParameters();
                        if (method.Name == "ReadLevel" && parameters.Length == 1 && parameters[0].ParameterType == typeof(System.IO.BinaryReader))
                        {
                            readLevel = method;
                            break;
                        }
                    }

                    if (readLevel != null)
                    {
                        break;
                    }
                }

                if (readLevel == null)
                {
                    Plugin.Log.LogWarning("PlusLevelFormat.ReadLevel was not found.");
                    return null;
                }

                object level;
                using (var stream = System.IO.File.OpenRead(rbplPath))
                using (var reader = new System.IO.BinaryReader(stream))
                {
                    level = readLevel.Invoke(null, new object[] { reader });
                }

                MethodInfo load = loaderType.GetMethod("LoadLevel", BindingFlags.Public | BindingFlags.Static);
                if (load == null)
                {
                    Plugin.Log.LogWarning("PlusLevelLoader.CustomLevelLoader.LoadLevel was not found.");
                    return null;
                }

                SceneObject sceneObject = load.Invoke(null, new object[] { level }) as SceneObject;
                if (sceneObject != null)
                {
                    sceneObject.name = "SH20XX_" + System.IO.Path.GetFileNameWithoutExtension(rbplPath);
                }

                return sceneObject;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Failed to load level '" + rbplPath + "': " + e.Message);
                return null;
            }
        }
    }
}
