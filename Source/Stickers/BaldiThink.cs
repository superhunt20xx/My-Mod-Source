using System;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Makes Baldi zone out: every roll has a 56% chance to start a 10 second think,
    /// during which the thinking pose is shown.
    ///
    /// This is separate from the sticker registration on purpose. A registered sticker
    /// only grants the upgrade; nothing in the base game reads it, so the behaviour has
    /// to be driven here.
    ///
    /// The pose is animated by writing NPC.spriteRenderer directly rather than using
    /// Baldi's Animator, because the thinking frames are not part of his animation
    /// controller and forcing an unknown state name on it could break his chase pose.
    /// The original sprites are saved and put back when the think ends.
    /// </summary>
    public sealed class BaldiThink : MonoBehaviour
    {
        /// <summary>How long to wait between rolls, in seconds.</summary>
        private const float RollInterval = 8f;

        /// <summary>Frame rate of the thinking animation.</summary>
        private const float FramesPerSecond = 2f;

        private NPC npc;
        private SpriteRenderer[] renderers;
        private Sprite[] original;
        private Sprite[] frames;

        private float rollTimer;
        private float thinkTimer;
        private float frameTimer;
        private int frame;

        private bool thinking;

        /// <summary>Set up once by the patch that adds this component.</summary>
        internal void Initialize(NPC target, Sprite[] thinkingFrames)
        {
            npc = target;
            frames = thinkingFrames;

            if (npc != null && npc.spriteRenderer != null)
            {
                renderers = npc.spriteRenderer;
                original = new Sprite[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    original[i] = renderers[i] == null ? null : renderers[i].sprite;
                }
            }

            // First roll after a short grace period so a level does not open with Baldi
            // immediately zoning out.
            rollTimer = RollInterval;
        }

        private void Update()
        {
            // Only in a real game with a live player; the menu and editor have none.
            if (npc == null || renderers == null || frames == null || frames.Length == 0)
            {
                enabled = false;
                return;
            }

            float delta = Time.deltaTime;

            if (thinking)
            {
                AdvanceThink(delta);
                return;
            }

            rollTimer -= delta;
            if (rollTimer > 0f)
            {
                return;
            }

            rollTimer = RollInterval;

            if (UnityEngine.Random.value < ThinkSticker.ThinkChance)
            {
                BeginThink();
            }
        }

        private void BeginThink()
        {
            thinking = true;
            thinkTimer = ThinkSticker.ThinkSeconds;
            frameTimer = 0f;
            frame = 0;

            ApplyFrame();
        }

        private void AdvanceThink(float delta)
        {
            thinkTimer -= delta;

            frameTimer -= delta;
            if (frameTimer <= 0f)
            {
                frameTimer = 1f / FramesPerSecond;
                frame = (frame + 1) % frames.Length;
                ApplyFrame();
            }

            if (thinkTimer <= 0f)
            {
                EndThink();
            }
        }

        private void ApplyFrame()
        {
            Sprite sprite = frames[frame];
            if (sprite == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sprite = sprite;
                }
            }
        }

        /// <summary>Restores whatever pose Baldi had before he started thinking.</summary>
        private void EndThink()
        {
            thinking = false;

            for (int i = 0; i < renderers.Length && i < original.Length; i++)
            {
                if (renderers[i] != null && original[i] != null)
                {
                    renderers[i].sprite = original[i];
                }
            }
        }

        private void OnDisable()
        {
            // Leaving the level mid-think must not strand the pose.
            if (thinking)
            {
                EndThink();
            }
        }
    }
}
