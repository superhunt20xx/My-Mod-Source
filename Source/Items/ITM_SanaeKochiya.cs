namespace SH20XX.BBPP
{
    /// <summary>
    /// SanaeKochiya - "Cute toy". Pressing E screams and draws characters in; see
    /// ScreamSound for the shared behaviour.
    /// </summary>
    public sealed class ITM_SanaeKochiya : Item
    {
        public override bool Use(PlayerManager player)
        {
            return ScreamSound.Play(player);
        }
    }
}
