using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Crossbow;
using Workshop.Greataxe;
using Workshop.Slinger;

namespace Workshop.SkelArsenal
{
    /// <summary>
    /// The players' bone weapons in a player's hands, for Blender (assets/ecp_skel_arsenal/blender_scene.py builds the
    /// .blend from the same format as the skeletons' showcase):
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.SkelArsenal.ArsenalPlayerBake.Run -workshopOut &lt;folder&gt; [-only &lt;label&gt;]
    /// The game's player (reference, preview only, <see cref="GreataxePlayer"/>) stands in a row, one per weapon, each
    /// holding it as the game hangs a held item (under RightHand_Attach, the bow and crossbow under LeftHand_Attach) and
    /// played by the game's own player controller (Player_animator.controller with its clips, <see cref="ReferenceController"/>)
    /// through its routine (<see cref="ArsenalPlayerRoutines"/>, <see cref="ArsenalPlayerSteps"/>): the stance, the whole
    /// combo, the stance again. Needs the prefabs in Assets/Bundles/ecp_skel_arsenal and ecp_crossbowman.
    /// </summary>
    public static class ArsenalPlayerBake
    {
        private const float Spacing = 4f;   // a close-up shows one player only
        private static readonly Vector3 AtgeirUp = new Vector3(0.339f, -0.143f, 0.930f).normalized;   // up the haft (ArsenalAtgeirProbe)
        private const string Controller = "Characters/Player/animation/Player_animator.controller";

