using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// The slinger's stone for the preview, as the mod fires it (EliteCreaturesPack SlingerStone, SlingerShot,
    /// SlingerAim): the game's rock mesh (reference, preview only) at half the size of a greydwarf's thrown rock, flying
    /// at the rules' default speed under the stone's gravity, launched at the low arc that lands on the target.
    /// </summary>
    public static class SlingStone
    {
        public const float Scale = 0.04f * 0.5f;   // the thrown rock's visual is 0.04; the slinger's stone is half that rock
        public const float Speed = 10f;            // a greydwarf throws at 12
        public const float Gravity = 4f;
        private const string RockMesh = "GameElements/Items/_res/stone/default.asset";

        public static GameObject Make(string name)
        {
            var stone = new GameObject(name);
            stone.AddComponent<MeshFilter>().sharedMesh = ReferenceAssets.Mesh(RockMesh);
            stone.AddComponent<MeshRenderer>().sharedMaterial = SlingerStage.Plain("rock", new Color(0.42f, 0.4f, 0.37f));
            return stone;
        }

        /// <summary>
        /// The low-arc elevation, in radians above the horizontal, that carries a stone at `speed` over `across` metres
        /// and `up` metres of rise under `gravity`; 45 degrees when the target is out of reach.
        /// </summary>
        public static float Elevation(float speed, float gravity, float across, float up)
        {
            float v2 = speed * speed;
            float root = v2 * v2 - gravity * (gravity * across * across + 2f * up * v2);
            return root < 0f || across < 0.01f ? Mathf.PI / 4f : Mathf.Atan((v2 - Mathf.Sqrt(root)) / (gravity * across));
        }
    }
}
