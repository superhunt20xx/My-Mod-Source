namespace SH20XX.BBPP
{
    /// <summary>
    /// Cirno - "Baka Fumo toy".
    ///
    /// Squeeze it and it screams: the noise is what pulls characters toward the player.
    /// All four toys share this behaviour - see ScreamSound - the flavour text is what
    /// differs.
    /// </summary>
    public sealed class ITM_Cirno : Item
    {
        public override bool Use(PlayerManager player)
        {
            return ScreamSound.Play(player);
        }
    }
}
