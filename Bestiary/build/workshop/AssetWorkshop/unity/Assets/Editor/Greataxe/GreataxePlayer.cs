using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.Greataxe
{
    /// <summary>
    /// The game's player from the reference export, for previewing the Executioner's Greataxe (Elite Creatures Pack) in
    /// a player's hands: Player.prefab's Visual (the male body with its humanoid avatar, and RightHand_Attach, where every
    /// weapon hangs), dressed on a Standard material: the skin with the game's leather armour chest and trousers laid
    /// over it where their alpha is, as the player's shader lays them; the hair's submesh is left off. Everything lands
    /// in Assets/Reference/Player, which never goes into a bundle.
    /// </summary>
    public static class GreataxePlayer
    {
        public const string Subfolder = "Player";
        private const string Game = "Characters/Player/";
        private const string Model = Game + "model/";
        private const string Skin = Model + "old_PlayerCharacter2/PlayerCharacter_01.png";
        private static readonly string[] Armour = { Model + "textures/LeatherArmourPants_d.png", Model + "textures/LeatherArmourChest_d.png" };
        private static readonly string[] Parts = { Model + "player_maleAvatar.asset", Model + "body.asset" };

        /// <summary>The player's Visual at the origin facing +Z, dressed, with its humanoid Animator and nothing else of the game's.</summary>
        public static GameObject Player()
        {
            foreach (string part in Parts)
                ReferenceAssets.Import(part, Subfolder);
            string prefab = ReferenceAssets.Import(Game + "Player.prefab", Subfolder);
            var game = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            Transform visual = game.transform.Find("Visual");
            visual.SetParent(null, false);
            Object.DestroyImmediate(game);
            visual.name = "Player";
            visual.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Strip(visual.gameObject);
            Dress(visual.gameObject);
            return visual.gameObject;
        }

        public static Animator Animator(GameObject player) => player.GetComponentInChildren<Animator>(true);

        public static Transform Bone(GameObject player, string name) =>
            player.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        /// <summary>One of the game's player clips (or a third-party clip the player controller plays), by its path.</summary>
        public static AnimationClip Clip(string referencePath) =>
            AssetDatabase.LoadAssetAtPath<AnimationClip>(ReferenceAssets.Import(referencePath, Subfolder));

        /// <summary>The game's scripts are missing here; colliders, cloth colliders and particles would only get in the way.</summary>
        private static void Strip(GameObject player)
        {
            foreach (var child in player.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            foreach (var collider in player.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            foreach (var particles in player.GetComponentsInChildren<ParticleSystem>(true))
                Object.DestroyImmediate(particles.gameObject);
            foreach (var renderer in player.GetComponentsInChildren<Renderer>(true).Where(r => !(r is SkinnedMeshRenderer)))
                Object.DestroyImmediate(renderer.gameObject);
        }

        private static void Dress(GameObject player)
        {
            var material = new Material(Shader.Find("Standard")) { name = "player_preview", mainTexture = Layered() };
            material.SetFloat("_Glossiness", 0.1f);
            foreach (var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.sharedMesh = FirstSubmesh(skin.sharedMesh);
                skin.sharedMaterials = new[] { material };
                skin.forceMatrixRecalculationPerRender = true;   // batch mode skins once and keeps it otherwise
            }
        }

        /// <summary>The body's own triangles only: the hair's second submesh would wear the skin.</summary>
        private static Mesh FirstSubmesh(Mesh mesh)
        {
            if (mesh == null || mesh.subMeshCount < 2)
                return mesh;
            Mesh copy = Object.Instantiate(mesh);
            copy.subMeshCount = 1;
            return copy;
        }

        /// <summary>The skin with the armour over it, saved as a texture so the bake can name its file.</summary>
        private static Texture2D Layered()
        {
            Texture2D skin = Readable(Skin);
            Color[] pixels = skin.GetPixels();
            foreach (string layer in Armour)
            {
                Texture2D over = Readable(layer);
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = over.GetPixelBilinear((i % skin.width + 0.5f) / skin.width, (i / skin.width + 0.5f) / skin.height);
                    pixels[i] = Color.Lerp(pixels[i], c, c.a);
                }
            }
            return Save(pixels, skin.width, skin.height);
        }

        private static Texture2D Save(Color[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            string path = ReferenceAssets.Folder + "/" + Subfolder + "/player_preview_skin.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D Readable(string referencePath)
        {
            string path = ReferenceAssets.Import(referencePath, Subfolder);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
