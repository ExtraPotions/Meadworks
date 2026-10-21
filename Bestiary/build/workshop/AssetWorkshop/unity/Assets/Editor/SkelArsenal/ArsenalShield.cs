using UnityEngine;
using Workshop.Greataxe;

namespace Workshop.SkelArsenal
{
    /// <summary>
    /// The Kraken shield on a player's left arm, placed as the mod places it (EliteCreaturesPack Kraken/Loot/KrakenShield):
    /// the game hangs a shield's attach child at LeftHand_Attach's origin, and the mod lays the shield's model in it with
    /// the silver shield's model turn (ShieldSilver attach/model in the reference export) and its grip at the origin.
    /// </summary>
    public static class ArsenalShield
    {
        public const string Prefab = "Assets/Bundles/ecp_kraken_loot/ecp_kraken_beak_shield/ecp_kraken_beak_shield.prefab";
        private static readonly Quaternion Turn = new Quaternion(0.69485027f, 0.14763822f, 0.0035652146f, 0.7038277f);
        private static readonly Vector3 Grip = new Vector3(0f, 0.565f, -0.16f);   // KrakenShield.Grip, the one grip, in the model's space

        public static void Mount(GameObject shield, GameObject player)
        {
            Transform model = shield.transform;
            model.SetParent(GreataxePlayer.Bone(player, "LeftHand_Attach"), false);
            (model.localRotation, model.localPosition, model.localScale) = (Turn, -(Turn * Grip), Vector3.one);
        }
    }
}
