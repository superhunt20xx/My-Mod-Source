using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// 99 Cassettes - a box full of cassettes.
    /// Every use digs one more cassette out of the box and drops it where the player is standing.
    /// </summary>
    public sealed class ITM_99Cassettes : Item
    {
        private const int CassettesPerBox = 3;

        private int cassettesLeft = CassettesPerBox;

        public override bool Use(PlayerManager player)
        {
            if (player == null || player.ec == null || cassettesLeft <= 0)
            {
                return false;
            }

            cassettesLeft--;

            ItemObject cassette = FindCassetteItem();
            if (cassette == null)
            {
                return false;
            }

            Vector3 position = player.transform.position;
            player.ec.CreateItem(
                player.ec.mainHall,
                cassette,
                new Vector2(position.x, position.z));

            player.ec.MakeNoise(position, 30, false);
            return true;
        }

        private static ItemObject FindCassetteItem()
        {
            MTM101BaldAPI.Registers.ItemMetaData meta =
                MTM101BaldAPI.Registers.ItemMetaStorage.Instance.FindByEnum(Items.Tape);

            return meta == null ? null : meta.value;
        }
    }
}
