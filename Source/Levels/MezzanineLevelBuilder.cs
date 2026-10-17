using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Builds a playable Mezzanine floor by cloning a vanilla level layout and
    /// re-skinning it with the Mezzanine textures.
    ///
    /// The hand-placed layout in Data/Levels/Mezzanine/Mezzanine_Room_01.rbpl
    /// cannot be recovered: it uses a legacy encoding that PlusLevelFormat rejects
    /// at every byte offset, and the private cell encoding cannot be reproduced.
    /// What IS reusable are the four 256x256 textures, so they are applied over a
    /// vanilla layout instead.
    /// </summary>
    internal static class MezzanineLevelBuilder
    {
        internal const string LevelName = "Mezzanine";

        private const string TextureFolder = "Level";

        /// <summary>
        /// Size hint for template selection; the room JSON asks for 10 x 10.
        /// </summary>
        private const int PreferredMinSize = 40;

        private const int DefaultWeight = 50;

        /// <summary>Mezzanine has five floors, shown in the elevator as -1 .. -5.</summary>
        internal const int FloorCount = 5;

        /// <summary>LevelObject's ctor default is 10; the requested value is 5.</summary>
        private const int LightStrength = 5;

        /// <summary>
        /// Spacing between hall lights. The ctor default is 8. Raising it is the only
        /// lever available for reducing the light count, because no per-floor cap is
        /// exposed on LevelObject (there is no "max lights" field).
        /// </summary>
        private const int LightSpacing = 24;

        internal static SceneObject Scene { get; private set; }

        /// <summary>Every Mezzanine floor; index 0 is floor -1.</summary>
        internal static readonly List<SceneObject> Scenes = new List<SceneObject>();

        internal static LevelObject Level { get; private set; }

        internal static void Build(Plugin mod)
        {
            if (mod == null)
            {
                return;
            }

            Texture2D floor = ModPaths.LoadTexture(ModPaths.Textures(TextureFolder, LevelName, "Mezzanine_Floor.png"));
            Texture2D wall = ModPaths.LoadTexture(ModPaths.Textures(TextureFolder, LevelName, "Mezzanine_Wall.png"));
            Texture2D ceiling = ModPaths.LoadTexture(ModPaths.Textures(TextureFolder, LevelName, "Mezzanine_Ceiling.png"));

            if (floor == null || wall == null || ceiling == null)
            {
                Plugin.Log.LogError(
                    "Mezzanine textures are missing; the level cannot be built. Expected " +
                    "Textures/Level/Mezzanine/Mezzanine_{Floor,Wall,Ceiling}.png");
                return;
            }

            List<LevelObject> templates = Resources.FindObjectsOfTypeAll<LevelObject>()
                .Where(l => l != null && l.roomGroup != null && l.roomGroup.Length > 0)
                .ToList();

            if (templates.Count == 0)
            {
                Plugin.Log.LogError("No vanilla LevelObjects were available as a Mezzanine template.");
                return;
            }

            LevelObject template = templates
                .OrderBy(l => Math.Abs(l.minSize.x - PreferredMinSize))
                .First();

            Plugin.Log.LogInfo(string.Format(
                "Mezzanine template '{0}' (type {1}, minSize {2}x{3}, {4} room groups).",
                template.name, template.type, template.minSize.x, template.minSize.z, template.roomGroup.Length));

            LevelObject level;
            try
            {
                level = UnityEngine.Object.Instantiate(template);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not clone the Mezzanine level template: " + e.Message);
                return;
            }

            level.name = "SH20XX_Mezzanine_Level";
            level.type = LevelRegistration.MezzanineType;

            level.hallWallTexs = Single(wall, WeightOf(level.hallWallTexs));
            level.hallFloorTexs = Single(floor, WeightOf(level.hallFloorTexs));
            level.hallCeilingTexs = Single(ceiling, WeightOf(level.hallCeilingTexs));

            // Room groups carry their own texture sets; without overriding these the
            // rooms would still look like the vanilla floor.
            for (int g = 0; g < level.roomGroup.Length; g++)
            {
                RoomGroup group = level.roomGroup[g];
                if (group == null)
                {
                    continue;
                }

                group.wallTexture = Single(wall, WeightOf(group.wallTexture));
                group.floorTexture = Single(floor, WeightOf(group.floorTexture));
                group.ceilingTexture = Single(ceiling, WeightOf(group.ceilingTexture));

                // An empty group can dead-end generation.
                if (group.potentialRooms == null || group.potentialRooms.Length == 0)
                {
                    group.minRooms = 0;
                    group.maxRooms = 0;
                }
            }

            // Lighting. standardLightStrength defaults to 10 in LevelObject's ctor, so 5
            // is a deliberate halving. maxLightDistance is the spacing between hall
            // lights (ctor default 8).
            level.standardLightStrength = LightStrength;
            level.standardLightColor = Color.white;
            level.maxLightDistance = LightSpacing;

            Scenes.Clear();
            SceneObject previous = null;

            for (int i = 0; i < FloorCount; i++)
            {
                SceneObject scene;
                try
                {
                    scene = ScriptableObject.CreateInstance<SceneObject>();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("Could not create a Mezzanine SceneObject: " + e.Message);
                    return;
                }

                // levelTitle is exactly what ElevatorScreen.UpdateFloorDisplay shows,
                // so this is what turns the elevator label into -1 .. -5.
                string title = "-" + (i + 1);

                scene.name = "SH20XX_Mezzanine_" + (i + 1);
                scene.levelTitle = title;
                scene.nameKey = "SH20XX_Level_" + LevelName;
                // Floor 0 reuses the configured level; the rest get copies so later
                // per-floor tweaks stay independent.
                scene.levelObject = i == 0 ? level : UnityEngine.Object.Instantiate(level);
                scene.levelNo = i + 1;
                scene.skyboxColor = Color.white;
                scene.mapPrice = level.mapPrice;
                scene.totalShopItems = level.totalShopItems;
                scene.shopItems = level.shopItems;

                if (previous != null)
                {
                    previous.nextLevel = scene;
                }

                previous = scene;
                Scenes.Add(scene);
            }

            Level = level;
            Scene = Scenes[0];

            Plugin.Log.LogInfo(string.Format(
                "Built {0} Mezzanine floors (titles {1}..{2}) from template '{3}'; light strength {4}, spacing {5}.",
                Scenes.Count, Scenes[0].levelTitle, Scenes[Scenes.Count - 1].levelTitle,
                template.name, LightStrength, LightSpacing));
        }

        /// <summary>
        /// Reuses the template's first weight so the replacement keeps the original
        /// balance rather than inventing one.
        /// </summary>
        private static int WeightOf(WeightedTexture2D[] source)
        {
            if (source != null && source.Length > 0 && source[0] != null && source[0].weight > 0)
            {
                return source[0].weight;
            }

            return DefaultWeight;
        }

        private static WeightedTexture2D[] Single(Texture2D texture, int weight)
        {
            return new[] { new WeightedTexture2D { selection = texture, weight = weight } };
        }
    }
}
