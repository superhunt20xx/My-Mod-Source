using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Builds playable RoomAssets for the SH20XX rooms and injects them into level
    /// generation.
    ///
    /// Why the .rbpl layouts are not used: PlusLevelFormat expects
    ///   [version][width][height][Int32 nybbleCount][nybbles...]
    /// while the shipped files contain
    ///   [version][len+name][len+type][len+floor][len+wall][len+ceiling][cell data]
    /// Every offset fails to parse, so the private cell encoding cannot be
    /// reproduced. Each room is therefore built by cloning a vanilla RoomAsset,
    /// which guarantees a valid proven layout, and re-skinning it with this mod's
    /// textures.
    /// </summary>
    internal static class RoomBuilder
    {
        internal sealed class BuiltRoom
        {
            internal string Name;
            internal RoomCategory Category;
            internal RoomType Type;
            internal RoomAsset Asset;
            internal string TemplateName;
            internal string SourceFile;

            /// <summary>Source texture paths, kept so the editor can republish them by alias.</summary>
            internal string FloorPath;
            internal string WallPath;
            internal string CeilingPath;

            /// <summary>
            /// The door materials built for this room. The Plus Studio editor takes
            /// door art per room TYPE from PlusStudioLevelLoader.RoomSettings.doorMat,
            /// so this has to be retained to populate that table.
            /// </summary>
            internal StandardDoorMats DoorMats;

            internal Color Color = Color.white;
            internal string DoorSet = "ClassDoorSet";
        }

        internal static readonly List<BuiltRoom> Rooms = new List<BuiltRoom>();

        internal static void Build(List<RoomRegistration.RoomDefinition> definitions)
        {
            Rooms.Clear();

            List<RoomAsset> templates = Resources.FindObjectsOfTypeAll<RoomAsset>()
                .Where(r => r != null
                            && r.cells != null && r.cells.Count > 0
                            && r.type != RoomType.Hall
                            && r.category != RoomCategory.Null)
                .OrderBy(r => r.cells.Count)
                .ToList();

            if (templates.Count == 0)
            {
                Plugin.Log.LogError("No vanilla RoomAssets were available as room templates.");
                return;
            }

            Plugin.Log.LogInfo("Using " + templates.Count + " vanilla rooms as layout templates.");

            for (int i = 0; i < definitions.Count; i++)
            {
                BuiltRoom room = BuildOne(definitions[i], templates);
                if (room != null)
                {
                    Rooms.Add(room);
                }
            }
        }

        private static BuiltRoom BuildOne(RoomRegistration.RoomDefinition definition, List<RoomAsset> templates)
        {
            string floorPath = FindPath(definition, "floor", "flor");
            string wallPath = FindPath(definition, "wall");
            string ceilingPath = FindPath(definition, "ceiling", "ceil");

            Texture2D floor = ModPaths.LoadTexture(floorPath);
            Texture2D wall = ModPaths.LoadTexture(wallPath);
            Texture2D ceiling = ModPaths.LoadTexture(ceilingPath);

            if (floor == null && wall == null && ceiling == null)
            {
                Plugin.Log.LogWarning("Skipping room '" + definition.Name + "': no textures could be loaded.");
                return null;
            }

            int wanted = definition.Width > 0 && definition.Height > 0 ? definition.Width * definition.Height : 0;
            RoomAsset template = wanted > 0
                ? templates.OrderBy(r => Math.Abs(r.cells.Count - wanted)).First()
                : templates[templates.Count / 2];

            RoomAsset clone;
            try
            {
                clone = UnityEngine.Object.Instantiate(template);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not clone a template for '" + definition.Name + "': " + e.Message);
                return null;
            }

            clone.name = "SH20XX_Room_" + definition.Name;
            clone.category = definition.Category;
            clone.type = definition.Type;
            clone.spawnWeight = Mathf.Max(1, definition.SpawnWeight);
            clone.keepTextures = true;
            clone.hasActivity = false;
            clone.offLimits = false;

            if (floor != null) clone.florTex = floor;
            if (wall != null) clone.wallTex = wall;
            if (ceiling != null) clone.ceilTex = ceiling;

            StandardDoorMats doors = BuildDoorMats(definition);
            if (doors != null)
            {
                clone.doorMats = doors;
            }

            Plugin.Log.LogInfo(string.Format(
                "Built room '{0}' from template '{1}' ({2} cells, requested ~{3}) floor={4} wall={5} ceiling={6} doors={7}",
                definition.Name, template.name, clone.cells.Count, wanted,
                floor != null, wall != null, ceiling != null,
                doors == null ? "vanilla" : doors.name));

            return new BuiltRoom
            {
                Name = definition.Name,
                Category = definition.Category,
                Type = definition.Type,
                Asset = clone,
                TemplateName = template.name,
                SourceFile = definition.SourceFile,
                FloorPath = floorPath,
                WallPath = wallPath,
                CeilingPath = ceilingPath,
                DoorMats = doors,
                Color = definition.Color,
                DoorSet = definition.DoorSet,
            };
        }

        private static string FindPath(RoomRegistration.RoomDefinition definition, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                string path = definition.Texture(keys[i]);
                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Builds the room's door materials from its own door textures.
        ///
        /// ObjectCreators.CreateDoorDataObject(name, openTex, closeTex) clones a
        /// "DoorTemplate" StandardDoorMats and swaps the main texture of both
        /// materials, so the door uses the mod's art instead of falling back to a
        /// vanilla door material. That fallback is what left doors showing only the
        /// shared mask texture.
        ///
        /// Falls back to looking the json "doorSet" up by name when the room ships no
        /// door art, which is the case for TaserRoom.
        /// </summary>
        internal static StandardDoorMats BuildDoorMats(RoomRegistration.RoomDefinition definition)
        {
            // doorClosed/doorOpen are the pair; a lone "door" entry is used for both
            // sides so a single-texture room still gets its own door art.
            string closedPath = FindPath(definition, "doorClosed");
            string openPath = FindPath(definition, "doorOpen");

            if (closedPath == null && openPath == null)
            {
                string single = FindPath(definition, "door");
                closedPath = single;
                openPath = single;
            }

            Texture2D closed = ModPaths.LoadTexture(closedPath);
            Texture2D open = ModPaths.LoadTexture(openPath);

            if (closed != null || open != null)
            {
                if (open == null) open = closed;
                if (closed == null) closed = open;

                try
                {
                    StandardDoorMats built = MTM101BaldAPI.ObjectCreators.CreateDoorDataObject(
                        "SH20XX_Door_" + definition.Name, open, closed);

                    if (built != null)
                    {
                        Plugin.Log.LogInfo("Built door materials for '" + definition.Name +
                                           "' (shut=" + Path.GetFileName(closedPath) +
                                           ", open=" + Path.GetFileName(openPath) + ").");
                        return built;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Could not build door materials for '" + definition.Name + "': " + e.Message);
                }
            }

            StandardDoorMats vanilla = ResolveDoorMats(definition.DoorSet);
            if (vanilla == null)
            {
                Plugin.Log.LogWarning("Room '" + definition.Name + "' has no door art and doorSet '" +
                                      definition.DoorSet + "' did not resolve; doors will use the default.");
            }
            else
            {
                Plugin.Log.LogInfo("Room '" + definition.Name + "' has no door art; using vanilla doorSet '" +
                                   definition.DoorSet + "'.");
            }

            return vanilla;
        }

        internal static StandardDoorMats ResolveDoorMats(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            StandardDoorMats[] all = Resources.FindObjectsOfTypeAll<StandardDoorMats>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && string.Equals(all[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return all[i];
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ injection

        /// <summary>
        /// Adds the built rooms to the level that is about to be generated.
        ///
        /// This works on LevelObject and then re-runs
        /// LevelGenerationParameters.AssignData, because AssignData copies the room
        /// pools out of the LevelObject. Injecting into the LevelObject alone (or
        /// into a replaced __result) has no effect on a builder whose ld was already
        /// assigned.
        ///
        /// The LevelObject is always cloned first: these are shared game assets, so
        /// mutating the original would add the rooms again on every later level.
        /// </summary>
        /// <returns>True when rooms were injected.</returns>
        internal static bool InjectIntoLevelBuilder(LevelBuilder builder, LevelObject mezzanineLevel)
        {
            if (builder == null || Rooms.Count == 0)
            {
                return false;
            }

            LevelObject source = mezzanineLevel ?? CurrentLevelObject(builder.scene);
            if (source == null)
            {
                Plugin.Log.LogWarning("No source LevelObject available for room injection.");
                return false;
            }

            LevelObject clone;
            try
            {
                clone = UnityEngine.Object.Instantiate(source);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not clone LevelObject for injection: " + e.Message);
                return false;
            }

            var additions = new List<WeightedRoomAsset>();
            for (int i = 0; i < Rooms.Count; i++)
            {
                additions.Add(new WeightedRoomAsset
                {
                    selection = Rooms[i].Asset,
                    weight = Mathf.Max(1, Rooms[i].Asset.spawnWeight),
                });
            }

            var special = new List<WeightedRoomAsset>(clone.potentialSpecialRooms ?? new WeightedRoomAsset[0]);
            special.AddRange(additions);
            clone.potentialSpecialRooms = special.ToArray();

            int grouped = 0;
            if (clone.roomGroup != null)
            {
                for (int g = 0; g < clone.roomGroup.Length; g++)
                {
                    RoomGroup group = clone.roomGroup[g];
                    if (group == null)
                    {
                        continue;
                    }

                    var pool = new List<WeightedRoomAsset>(group.potentialRooms ?? new WeightedRoomAsset[0]);
                    for (int a = 0; a < additions.Count; a++)
                    {
                        if (additions[a].selection.category == CategoryFromName(group.name))
                        {
                            pool.Add(additions[a]);
                            grouped++;
                        }
                    }

                    group.potentialRooms = pool.ToArray();
                }
            }

            // Re-assign so the room pools actually reach the generator.
            var parameters = new LevelGenerationParameters();
            parameters.AssignData(clone, null);
            builder.ld = parameters;

            Plugin.Log.LogInfo(string.Format(
                "Injected {0} SH20XX room(s) into '{1}' (source '{2}', special={3}, grouped={4}).",
                additions.Count, builder.scene == null ? "?" : builder.scene.name,
                source.name, additions.Count, grouped));

            return true;
        }

        /// <summary>
        /// Picks the LevelObject the scene would have generated: the fixed one when
        /// present, otherwise the first randomized entry's selection.
        /// </summary>
        private static LevelObject CurrentLevelObject(SceneObject scene)
        {
            if (scene == null)
            {
                return null;
            }

            if (scene.levelObject != null)
            {
                return scene.levelObject;
            }

            if (scene.randomizedLevelObject != null && scene.randomizedLevelObject.Length > 0)
            {
                WeightedLevelObject first = scene.randomizedLevelObject[0];
                if (first != null && first.selection != null)
                {
                    return first.selection;
                }
            }

            return null;
        }

        private static RoomCategory CategoryFromName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return RoomCategory.Null;
            }

            string[] names = Enum.GetNames(typeof(RoomCategory));
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return (RoomCategory)Enum.Parse(typeof(RoomCategory), names[i]);
                }
            }

            return RoomCategory.Null;
        }
    }
}
