using MTM101BaldAPI.Components;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Potion - "Drink it and you'll be immune to Baldi's BUZZ."
    /// Grants a short burst of speed and stamina so the player can outrun Baldi.
    /// </summary>
    public sealed class ITM_Potion : Item
    {
        private const float SpeedMultiplier = 1.6f;
        private const float Duration = 8f;

        public override bool Use(PlayerManager player)
        {
            if (player == null || player.plm == null || player.plm.am == null)
            {
                return false;
            }

            ActivityModifier modifier = player.plm.am;
            modifier.AddTemporaryModification(Vector3.zero, SpeedMultiplier);

            player.plm.AddStamina(player.plm.staminaMax, false);
            player.ec.MakeNoise(player.transform.position, 15, false);

            Plugin.Instance.StartCoroutine(Expire(modifier, Duration));
            return true;
        }

        private static System.Collections.IEnumerator Expire(ActivityModifier modifier, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (modifier != null)
            {
                modifier.AddTemporaryModification(Vector3.zero, 1f / SpeedMultiplier);
            }
        }
    }
}
