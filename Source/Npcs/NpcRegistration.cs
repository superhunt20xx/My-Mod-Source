using MTM101BaldAPI.ObjectCreation;
using MTM101BaldAPI.Registers;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Registers the SH20XX characters.
    ///
    /// Every character is built from the vanilla "TemplateNPC" prefab and then given
    /// its own sprite and character enum entry, exactly how the public API expects it.
    /// </summary>
    internal static class NpcRegistration
    {
        internal static readonly System.Collections.Generic.Dictionary<string, NPC> Npcs =
            new System.Collections.Generic.Dictionary<string, NPC>();

        internal static void Register(Plugin mod)
        {
            RegisterNpc(
                mod,
                "BaldiCorona",
                "Baldi (Corona)",
                ModPaths.Textures("NPC", "Baldi", "Bal_corona.png"),
                "Bal_corona",
                Color.red);

            RegisterNpc(
                mod,
                "Salesperson",
                "Salesperson",
                ModPaths.Textures("NPC", "idk", "NPC_salesperson1.png"),
                "salesperson1",
                Color.yellow);

            RegisterNpc(
                mod,
                "YuLuoLin",
                "YuLuoLin",
                ModPaths.Textures("NPC", "YuLuoLin", "NPC_YuLuoLin.png"),
                "YuLuoLin",
                Color.magenta);
        }

        private static void RegisterNpc(Plugin mod, string enumName, string displayName, string spritePath, string voiceFolder, Color color)
        {
            Texture2D texture = ModPaths.LoadTexture(spritePath);
            if (texture == null)
            {
                Plugin.Log.LogWarning("Skipping NPC '" + enumName + "' because its sprite is missing.");
                return;
            }

            NPC npc = new NPCBuilder<NPC>(mod.Info)
                .SetEnum(enumName)
                .SetName(displayName)
                .SetMetaName(displayName)
                .SetPoster(texture, displayName, "A strange student wandering the school.")
                .SetMinMaxAudioDistance(5f, 90f)
                .AddLooker()
                .AddTrigger()
                .SetWanderEnterRooms()
                .AddSpawnableRoomCategories(new[] { RoomCategory.Hall, RoomCategory.Class, RoomCategory.Faculty, RoomCategory.Office })
                .AddMetaFlag(NPCFlags.CanSee)
                .SetMetaTags(new[] { "sh20xx", "npc" })
                .Build();

            ApplySprite(npc, texture);
            Npcs[enumName] = npc;

            Plugin.Log.LogInfo("Registered NPC " + displayName + " (" + enumName + ").");
        }

        /// <summary>
        /// NPCBuilder clones TemplateNPC, so the sprite has to be pushed into the
        /// cloned renderers by hand.
        /// </summary>
        private static void ApplySprite(NPC npc, Texture2D texture)
        {
            if (npc == null || npc.spriteRenderer == null)
            {
                return;
            }

            Sprite sprite = MTM101BaldAPI.AssetTools.AssetLoader.SpriteFromTexture2D(texture, 25f);
            if (sprite == null)
            {
                return;
            }

            for (int i = 0; i < npc.spriteRenderer.Length; i++)
            {
                SpriteRenderer renderer = npc.spriteRenderer[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.sprite = sprite;
            }
        }
    }
}
