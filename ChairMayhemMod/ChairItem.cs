using UnityEngine;

namespace Superhunt.ChairMayhem
{
    /// <summary>
    /// Inventory item created when a chair is picked up. Using the item throws
    /// a physical chair in the direction the player is facing.
    /// </summary>
    public sealed class ChairItem : Item
    {
        public override bool Use(PlayerManager pm)
        {
            bool thrown = Plugin.TryThrowChair(pm);
            Object.Destroy(gameObject);
            return thrown;
        }
    }
}
