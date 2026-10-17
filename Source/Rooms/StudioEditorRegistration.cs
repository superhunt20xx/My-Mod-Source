using System;
using System.Collections.Generic;
using System.IO;
using PlusLevelStudio;
using PlusLevelStudio.Editor;
using PlusLevelStudio.Editor.Tools;
using PlusStudioLevelLoader;
using PlusStudioLevelFormat;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Adds the SH20XX rooms to the Plus Studio level editor as freely resizable rooms.
    ///
    /// How a resizable room works in Plus Studio (traced through the IL):
    ///   * RoomTool.get_id() returns "room_" + roomType, and the editor looks up
    ///     "Ed_Tool_" + id - exactly the naming already used in Localization.json
    ///     (Ed_Tool_room_XPC_Title / _Desc).
    ///   * Dragging a RoomTool builds a room through
    ///     EditorLevelData.CreateRoomWithDefaultSettings(roomType), which does
    ///     defaultTextures[roomType] - an unguarded dictionary index.
    ///   * defaultTextures is copied from LevelStudioPlugin.defaultRoomTextures when
    ///     an EditorLevelData is constructed.
    ///   * A TextureContainer holds texture NAMES, resolved through
    ///     LevelLoaderPlugin.roomTextureAliases, falling back to PlaceholderWall and
    ///     logging "Cannot find: X as roomtexture!" otherwise.
    ///
    /// So three things must be published, in this order: the textures under their
    /// aliases, the room type in defaultRoomTextures, then the tool itself.
    /// </summary>
    internal static class StudioEditorRegistration
    {
        private const string Category = "rooms";
        private const string IconPrefix = "ed_tool_Room_";
        private const string LocalizationTitlePrefix = "Ed_Tool_room_";
        private const string LocalizationTitleSuffix = "_Title";
        private const string LocalizationDescSuffix = "_Desc";

        private static readonly Dictionary<string, string> Localized =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<RoomTypeEntry> Entries = new List<RoomTypeEntry>();

        private static bool callbackRegistered;

        private sealed class RoomTypeEntry
        {
            internal string TypeId;
            internal string Title;
            internal Sprite Icon;
            internal RoomBuilder.BuiltRoom Room;
        }

        /// <summary>
        /// Matches every authored tool to a built room once, and keeps the result.
        /// </summary>
        private static void MatchAll()
        {
            Entries.Clear();
            var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string> authored in AuthoredTools())
            {
                string suffix = authored.Key;
                RoomBuilder.BuiltRoom room = Match(suffix, claimed);
                if (room == null)
                {
                    Plugin.Log.LogWarning("No built room matched editor tool '" + suffix + "'.");
                    continue;
                }

                claimed.Add(room.Name);
                Entries.Add(new RoomTypeEntry
                {
                    TypeId = suffix,
                    Title = authored.Value,
                    Icon = IconFor(suffix),
                    Room = room,
                });
            }

            if (Entries.Count == 0)
            {
                Plugin.Log.LogWarning(
                    "No editor room types were registered; check the Ed_Tool_room_* entries in Localization.json.");
            }
        }

        /// <summary>
        /// Publishes PlusStudioLevelLoader.RoomSettings for each room type.
        ///
        /// This is what makes doors use the room's own art in the editor:
        /// LevelImporter.CreateRoomAsset copies RoomSettings.doorMat into
        /// RoomAsset.doorMats, and the editor's StandardDoorDisplay.UpdateSides reads
        /// RoomSettings.doorMat directly. Without an entry here a placed room falls
        /// back to a default door set, which is why the doors looked unchanged.
        /// </summary>
        private static void PublishRoomSettings()
        {
            Dictionary<string, RoomSettings> settings = RoomSettingsTable();
            if (settings == null)
            {
                Plugin.Log.LogWarning("PlusStudioLevelLoader.roomSettings was not reachable; editor doors keep their default art.");
                return;
            }

            int added = 0;
            for (int i = 0; i < Entries.Count; i++)
            {
                RoomTypeEntry entry = Entries[i];
                RoomBuilder.BuiltRoom room = entry.Room;

                if (room == null || room.DoorMats == null)
                {
                    Plugin.Log.LogWarning("Room type '" + entry.TypeId + "' has no door materials to publish.");
                    continue;
                }

                try
                {
                    settings[entry.TypeId] = new RoomSettings(
                        room.Category, room.Type, room.Color, room.DoorMats, null);
                    added++;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Could not publish room settings for '" + entry.TypeId + "': " + e.Message);
                }
            }

            if (added > 0)
            {
                Plugin.Log.LogInfo("Published " + added + " room setting(s) with door art to Plus Studio.");
            }
        }

        private static Dictionary<string, RoomSettings> RoomSettingsTable()
        {
            try
            {
                LevelLoaderPlugin instance = LevelLoaderPlugin.Instance;
                return instance == null ? null : instance.roomSettings;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not read the Plus Studio room settings: " + e.Message);
                return null;
            }
        }

        internal static void Register(Plugin mod)
        {
            if (mod == null || RoomBuilder.Rooms.Count == 0)
            {
                return;
            }

            if (!Available())
            {
                Plugin.Log.LogInfo("Plus Studio was not detected; editor room registration is skipped.");
                return;
            }

            LoadLocalization();
            PublishRoomTextures();
            // Room settings carry the door art per room type, so they have to exist
            // before the room types are registered.
            MatchAll();
            PublishRoomSettings();
            PublishRoomTypes();

            // ItemTool.TryPlace indexes LevelLoaderPlugin.itemObjects by the tool's id
            // with no ContainsKey guard, so an item that is not in that table cannot be
            // placed even though its tool renders.
            PublishItemObjects();

            // Creates the PosterObjects and registers them, so the poster category has
            // this mod's artwork and the generator can pick them up.
            RegisterPosters();

            if (!callbackRegistered)
            {
                try
                {
                    EditorInterfaceModes.AddModeCallback(AddToolsToMode);
                    callbackRegistered = true;
                    Plugin.Log.LogInfo("Registered the Plus Studio editor-mode callback.");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("Could not register the Plus Studio editor callback: " + e);
                }
            }
        }

        private static bool Available()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetType("PlusLevelStudio.EditorInterfaceModes", false) != null)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ localization

        /// <summary>
        /// Reads the Ed_Tool_room_* entries from Localization.json so the editor shows
        /// the authored names and tooltips instead of raw ids.
        /// </summary>
        private static void LoadLocalization()
        {
            Localized.Clear();

            string path = Path.Combine(ModPaths.Root, "Localization.json");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                SimpleJson json = SimpleJson.Parse(File.ReadAllText(path));
                SimpleJson items = json.Array("items");
                if (items == null)
                {
                    return;
                }

                foreach (SimpleJson item in items.Items())
                {
                    string key = item.String("key");
                    string value = item.String("value");
                    if (!string.IsNullOrEmpty(key) && value != null)
                    {
                        Localized[key] = value;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not read editor tool localization: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ textures

        /// <summary>
        /// Publishes each room's floor/wall/ceiling under its file name, which is the
        /// alias form TextureContainer expects.
        /// </summary>
        private static void PublishRoomTextures()
        {
            Dictionary<string, Texture2D> aliases = TextureAliases();
            if (aliases == null)
            {
                Plugin.Log.LogWarning("PlusStudioLevelLoader.roomTextureAliases was not reachable.");
                return;
            }

            int added = 0;
            for (int i = 0; i < RoomBuilder.Rooms.Count; i++)
            {
                RoomBuilder.BuiltRoom room = RoomBuilder.Rooms[i];
                string[] paths = { room.FloorPath, room.WallPath, room.CeilingPath };

                for (int p = 0; p < paths.Length; p++)
                {
                    string alias = AliasOf(paths[p]);
                    if (string.IsNullOrEmpty(alias) || aliases.ContainsKey(alias))
                    {
                        continue;
                    }

                    Texture2D texture = ModPaths.LoadTexture(paths[p]);
                    if (texture == null)
                    {
                        continue;
                    }

                    aliases[alias] = texture;
                    added++;
                }
            }

            if (added > 0)
            {
                Plugin.Log.LogInfo("Published " + added + " room texture alias(es) to Plus Studio.");
            }
        }

        private static Dictionary<string, Texture2D> TextureAliases()
        {
            try
            {
                LevelLoaderPlugin instance = LevelLoaderPlugin.Instance;
                return instance == null ? null : instance.roomTextureAliases;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not read the Plus Studio texture aliases: " + e.Message);
                return null;
            }
        }

        // ------------------------------------------------------------------ room types

        /// <summary>
        /// Registers the matched room types in LevelStudioPlugin.defaultRoomTextures.
        /// Content is copied out of this table when an EditorLevelData is created, so
        /// a level has to be opened fresh for a new room type to appear.
        /// </summary>
        private static void PublishRoomTypes()
        {
            Dictionary<string, TextureContainer> roomTypes = RoomTypes();
            if (roomTypes == null)
            {
                Plugin.Log.LogWarning("LevelStudioPlugin.defaultRoomTextures was not reachable.");
                return;
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                RoomTypeEntry entry = Entries[i];
                RoomBuilder.BuiltRoom room = entry.Room;
                if (room == null)
                {
                    continue;
                }

                var container = new TextureContainer(
                    AliasOf(room.FloorPath),
                    AliasOf(room.WallPath),
                    AliasOf(room.CeilingPath));

                try
                {
                    roomTypes[entry.TypeId] = container;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Could not register room type '" + entry.TypeId + "': " + e.Message);
                    continue;
                }

                Plugin.Log.LogInfo("Registered resizable editor room '" + entry.TypeId + "' ('" +
                                   entry.Title + "') for room '" + room.Name + "'.");
            }

            if (Entries.Count == 0)
            {
                Plugin.Log.LogWarning(
                    "No editor room types were registered; check the Ed_Tool_room_* entries in Localization.json.");
            }
        }

        /// <summary>
        /// The authored room tool entries, keyed by the suffix after "Ed_Tool_room_".
        /// Only the _Title entries carry the display name.
        /// </summary>
        private static IEnumerable<KeyValuePair<string, string>> AuthoredTools()
        {
            foreach (KeyValuePair<string, string> pair in Localized)
            {
                if (!pair.Key.StartsWith(LocalizationTitlePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string rest = pair.Key.Substring(LocalizationTitlePrefix.Length);
                if (!rest.EndsWith(LocalizationTitleSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string suffix = rest.Substring(0, rest.Length - LocalizationTitleSuffix.Length);
                if (!string.IsNullOrEmpty(suffix))
                {
                    yield return new KeyValuePair<string, string>(suffix, pair.Value);
                }
            }
        }

        private static Dictionary<string, TextureContainer> RoomTypes()
        {
            try
            {
                LevelStudioPlugin instance = LevelStudioPlugin.Instance;
                return instance == null ? null : instance.defaultRoomTextures;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not read defaultRoomTextures: " + e.Message);
                return null;
            }
        }

        private static string AliasOf(string path)
        {
            return string.IsNullOrEmpty(path) ? null : Path.GetFileNameWithoutExtension(path);
        }

        /// <summary>
        /// Finds the tool icon for an authored tool name.
        ///
        /// The name in Localization.json can carry a mod-attribution prefix that the
        /// icon file does not have - the keys are Ed_Tool_room_BBP_Ruin321_Title and
        /// Ed_Tool_room_BBPP_Roomjojo_Title while the files are
        /// ed_tool_Room_Ruin321.png and ed_tool_Room_Roomjojo.png. So the exact name is
        /// tried first, then common prefixes are stripped.
        /// </summary>
        private static Sprite IconFor(string suffix)
        {
            string folder = ModPaths.Textures("EditorUI");

            foreach (string candidate in IconCandidates(suffix))
            {
                string path = Path.Combine(folder, IconPrefix + candidate + ".png");
                if (File.Exists(path))
                {
                    return ModPaths.LoadSprite(path, 25f);
                }
            }

            Plugin.Log.LogWarning("No editor icon found for '" + suffix + "' in " + folder +
                                  " (tried " + string.Join(", ", IconCandidates(suffix)) + ").");
            return null;
        }

        private static IEnumerable<string> IconCandidates(string suffix)
        {
            yield return suffix;

            string[] prefixes = { "BBPP_", "BBP_", "SH20XX_" };
            for (int i = 0; i < prefixes.Length; i++)
            {
                if (suffix.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase) &&
                    suffix.Length > prefixes[i].Length)
                {
                    yield return suffix.Substring(prefixes[i].Length);
                }
            }
        }

        /// <summary>
        /// Authored tool name to room name. Needed because the names are not always
        /// derivable: "XPC" is an abbreviation of "XX_Purple-chan", which no prefix
        /// or normalisation rule can produce. Entries are matched with separators
        /// stripped and case ignored.
        /// </summary>
        private static readonly Dictionary<string, string> RoomAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "XPC", "XX_Purple-chan" },
                { "BBP_Ruin321", "Ruin321" },
                { "BBPP_Roomjojo", "Roomjojo" },
                { "TaserRoom", "TaserRoom" },
                { "SteelBallRoom", "SteelBallRoom" },
            };

        /// <summary>
        /// Finds the built room an authored tool belongs to. Tries the explicit alias
        /// table first, then a normalised prefix match so extra rooms keep working
        /// without needing a new entry.
        /// </summary>
        private static RoomBuilder.BuiltRoom Match(string suffix, HashSet<string> claimed)
        {
            string target = Normalize(suffix);

            string alias;
            if (RoomAliases.TryGetValue(suffix, out alias))
            {
                target = Normalize(alias);
            }

            // Exact match on the alias target (or the suffix itself).
            for (int i = 0; i < RoomBuilder.Rooms.Count; i++)
            {
                RoomBuilder.BuiltRoom room = RoomBuilder.Rooms[i];
                if (!claimed.Contains(room.Name) && Normalize(room.Name) == target)
                {
                    return room;
                }
            }

            for (int i = 0; i < RoomBuilder.Rooms.Count; i++)
            {
                RoomBuilder.BuiltRoom room = RoomBuilder.Rooms[i];
                if (claimed.Contains(room.Name))
                {
                    continue;
                }

                if (Normalize(room.Name).StartsWith(target, StringComparison.OrdinalIgnoreCase) ||
                    Normalize(room.SourceFile).StartsWith(target, StringComparison.OrdinalIgnoreCase))
                {
                    return room;
                }
            }

            return null;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        // ------------------------------------------------------------------ editor tools

        private static void AddToolsToMode(EditorMode mode, bool vanillaCompliant)
        {
            if (mode == null)
            {
                return;
            }

            try
            {
                int added = 0;
                for (int i = 0; i < Entries.Count; i++)
                {
                    RoomTypeEntry entry = Entries[i];
                    // RoomTool, not PremadeRoomTool: this is the drag-to-resize tool.
                    if (EditorInterfaceModes.AddToolToCategory(mode, Category, new RoomTool(entry.TypeId, entry.Icon), true))
                    {
                        added++;
                    }
                }

                if (added > 0)
                {
                    Plugin.Log.LogInfo("Added " + added + " resizable SH20XX room tool(s) to editor mode '" + mode.id + "'.");
                }

                AddItemToolsToMode(mode);
                AddObjectToolsToMode(mode);
                AddPosterToolsToMode(mode);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not add SH20XX rooms to editor mode '" + mode.id + "': " + e);
            }
        }

        // ------------------------------------------------------------------ item tools

        /// <summary>
        /// Item tool ids, matching the icon files Ed_Tool_item_&lt;id&gt;.png and the
        /// keys Ed_Tool_item_&lt;id&gt;_Title / _Desc. ItemTool.get_id() is
        /// "item_" + id and its title/desc keys are "Ed_Tool_" + id + suffix, so the id
        /// has to be spelled exactly like the icon's suffix.
        /// </summary>
        private static readonly string[] ItemToolIds =
        {
            "Potion", "Taser", "99Cassettes", "TestPaper",
            "Cirno", "FumoReimuHakurei", "Noise", "SanaeKochiya",
        };

        private const string ItemCategory = "items";
        private const string ItemIconPrefix = "Ed_Tool_item_";

        // ----------------------------------------------------------------- poster tools

        /// <summary>
        /// The mod's own posters, which appear in the editor's poster category.
        ///
        /// Each entry is the texture's base name: Textures/Posters/PST_BALDI.png becomes
        /// the "PST_BALDI" entry in EditorCustomContent.posters.
        /// </summary>
        private static readonly string[] PosterIds =
        {
            "PST_BALDI", "PST_EpicFaceSay", "PST_HNT_mind", "PST_Ruin321",
        };

        private const string PosterCategory = "posters";
        private const string PosterIconFile = "Ed_Tool_PosterIcon.png";

        // ----------------------------------------------------------------- object tools

        /// <summary>
        /// Object tool ids, shown in the editor's 物件 (objects) category. These match
        /// the icon files Ed_Tool_object_&lt;id&gt;.png.
        ///
        /// ObjectTool.get_id() is "object_" + type, so the icon suffix has to be the
        /// id itself. ObjectTool(String, Sprite) is used deliberately: the 2-arg
        /// constructor leaves useItemName false, which makes the tool resolve its text
        /// from Ed_Tool_object_&lt;id&gt;_Title / _Desc rather than from an object name.
        /// </summary>
        private static readonly string[] ObjectToolIds =
        {
            "PotionMachines", "TaserMachines",
        };

        private const string ObjectCategory = "objects";
        private const string ObjectIconPrefix = "Ed_Tool_object_";

        private static void AddObjectToolsToMode(EditorMode mode)
        {
            int added = 0;

            for (int i = 0; i < ObjectToolIds.Length; i++)
            {
                string id = ObjectToolIds[i];

                try
                {
                    string path = Path.Combine(ModPaths.Textures("EditorUI"), ObjectIconPrefix + id + ".png");
                    Sprite icon = File.Exists(path) ? ModPaths.LoadSprite(path, 25f) : null;

                    if (icon == null)
                    {
                        Plugin.Log.LogWarning("No editor icon for object tool '" + id + "' (expected " +
                                              ObjectIconPrefix + id + ".png).");
                        continue;
                    }

                    if (EditorInterfaceModes.AddToolToCategory(mode, ObjectCategory, new ObjectTool(id, icon), true))
                    {
                        added++;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Could not add object tool '" + id + "': " + e.Message);
                }
            }

            if (added > 0)
            {
                Plugin.Log.LogInfo("Added " + added + " SH20XX object tool(s) to editor mode '" + mode.id + "'.");
            }

            RegisterObjectDisplays();
        }

        private static bool objectDisplaysRegistered;

        /// <summary>
        /// Registers the editor-side display objects for the machine tools.
        ///
        /// This is the step that makes placement actually work. ObjectTool.TryPlace only
        /// records the type string; the visual comes from
        /// BasicObjectLocation.GetVisualPrefab, which indexes
        /// LevelStudioPlugin.basicObjectDisplays[prefab] with no ContainsKey guard. A key
        /// that is not in that table throws, so the object is never added and the tool
        /// looks like it "cannot place" even though the direction step completed.
        ///
        /// EditorInterface.AddObjectVisual is the public registration entry point.
        /// </summary>
        private static void RegisterObjectDisplays()
        {
            if (objectDisplaysRegistered)
            {
                return;
            }

            objectDisplaysRegistered = true;

            for (int i = 0; i < ObjectToolIds.Length; i++)
            {
                string id = ObjectToolIds[i];

                try
                {
                    LevelStudioPlugin plugin = LevelStudioPlugin.Instance;
                    if (plugin == null || plugin.basicObjectDisplays == null)
                    {
                        Plugin.Log.LogWarning("Plus Studio is not ready; object display '" + id + "' was not registered.");
                        objectDisplaysRegistered = false;
                        return;
                    }

                    if (plugin.basicObjectDisplays.ContainsKey(id))
                    {
                        continue;
                    }

                    GameObject visual = BuildMachineVisual(id);
                    if (visual == null)
                    {
                        continue;
                    }

                    EditorInterface.AddObjectVisual(id, visual, true);
                    Plugin.Log.LogInfo("Registered the editor display for object '" + id + "'.");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Could not register the display for '" + id + "': " + e.Message);
                }
            }
        }

        /// <summary>
        /// Builds a minimal stand-in visual: a textured quad sized to a locker, plus a
        /// collider so the editor has something to click. The real machine mesh is not
        /// available to the mod, so the sprite from Textures/VendingMachines is used.
        /// </summary>
        private static GameObject BuildMachineVisual(string id)
        {
            try
            {
                string file = id == "PotionMachines" ? "Machine_Potion.png" : "Machine_Taser.png";
                string path = Path.Combine(ModPaths.Root, file);

                Texture2D texture = File.Exists(path) ? ModPaths.LoadTexture(path) : null;
                if (texture == null)
                {
                    Plugin.Log.LogWarning("No texture at " + path + " for object display '" + id + "'.");
                    return null;
                }

                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = id + "_Visual";

                // Roughly locker-sized so it reads at editor scale.
                visual.transform.localScale = new Vector3(1f, 1f, 0.25f);

                var renderer = visual.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    string shaderName = "Unlit/Texture";
                    Shader shader = Shader.Find(shaderName) ?? Shader.Find("Sprites/Default");
                    if (shader != null)
                    {
                        renderer.sharedMaterial = new Material(shader) { mainTexture = texture };
                    }
                }

                // SetActive(true) keeps the clone valid when AddObjectVisual strips
                // behaviours from it.
                visual.SetActive(true);
                return visual;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not build the visual for '" + id + "': " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Publishes the SH20XX items into PlusStudioLevelLoader's item table under the
        /// same ids the tools use.
        ///
        /// This is required, not cosmetic: ItemTool.TryPlace reads
        /// LevelLoaderPlugin.itemObjects[item] to play the pickup sound, with no
        /// ContainsKey check, so a missing entry means the item cannot be placed.
        /// The key has to match ItemTool.get_id() minus its "item_" prefix, which is
        /// exactly the enum name the item was registered with.
        /// </summary>
        private static void PublishItemObjects()
        {
            Dictionary<string, ItemObject> table = ItemObjects();
            if (table == null)
            {
                Plugin.Log.LogWarning("PlusStudioLevelLoader.itemObjects was not reachable; item tools cannot place.");
                return;
            }

            int added = 0;
            for (int i = 0; i < ItemToolIds.Length; i++)
            {
                string id = ItemToolIds[i];

                ItemObject item;
                if (!ItemRegistration.Items.TryGetValue(id, out item) || item == null)
                {
                    Plugin.Log.LogWarning("Item tool '" + id + "' has no registered ItemObject to publish.");
                    continue;
                }

                if (table.ContainsKey(id))
                {
                    continue;
                }

                try
                {
                    table[id] = item;
                    added++;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Could not publish item '" + id + "': " + e.Message);
                }
            }

            if (added > 0)
            {
                Plugin.Log.LogInfo("Published " + added + " item(s) to Plus Studio for editor placement.");
            }
        }

        private static Dictionary<string, ItemObject> ItemObjects()
        {
            try
            {
                LevelLoaderPlugin instance = LevelLoaderPlugin.Instance;
                return instance == null ? null : instance.itemObjects;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not read the Plus Studio item table: " + e.Message);
                return null;
            }
        }

        private static void AddItemToolsToMode(EditorMode mode)
        {
            int added = 0;

            for (int i = 0; i < ItemToolIds.Length; i++)
            {
                string id = ItemToolIds[i];

                try
                {
                    string path = Path.Combine(ModPaths.Textures("EditorUI"), ItemIconPrefix + id + ".png");
                    Sprite icon = File.Exists(path) ? ModPaths.LoadSprite(path, 25f) : null;

                    if (icon == null)
                    {
                        Plugin.Log.LogWarning("No editor icon for item tool '" + id + "' (expected " +
                                              ItemIconPrefix + id + ".png).");
                        continue;
                    }

                    if (EditorInterfaceModes.AddToolToCategory(mode, ItemCategory, new ItemTool(id, icon), true))
                    {
                        added++;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Could not add item tool '" + id + "': " + e.Message);
                }
            }

            if (added > 0)
            {
                Plugin.Log.LogInfo("Added " + added + " SH20XX item tool(s) to editor mode '" + mode.id + "'.");
            }
        }

        // ----------------------------------------------------------------- posters

        /// <summary>
        /// Builds a PosterObject for every entry in PosterIds, registers it with Plus
        /// Studio so the editor can place it, and hands it to PosterRegistry so level
        /// generation can add it to a level's poster pool.
        ///
        /// The text array must not be null: EnvironmentController.BuildPoster reads
        /// textData.Length while generating, and passing null produced a
        /// NullReferenceException at 99.99% of generation - which surfaced as a
        /// GENERATION TIMEOUT and an unfinished level.
        /// </summary>
        private static void RegisterPosters()
        {
            Dictionary<string, PosterObject> table = PosterTable();
            if (table == null)
            {
                Plugin.Log.LogWarning("EditorCustomContent.posters was not reachable; posters cannot be placed.");
                return;
            }

            int added = 0;
            for (int i = 0; i < PosterIds.Length; i++)
            {
                string id = PosterIds[i];

                try
                {
                    string path = Path.Combine(ModPaths.Textures("Posters"), id + ".png");
                    if (!File.Exists(path))
                    {
                        Plugin.Log.LogWarning("No poster texture for '" + id + "' (expected " + path + ").");
                        continue;
                    }

                    if (table.ContainsKey(id))
                    {
                        continue;
                    }

                    Texture2D texture = AssetLoader.TextureFromFile(path);
                    if (texture == null)
                    {
                        Plugin.Log.LogWarning("Could not load the poster texture for '" + id + "'.");
                        continue;
                    }

                    PosterObject poster = ObjectCreators.CreatePosterObject(texture, new PosterTextData[0]);
                    if (poster == null)
                    {
                        Plugin.Log.LogWarning("Could not create a PosterObject for '" + id + "'.");
                        continue;
                    }

                    poster.name = id;
                    table[id] = poster;
                    PosterRegistry.Add(poster);
                    added++;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Could not register poster '" + id + "': " + e.Message);
                }
            }

            if (added > 0)
            {
                Plugin.Log.LogInfo("Registered " + added + " poster(s) for the editor and generator.");
            }
        }

        private static Dictionary<string, PosterObject> PosterTable()
        {
            try
            {
                LevelLoaderPlugin instance = LevelLoaderPlugin.Instance;
                return instance == null ? null : instance.posterAliases;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not reach LevelLoaderPlugin.posterAliases: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Adds one poster tool per registered poster to the editor's poster category.
        /// PosterTool takes the poster's type name and the icon to draw in the palette.
        /// </summary>
        private static void AddPosterToolsToMode(EditorMode mode)
        {
            Dictionary<string, PosterObject> table = PosterTable();
            if (table == null || table.Count == 0)
            {
                return;
            }

            Sprite icon = null;
            try
            {
                string path = Path.Combine(ModPaths.Textures("EditorUI"), PosterIconFile);
                icon = File.Exists(path) ? ModPaths.LoadSprite(path, 25f) : null;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not load the poster tool icon: " + e.Message);
            }

            int added = 0;
            for (int i = 0; i < PosterIds.Length; i++)
            {
                string id = PosterIds[i];
                if (!table.ContainsKey(id))
                {
                    continue;
                }

                try
                {
                    var tool = icon == null
                        ? new PosterTool(id)
                        : new PosterTool(id, icon);

                    if (EditorInterfaceModes.AddToolToCategory(mode, PosterCategory, tool, true))
                    {
                        added++;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Could not add poster tool '" + id + "': " + e.Message);
                }
            }

            if (added > 0)
            {
                Plugin.Log.LogInfo("Added " + added + " SH20XX poster tool(s) to editor mode '" + mode.id + "'.");
            }
        }
    }
}
