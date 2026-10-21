using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Workshop.GameRig
{
    /// <summary>
    /// The three creatures a playback runs side by side, all driven by the game's own controller and avatar:
    /// - Game: the game creature with its own body (reference only), at +X: on the left seen from the front;
    /// - Worn: the game creature wearing our body, put on by GameRigSwap exactly as the mod's CreatureBody.Wear does,
    ///   at -X (on the right seen from the front);
    /// - Copy: our bundle prefab alone with an Animator added where the game creature has one (never drawn), whose
    ///   bones must move exactly as the game creature's do, proving the skeleton in the bundle is the game's.
    /// </summary>
    public sealed class GameRigActors
    {
        public const float Apart = 0.85f;

        public GameObject Game, Worn, Copy;
        public SkinnedMeshRenderer GameBody, WornBody;
        public Transform[] GameBones, CopyBones;
        public AnimatorController Controller;

        public Animator[] Animators => new[] { Game, Worn, Copy }.Select(c => c.GetComponentInChildren<Animator>(true)).ToArray();

        public static GameRigActors Create(GameRigModel model, string prefabPath)
        {
            var actors = new GameRigActors();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            actors.Game = GameRigStaging.Creature(model.reference, "game_" + model.asset);
            actors.Worn = GameRigStaging.Creature(model.reference, model.asset);
            actors.GameBody = GameRigStaging.Body(actors.Game, model.renderer);
            actors.WornBody = GameRigSwap.Wear(actors.Worn, prefab, model.renderer);
            actors.Copy = Object.Instantiate(prefab);
            actors.Copy.name = "copy_" + model.asset;
            actors.Controller = (AnimatorController)actors.Game.GetComponentInChildren<Animator>(true).runtimeAnimatorController;
            actors.Animate(actors.Copy, model);
            actors.Game.transform.position = new Vector3(Apart, 0f, 0f);
            actors.Worn.transform.position = actors.Copy.transform.position = new Vector3(-Apart, 0f, 0f);
            actors.GameBones = model.bones.Select(b => GameRigPrefab.Named(actors.Game.transform, b)).ToArray();
            actors.CopyBones = model.bones.Select(b => GameRigPrefab.Named(actors.Copy.transform, b)).ToArray();
            return actors;
        }

        /// <summary>The copy gets an Animator where the game creature has its own, with the game's avatar and controller.</summary>
        private void Animate(GameObject copy, GameRigModel model)
        {
            Animator game = Game.GetComponentInChildren<Animator>(true);
            Transform node = copy.transform.Find(model.reference.animatorNode);
            var animator = node.gameObject.AddComponent<Animator>();
            animator.avatar = game.avatar;
            animator.runtimeAnimatorController = Controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            foreach (var skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.enabled = false;
        }

        /// <summary>The worst distance, in the creatures' own space, between a game bone and our copy's bone of that name.</summary>
        public float SkeletonGap()
        {
            float worst = 0f;
            for (int i = 0; i < GameBones.Length; i++)
            {
                Vector3 a = Game.transform.InverseTransformPoint(GameBones[i].position);
                Vector3 b = Copy.transform.InverseTransformPoint(CopyBones[i].position);
                worst = Mathf.Max(worst, Vector3.Distance(a, b));
            }
            return worst;
        }

        public void Destroy()
        {
            Object.DestroyImmediate(Game);
            Object.DestroyImmediate(Worn);
            Object.DestroyImmediate(Copy);
        }
    }
}
