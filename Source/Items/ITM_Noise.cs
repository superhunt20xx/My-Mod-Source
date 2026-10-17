namespace SH20XX.BBPP
{
    /// <summary>
    /// Noise - "Woah". Pressing E screams and draws characters in; see ScreamSound for
    /// the shared behaviour.
    /// </summary>
    public sealed class ITM_Noise : Item
    {
        public override bool Use(PlayerManager player)
        {
            return ScreamSound.Play(player);
        }
    }
}
