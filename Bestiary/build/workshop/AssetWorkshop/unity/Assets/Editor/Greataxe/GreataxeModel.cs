using UnityEditor;
using UnityEngine;
using Workshop.Headsman;

namespace Workshop.Greataxe
{
    /// <summary>
    /// The Executioner's Greataxe as a player holds it, in the frame of the hand's attach point (RightHand_Attach), built
    /// the way the game's Battleaxe is (Battleaxe.prefab: attach/battleaxe turned (0, -0.707, -0.707, 0), its haft up +Z
    /// from 0.30 m below the fist to 1.40 m above it, the blade out along +X): the headsman's bone greataxe
    /// (<see cref="HeadsmanAxe"/>: butt at the origin, haft up +Y, blade out along -X) scaled to the Battleaxe's 1.7 m
    /// and turned the same way, but held near the butt as a greataxe is (the Battleaxe's clips put the left fist 0.77 m
    /// up the haft, which on this axe's longer head would be on the blade) and its S-curved haft aimed through that left
    /// fist. The left hand is then kept on the haft by <see cref="GreataxeGrip"/>.
    /// </summary>
    public static class GreataxeModel
    {
        public const string Name = "ecp_greataxe";
        public const float Length = 1.7f;

        /// <summary>Height up the model's haft the right fist holds (the lowest vertebra above the butt's point).</summary>
        public const float FistHeight = 0.08f;

        /// <summary>The Battleaxe model's turn in its attach frame: +Y up the haft to +Z, -X (the blade) to +X.</summary>
        public static readonly Quaternion BattleaxeTurn = new Quaternion(0f, -0.70710677f, -0.70710677f, 0f);

        /// <summary>
        /// Where the game's Battleaxe clips hold the left fist (LeftHand_Attach) in the right fist's frame while standing
        /// and moving (GreataxeFit, over idle, walk, jog, run and the jump).
        /// </summary>
        public static readonly Vector3 LeftFist = new Vector3(-0.006f, -0.027f, 0.77f);

        public static float Scale => Length / HeadsmanAxe.Length;

        /// <summary>The fist's grip, in the axe model's own frame.</summary>
        public static Vector3 Grip => HeadsmanAxe.Spine(FistHeight);

        /// <summary>The height up the model's haft the left fist comes to.</summary>
        public static float LeftHeight => FistHeight + LeftFist.magnitude / Scale;

        /// <summary>The Battleaxe's turn, then aimed so the haft runs from the right fist through the left.</summary>
        public static Quaternion Turn
        {
            get
            {
                Vector3 haft = BattleaxeTurn * (HeadsmanAxe.Spine(LeftHeight) - Grip);
                return Quaternion.FromToRotation(haft, LeftFist) * BattleaxeTurn;
            }
        }

        /// <summary>A new "attach" object holding the axe in the hand's frame.</summary>
        public static GameObject Attach()
        {
            var attach = new GameObject("attach");
            var axe = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HeadsmanAxe.Prefab), attach.transform, false);
            axe.name = Name;
            axe.transform.localRotation = Turn;
            axe.transform.localScale = Vector3.one * Scale;
            axe.transform.localPosition = -(Turn * (Grip * Scale));
            return attach;
        }

        /// <summary>The axe hung from the player's RightHand_Attach as the game hangs a weapon: its attach at the point's origin.</summary>
        public static GameObject Hang(GameObject player)
        {
            GameObject attach = Attach();
            attach.transform.SetParent(GreataxePlayer.Bone(player, "RightHand_Attach"), false);
            attach.transform.localPosition = Vector3.zero;
            attach.transform.localRotation = Quaternion.identity;
            return attach;
        }
    }
}
