using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Discovers the SH20XX room definitions under Data/Rooms, then hands them to
    /// <see cref="RoomBuilder"/> so they become real, generatable RoomAssets.
    /// </summary>
    internal static class RoomRegistration
    {
        /// <summary>
        /// One Data/Rooms/**/*.json definition.
        /// </summary>
        internal sealed class RoomDefinition
        {
            internal string Name = string.Empty;
            internal string Alias = string.Empty;
            internal RoomCategory Category = RoomCategory.Special;
            internal RoomType Type = RoomType.Room;
            internal Color Color = Color.white;
            internal string DoorSet = "ClassDoorSet";
            internal int Width;
            internal int Height;
            internal int SpawnWeight = 20;
            internal string JsonPath;
            internal string RbplPath;

            /// <summary>
            /// JSON file name without extension. Used to match editor icons such as
            /// ed_tool_Room_Roomjojo.png against this room.
            /// </summary>
            internal string SourceFile = string.Empty;

            private readonly Dictionary<string, string> textures =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            internal void SetTexture(string key, string relativePath)
            {
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(relativePath))
                {
                    return;
                }

                string full = ModPaths.Combine(ModPaths.Root, relativePath.Replace('/', Path.DirectorySeparatorChar), new string[0]);
                if (File.Exists(full))
                {
                    textures[key] = full;
                }
                else
                {
                    Plugin.Log.LogWarning("Room '" + Name + "' references a missing texture: " + relativePath);
                }
            }

            internal string Texture(string key)
            {
                string value;
                return textures.TryGetValue(key, out value) ? value : null;
            }

            internal string RbplFileName
            {
                get { return RbplPath == null ? null : Path.GetFileName(RbplPath); }
            }

            internal bool HasRbpl
            {
                get { return !string.IsNullOrEmpty(RbplPath); }
            }
        }

        internal static readonly List<RoomDefinition> Definitions = new List<RoomDefinition>();

        internal static void Register(Plugin mod)
        {
            Definitions.Clear();
            Discover();

            if (Definitions.Count == 0)
            {
                Plugin.Log.LogWarning("No room definitions were found under Data/Rooms.");
                return;
            }

            for (int i = 0; i < Definitions.Count; i++)
            {
                RoomDefinition room = Definitions[i];
                Plugin.Log.LogInfo(string.Format(
                    "Room '{0}' (type '{1}', {2}/{3}) json='{4}' rbpl='{5}'",
                    room.Name, room.Alias, room.Category, room.Type,
                    room.JsonPath == null ? "-" : Path.GetFileName(room.JsonPath),
                    room.RbplFileName ?? "-"));

                if (room.HasRbpl)
                {
                    // Deliberately not parsed: the shipped .rbpl layout is an
                    // unsupported legacy encoding. See RoomBuilder for details.
                    Plugin.Log.LogWarning(
                        "  '" + room.RbplFileName + "' uses an .rbpl layout this build cannot decode; " +
                        "the room will use a vanilla layout re-skinned with its own textures.");
                }
            }

            RoomBuilder.Build(Definitions);
        }

        // ------------------------------------------------------------------ discovery

        private static void Discover()
        {
            string root = ModPaths.Data("Rooms");
            if (!Directory.Exists(root))
            {
                return;
            }

            string[] jsonFiles = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
            Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < jsonFiles.Length; i++)
            {
                RoomDefinition room = ReadDefinition(jsonFiles[i]);
                if (room != null)
                {
                    Definitions.Add(room);
                }
            }
        }

        private static RoomDefinition ReadDefinition(string jsonPath)
        {
            SimpleJson json;
            try
            {
                json = SimpleJson.Parse(File.ReadAllText(jsonPath));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not parse room json '" + jsonPath + "': " + e.Message);
                return null;
            }

            string name = json.String("name");
            if (string.IsNullOrEmpty(name))
            {
                name = Path.GetFileNameWithoutExtension(jsonPath);
            }

            var room = new RoomDefinition
            {
                Name = name,
                JsonPath = jsonPath,
                Alias = name,
                SourceFile = Path.GetFileNameWithoutExtension(jsonPath),
            };

            string category = json.String("category");
            RoomCategory parsed;
            room.Category = TryParseCategory(category, out parsed) ? parsed : ClassifyCategory(name);

            string roomType = json.String("room_type") ?? json.String("roomType");
            room.Type = "hall".Equals(roomType, StringComparison.OrdinalIgnoreCase) ? RoomType.Hall : RoomType.Room;

            string doorSet = json.String("doorSet");
            if (!string.IsNullOrEmpty(doorSet))
            {
                room.DoorSet = doorSet;
            }

            // Room size, if the definition declares one.
            SimpleJson size = json.Object("roomSize");
            if (size != null)
            {
                room.Width = (int)size.Number("width", 0f);
                room.Height = (int)size.Number("height", 0f);
            }

            // Spawn weight from a "weights" map such as { "2": 30, "3": 50 }.
            SimpleJson generation = json.Object("generation");
            if (generation != null)
            {
                SimpleJson weights = generation.Object("weights");
                if (weights != null)
                {
                    int best = 0;
                    foreach (KeyValuePair<string, SimpleJson> pair in weights.Entries())
                    {
                        int w = (int)pair.Value.AsNumber(0f);
                        if (w > best)
                        {
                            best = w;
                        }
                    }

                    if (best > 0)
                    {
                        room.SpawnWeight = best;
                    }
                }
            }

            SimpleJson textureBlock = json.Object("textures");
            if (textureBlock != null)
            {
                room.SetTexture("floor", textureBlock.String("floor"));
                room.SetTexture("wall", textureBlock.String("wall"));
                room.SetTexture("ceiling", textureBlock.String("ceiling"));
                room.SetTexture("door", textureBlock.String("door"));
                room.SetTexture("doorClosed", textureBlock.String("doorClosed"));
                room.SetTexture("doorOpen", textureBlock.String("doorOpen"));
            }

            string sibling = Path.ChangeExtension(jsonPath, ".rbpl");
            if (File.Exists(sibling))
            {
                room.RbplPath = sibling;

                // The header names the room type; that part of the format does
                // decode reliably, so it is still worth reading.
                List<string> header = RbplInspector.ReadHeaderTokens(sibling, 5);
                if (header.Count >= 2 && !string.IsNullOrEmpty(header[1]))
                {
                    room.Alias = header[1];
                    if (string.IsNullOrEmpty(category))
                    {
                        room.Category = ClassifyCategory(header[1]);
                    }
                }
            }

            return room;
        }

        private static bool TryParseCategory(string value, out RoomCategory category)
        {
            category = RoomCategory.Special;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string[] names = Enum.GetNames(typeof(RoomCategory));
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    category = (RoomCategory)Enum.Parse(typeof(RoomCategory), names[i]);
                    return true;
                }
            }

            return false;
        }

        private static RoomCategory ClassifyCategory(string alias)
        {
            if (string.IsNullOrEmpty(alias))
            {
                return RoomCategory.Special;
            }

            switch (alias.ToLowerInvariant())
            {
                case "class":
                    return RoomCategory.Class;
                case "faculty":
                    return RoomCategory.Faculty;
                case "office":
                    return RoomCategory.Office;
                case "hall":
                    return RoomCategory.Hall;
                case "store":
                    return RoomCategory.Store;
                default:
                    return RoomCategory.Special;
            }
        }
    }
}
