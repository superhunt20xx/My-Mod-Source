using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Test Paper - "You can use this to make Baldi stop and admire the test papers you stole."
    /// Freezes Baldi in place for a few seconds, but he remembers it.
    /// </summary>
    public sealed class ITM_TestPaper : Item
    {
        private const float FreezeTime = 6f;
        private const float CooldownTime = 45f;

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

            Baldi baldi = player.ec.GetBaldi();
            if (baldi == null || baldi.Navigator == null)
            {
                return false;
            }

            lastUsedAt = Time.timeSinceLevelLoad;

            baldi.Navigator.ClearDestination();
            baldi.Navigator.SetSpeed(0f);

            Plugin.Instance.StartCoroutine(Release(baldi, FreezeTime));
            return true;
        }

        private static System.Collections.IEnumerator Release(Baldi baldi, float delay)
        {
            yield return new MTM101BaldAPI.WaitForSecondsNPCTimescale(baldi, delay);

            if (baldi != null && baldi.Navigator != null)
            {
                baldi.Navigator.SetSpeed(1f);
            }
        }
    }
}
