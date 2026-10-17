using System;
using System.IO;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI.ObjectCreation;
using MTM101BaldAPI.Registers;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// The "Thinking..." sticker: Baldi zones out, giving the player a window to move.
    ///
    ///   Value : Baldi has a 56% chance to think for 10 seconds.
    ///
    /// The sticker itself is registered through MTM101's StickerBuilder, which is what
    /// makes it appear in sticker packets and resolve its text. The sticker's localized
    /// strings are looked up as "StickerTitle_{enum}" and "StickerDescription_{enum}",
    /// so the enum name has to match the Localization.json keys exactly: the entries are
    /// StickerTitle_thinkStickers / StickerDescription_thinkStickers.
    ///
    /// The behaviour is attached to Baldi by BaldiThink, which is what actually makes him
    /// stop; registering a sticker alone only grants the upgrade.
    /// </summary>
    internal static class ThinkSticker
    {
        /// <summary>Must match the StickerTitle_/StickerDescription_ suffixes.</summary>
        internal const string StickerName = "thinkStickers";

        /// <summary>The sheet is a 2x2 grid of 256x256 frames.</summary>
        private const int FrameColumns = 2;
        private const int FrameRows = 2;

        /// <summary>Chance to start thinking when the roll comes up.</summary>
        internal const float ThinkChance = 0.56f;

        /// <summary>How long a think lasts, in seconds.</summary>
        internal const float ThinkSeconds = 10f;

        private static ExtendedStickerData sticker;
        private static Sprite[] frames;

        /// <summary>Registers the sticker. Called once during content registration.</summary>
        internal static void Register(Plugin mod)
        {
            if (mod == null || sticker != null)
            {
                return;
            }

            frames = LoadFrames();

            try
            {
                Sprite icon = frames != null && frames.Length > 0 ? frames[0] : null;

                sticker = new StickerBuilder<ExtendedStickerData>(mod.Info)
                    .SetEnum(StickerName)
                    .SetSprite(icon)
                    // Rarity for the 100-600 YTP packet band.
                    //
                    // The packet's own price is fixed on its ITM_StickerPack object, so a
                    // sticker cannot opt into a price itself. What it can control is how
                    // often it is drawn: SetValueCap stops copies stacking value, and
                    // SetDuplicateOddsMultiplier lowers the chance of repeats. Both
                    // defaults are wide open, which would make a behaviour sticker like
                    // this turn up constantly in cheap packets.
                    .SetValueCap(2)
                    .SetDuplicateOddsMultiplier(0.4f)
                    .SetTags(new[] { "sh20xx", "sticker" })
                    .Build();

                if (sticker == null)
                {
                    Plugin.Log.LogWarning("Thinking sticker: the builder returned null.");
                    return;
                }

                Plugin.Log.LogInfo("Registered sticker '" + StickerName + "' (" +
                                   (frames == null ? 0 : frames.Length) + " frame(s), " +
                                   (int)(ThinkChance * 100f) + "% for " + ThinkSeconds + "s).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Thinking sticker: could not register: " + e);
            }
        }

        /// <summary>The sprite frames of the thinking pose, for BaldiThink to cycle.</summary>
        internal static Sprite[] Frames
        {
            get { return frames; }
        }

        /// <summary>
        /// Slices Textures/Stickers/thinkStickers.png into its four frames. The sheet is
        /// a 2x2 grid, so each frame is a quarter of the image.
        /// </summary>
        private static Sprite[] LoadFrames()
        {
            try
            {
                string path = ModPaths.FindFile("thinkStickers.png",
                    ModPaths.Textures("Stickers"),
                    ModPaths.Textures());

                if (path == null)
                {
                    Plugin.Log.LogWarning("Thinking sticker: thinkStickers.png was not found.");
                    return null;
                }

                Texture2D texture = AssetLoader.TextureFromFile(path);
                if (texture == null)
                {
                    Plugin.Log.LogWarning("Thinking sticker: could not read " + path + ".");
                    return null;
                }

                int width = texture.width / FrameColumns;
                int height = texture.height / FrameRows;
                var result = new Sprite[FrameColumns * FrameRows];

                int index = 0;
                for (int row = 0; row < FrameRows; row++)
                {
                    for (int col = 0; col < FrameColumns; col++)
                    {
                        // Sheets are laid out top-left first, but texture space starts at
                        // the bottom, so the row is flipped.
                        float x = col * width;
                        float y = texture.height - (row + 1) * height;

                        result[index] = Sprite.Create(texture, new Rect(x, y, width, height),
                            new Vector2(0.5f, 0.5f), 25f);
                        index++;
                    }
                }

                return result;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Thinking sticker: could not slice the sheet: " + e.Message);
                return null;
            }
        }
    }
}