        public static void Run()
        {
            int code = 1;
            try
            {
                Bake(SlingerStage.Argument("-workshopOut"));
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("bone weapons player bake failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static void Bake(string folder)
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ReferenceController.Import(Controller, "PlayerController"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PlayerRoutine[] routines = Routines();
            var groups = routines.Select((routine, i) => Player(routine, i, routines.Length, controller, folder)).ToArray();
            Directory.CreateDirectory(folder);
            var lineup = new ArsenalBake.Lineup { fps = ArsenalPlayerSteps.Fps, groups = groups };
            File.WriteAllText(Path.Combine(folder, "arsenal.json"), JsonUtility.ToJson(lineup, true));
            Log.Info($"bone weapons: {groups.Length} players baked into {folder}");
        }

        /// <summary>Every routine, or only the one named after -only (its label, like Shield).</summary>
        private static PlayerRoutine[] Routines()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-only");
            return at < 0 || at + 1 >= args.Length ? ArsenalPlayerRoutines.All : ArsenalPlayerRoutines.All.Where(r => r.Label == args[at + 1]).ToArray();
        }

        /// <summary>One player with one weapon, placed in the row, baked as one loop into its own folder.</summary>
        private static ArsenalBake.Group Player(PlayerRoutine routine, int index, int count, RuntimeAnimatorController controller, string folder)
        {
            GameObject player = GreataxePlayer.Player();
            player.name = "Player_" + routine.Label;
            float x = (index - (count - 1) / 2f) * Spacing;
            player.transform.position = new Vector3(-x, 0f, 0f);   // Blender x = -Unity x
            Transform weapon = Mount(routine.Prefab, routine, player);
            Transform spent = routine.Spent != null ? Mount(routine.Spent, routine, player) : null;
            if (routine.Shield != null)
                ArsenalShield.Mount(Prefab(routine.Shield), player);
            ArsenalBow bow = routine.Bow == null ? null : new ArsenalBow(weapon.gameObject, GreataxePlayer.Bone(player, "RightHand_Attach"),
                Prefab("Assets/Bundles/ecp_skel_arsenal/ecp_skel_arrow/ecp_skel_arrow.prefab"), ArsenalBow.PointsFile(routine.Bow), "bow aim", "bow fire");
            Animator animator = Animate(player, routine.Crossbow ? BoneCrossbow(controller) : routine.Label == "Atgeir" ? BoneAtgeir(controller) : controller, routine.State);
            XbowPlayerRigPreview rig = routine.Crossbow ? new XbowPlayerRigPreview(weapon, player, routine.Scale) : null;
            var cache = new XbowCache(player.GetComponentsInChildren<Renderer>(true).Concat(bow?.Renderers ?? Enumerable.Empty<Renderer>()));
            var steps = new ArsenalPlayerSteps(animator, cache, bow, weapon, spent, routine.Scale) { Rig = rig };
            if (routine.Label == "Atgeir")
                steps.Grip = new GreataxeGrip(player, weapon, h => AtgeirUp * h, 0.2f, 1.3f);   // as the mod's ArsenalAtgeirHold
            ArsenalPlayerTour.Play(routine, steps, animator, cache);
            string name = "player_" + routine.Label.ToLowerInvariant();
            cache.Write(Path.Combine(folder, name), ArsenalPlayerSteps.Fps);
            Log.Info($"bone weapons {routine.Label}: {steps.Frame} frames, attack at {steps.Attack}, loose {steps.Loose}");
            player.SetActive(false);
            return new ArsenalBake.Group { weapon = name, label = routine.Label, folder = name, frames = steps.Frame, attack = steps.Attack, loose = steps.Loose, x = x };
        }

        /// <summary>The weapon's prefab under the hand's attach point at zero position and rotation, as the game hangs a held item.</summary>
        private static Transform Mount(string prefab, PlayerRoutine routine, GameObject player)
        {
            Transform weapon = Prefab(prefab).transform;
            weapon.SetParent(GreataxePlayer.Bone(player, routine.LeftHand ? "LeftHand_Attach" : "RightHand_Attach"), false);
            (weapon.localPosition, weapon.localRotation, weapon.localScale) = (Vector3.zero, Quaternion.identity, Vector3.one * routine.Scale);
            return weapon;
        }

        /// <summary>The game's controller with the Bone Crossbow's own player reload in the Arbalest's (the mod's XbowHold does the same).</summary>
        private static RuntimeAnimatorController BoneCrossbow(RuntimeAnimatorController game)
        {
            var ours = new AnimatorOverrideController(game) { name = "bone_crossbow_player" };
            foreach (var (slot, clip) in new[] { ("Reload Crossbow", XbowClips.PlayerReloadName), ("Reload done", XbowClips.PlayerDoneName) })
            {
                AnimationClip replaced = game.animationClips.First(c => c.name == slot);
                ours[replaced] = AssetDatabase.LoadAssetAtPath<AnimationClip>(XbowBuild.ClipPath(clip));
            }
            return ours;
        }

        /// <summary>The game's controller with the Bone Atgeir's own attacks in the game's atgeir attacks (the mod's ArsenalAtgeirHold does the same).</summary>
        private static RuntimeAnimatorController BoneAtgeir(RuntimeAnimatorController game)
        {
            var ours = new AnimatorOverrideController(game) { name = "bone_atgeir_player" };
            string[] slots = { "2Hand-Spear-Attack1", "2Hand-Spear-Attack9", "2Hand-Spear-Attack3" };
            for (int i = 0; i < slots.Length; i++)
                ours[game.animationClips.First(c => c.name == slots[i])] = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AtgeirAuthor.Folder}/{AtgeirAuthor.Prefix}{i}.anim");
            return ours;
        }

        private static Animator Animate(GameObject player, RuntimeAnimatorController controller, int state)
        {
            Animator animator = GreataxePlayer.Animator(player);
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // no camera sees it here
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.SetInteger("statei", state);
            animator.SetFloat("statef", state);
            animator.SetBool("onGround", true);
            animator.Update(0f);
            for (int i = 0; i < 90; i++)
                animator.Update(1f / ArsenalPlayerSteps.Fps);   // past the controller's sit-and-rise intro, unrecorded
            return animator;
        }

        private static GameObject Prefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new FileNotFoundException("no prefab; build its bundle first", path);
            var instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = Path.GetFileNameWithoutExtension(path);
            return instance;
        }
    }
}
