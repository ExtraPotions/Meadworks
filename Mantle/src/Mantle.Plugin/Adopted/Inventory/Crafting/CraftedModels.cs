using ExtraPotions.Mantle.Embedded.BundlePrefabs;
using UnityEngine;

namespace Mantle.Crafting
{
    /// <summary>
    /// A crafted item's model from an embedded bundle (AssetWorkshop, one build per platform: dedicated servers run the
    /// Linux player), dressed in a copy of the game's troll hide cape material wearing the model's own baked textures, so
    /// the game lights it as it lights a cape (rain darkens it too). Shared by the backpacks and the tackleboxes.
    /// </summary>
    public static class CraftedModels
    {
        private const string Cape = "CapeTrollHide";

        /// <summary>The kind's model from the bundle, with the look its copies are dressed in.</summary>
        public static GameObject Model(string bundle, CraftedKind kind) =>
            EmbeddedBundle.Prefab(EmbeddedBundle.Load(typeof(CraftedModels).Assembly, bundle), kind.Asset);

        /// <summary>The cape's material with the model's own textures, or the model's placeholder when the cape is missing.</summary>
        public static Material Look(ZNetScene scene, GameObject model)
        {
            Material placeholder = model.GetComponentInChildren<Renderer>(true).sharedMaterial;
            Material game = GameMaterials.Borrow(scene.GetPrefab(Cape), "attach_skin");
            return game != null ? GameMaterials.Dress(game, placeholder) : placeholder;
        }

        /// <summary>A copy of the model on BundlePrefabs' inactive bench, wearing the look.</summary>
        public static GameObject Dressed(GameObject model, string name, Material look)
        {
            GameObject copy = PrefabBench.Copy(model, name);
            GameMaterials.Apply(copy, placeholder => look);
            return copy;
        }
    }
}
