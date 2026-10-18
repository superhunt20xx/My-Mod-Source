using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Superhunt.ChairMayhem
{
    internal static class ChairDamage
    {
        private static readonly MethodInfo EndSequenceMethod =
            AccessTools.Method(typeof(CoreGameManager), "EndSequence");

        private static bool _jumpscareActive;

        internal static void ResetForLevel()
        {
            _jumpscareActive = false;
        }

        internal static void Hit(NPC npc, PlayerManager owner)
        {
            if (npc == null || owner == null || npc.ec == null)
            {
                return;
            }

            ChairAggro aggro = npc.GetComponent<ChairAggro>();
            if (aggro == null)
            {
                aggro = npc.gameObject.AddComponent<ChairAggro>();
            }

            aggro.RegisterHit(owner);

            if (Plugin.NullHitClip != null)
            {
                AudioSource.PlayClipAtPoint(Plugin.NullHitClip, npc.transform.position, 0.9f);
            }
        }

        internal static void Jumpscare(PlayerManager player, NPC npc)
        {
            if (_jumpscareActive || player == null || npc == null)
            {
                return;
            }

            CoreGameManager core = Singleton<CoreGameManager>.Instance;
            if (core == null)
            {
                return;
            }

            _jumpscareActive = true;

            // Put the attacker right in front of the camera, like Baldi's
            // jumpscare, instead of showing a distant character.
            Vector3 forward = player.transform.forward;
            Vector3 position = player.transform.position + forward * 4f;
            position.y = player.transform.position.y;
            npc.transform.position = position;
            npc.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);

            if (npc.Entity != null)
            {
                npc.Entity.SetFrozen(true);
            }

            if (npc.Navigator != null)
            {
                npc.Navigator.SetSpeed(0f);
            }

            Time.timeScale = 0f;
            Singleton<MusicManager>.Instance.StopMidi();
            core.disablePause = true;

            GameCamera camera = core.GetCamera(player.playerNumber);
            camera.UpdateTargets(npc.transform, 0);
            camera.offestPos =
                (player.transform.position - npc.transform.position).normalized * 2f + Vector3.up;
            camera.SetControllable(false);
            camera.matchTargetRotation = false;

            core.audMan.volumeModifier = 0.6f;
            Baldi baldi = player.ec != null ? player.ec.GetBaldi() : null;

            if (baldi != null && baldi.loseSounds != null && baldi.loseSounds.Length > 0)
            {
                SoundObject loseSound = WeightedSelection<SoundObject>.RandomSelection(baldi.loseSounds);
                if (loseSound != null)
                {
                    core.audMan.PlaySingle(loseSound);
                }
            }
            else if (Plugin.NullHitClip != null)
            {
                AudioSource.PlayClipAtPoint(Plugin.NullHitClip, player.transform.position, 1f);
            }

            Singleton<InputManager>.Instance.Rumble(1f, 2f);

            if (EndSequenceMethod == null)
            {
                Plugin.Log.LogError("Could not find CoreGameManager.EndSequence; using the normal EndGame fallback.");
                Time.timeScale = 1f;

                if (baldi != null)
                {
                    core.EndGame(player.transform, baldi);
                }

                return;
            }

            try
            {
                IEnumerator sequence = EndSequenceMethod.Invoke(core, null) as IEnumerator;
                if (sequence != null)
                {
                    core.StartCoroutine(sequence);
                }
                else
                {
                    Plugin.Log.LogError("CoreGameManager.EndSequence returned null.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Custom jumpscare failed: {ex}");
                Time.timeScale = 1f;

                if (baldi != null)
                {
                    core.EndGame(player.transform, baldi);
                }
            }
        }
    }
}
