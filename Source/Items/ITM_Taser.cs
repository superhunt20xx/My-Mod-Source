using System.Collections.Generic;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Taser - "With it even the gods have to yield to you."
    /// Shocks every nearby character, making them lose their path for a moment.
    /// </summary>
    public sealed class ITM_Taser : Item
    {
        private const float Range = 18f;
        private const float StunTime = 1.4f;

        private const float CooldownTime = 30f;

        private float lastUsedAt = -999f;

        public override bool Use(PlayerManager player)
        {
            if (player == null || player.ec == null)
            {
                return false;
            }

            if (Time.timeSinceLevelLoad - lastUsedAt < CooldownTime)
            {
                return false;
            }

            PlayShockSound();

            bool hitAnything = false;
            Vector3 origin = player.transform.position;
            List<NPC> npcs = player.ec.Npcs;

            for (int i = 0; i < npcs.Count; i++)
            {
                NPC npc = npcs[i];
                if (npc == null || npc.Navigator == null)
                {
                    continue;
                }

                if (Vector3.Distance(origin, npc.transform.position) > Range)
                {
                    continue;
                }

                npc.Navigator.ClearDestination();
                npc.Navigator.SetSpeed(0f);
                Plugin.Instance.StartCoroutine(RestoreSpeed(npc, StunTime));
                hitAnything = true;
            }

            if (hitAnything)
            {
                lastUsedAt = Time.timeSinceLevelLoad;
                player.ec.MakeNoise(origin, 60, false);
            }

            return true;
        }

        private static System.Collections.IEnumerator RestoreSpeed(NPC npc, float delay)
        {
            yield return new MTM101BaldAPI.WaitForSecondsNPCTimescale(npc, delay);

            if (npc != null && npc.Navigator != null)
            {
                npc.Navigator.SetSpeed(1f);
            }
        }

        private static void PlayShockSound()
        {
            if (CoreGameManager.Instance == null || CoreGameManager.Instance.audMan == null)
            {
                return;
            }

            AudioClip clip = ModPaths.LoadAudio(ModPaths.FindFile(
                "Electric shock sound placeholder.wav",
                ModPaths.Audio("Sounds"),
                ModPaths.Root));

            if (clip == null)
            {
                return;
            }

            SoundObject sound = MTM101BaldAPI.ObjectCreators.CreateSoundObject(
                clip,
                "SH20XX_Taser_Shock",
                SoundType.Effect,
                Color.yellow,
                0f);

            sound.subtitle = false;
            CoreGameManager.Instance.audMan.PlaySingle(sound);
        }
    }
}
