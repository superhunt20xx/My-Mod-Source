using UnityEngine;

namespace Superhunt.ChairMayhem
{
    /// <summary>
    /// A clickable marker placed on a classroom chair (or on a thrown chair
    /// that has landed). Looking at it and pressing the interact key picks it up.
    /// </summary>
    public sealed class ChairPickup : MonoBehaviour, IClickable<int>
    {
        private GameObject _chairRoot;
        private ItemObject _item;

        public void Initialize(GameObject chairRoot, ItemObject item)
        {
            _chairRoot = chairRoot;
            _item = item;
        }

        public void Clicked(int player)
        {
            if (_chairRoot == null || _item == null)
            {
                return;
            }

            CoreGameManager core = Singleton<CoreGameManager>.Instance;
            if (core == null)
            {
                return;
            }

            PlayerManager playerManager = core.GetPlayer(player);
            if (playerManager == null || playerManager.itm == null)
            {
                return;
            }

            if (playerManager.itm.InventoryFull())
            {
                return;
            }

            playerManager.itm.AddItem(_item);
            _chairRoot.SetActive(false);
        }

        public void ClickableSighted(int player)
        {
        }

        public void ClickableUnsighted(int player)
        {
        }

        public bool ClickableHidden()
        {
            return false;
        }

        public bool ClickableRequiresNormalHeight()
        {
            return true;
        }
    }
}
