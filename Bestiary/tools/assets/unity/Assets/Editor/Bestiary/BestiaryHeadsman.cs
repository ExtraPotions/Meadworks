// Bestiary bundle build: the Crypt Executioner. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Workshop.Greataxe;
using Workshop.Headsman;

namespace Bestiary.Build
{
    /// <summary>
    /// The Crypt Executioner's bundle, ecp_headsman. From the pinned workshop: the bone greataxe prefab
    /// (HeadsmanAxe.Stage), its pieces (HeadsmanPieces) and the axehead mesh the way HeadsmanBundle combines it. From
    /// EliteCreaturesPack's bundle export: the kit, held greataxe and axehead trees (BestiaryKits) and the authored
    /// clips, re-keyed per Q3 (clip_curves.py). The animator controller is HeadsmanController's, except that the
    /// Movement blend tree runs three empty slot clips the mod overrides with the game Skeleton's own idle, walk and
    /// run at runtime (design 10.1 rule 2): the resampled ecp_headsman_idle, _walk and _run are never built.
    /// </summary>
    public static class BestiaryHeadsman
    {
        public const string Controller = HeadsmanController.Name;
        public static readonly string[] Attacks =
        {
            "ecp_headsman_slam", "ecp_headsman_scrape", "ecp_headsman_spin",
            "ecp_headsman_hurl", "ecp_headsman_spinthrow", "ecp_headsman_rear",
        };
        public static readonly string[] Slots = { "ecp_headsman_slot_idle", "ecp_headsman_slot_walk", "ecp_headsman_slot_run" };
        private const float Blend = 0.2f;
        private static readonly string[] Floats = { "forward_speed", "sideway_speed", "turn_speed", "footstep" };
        private static readonly string[] Bools = { "inWater", "dead", "wakeup" };
        private static readonly string[] Triggers = { "jump", "attack", "attack_bow", "stagger", "attack_mace", "attack_fire" };

        public static string[] Build(string bundle)
        {
            if (bundle != HeadsmanBuild.Bundle)
                throw new ArgumentException($"the headsman bundle is {HeadsmanBuild.Bundle}, not {bundle}");
            string folder = HeadsmanBuild.Folder;
            string built = folder + "/_built";
            Directory.CreateDirectory(built);
            string axePrefab = HeadsmanAxe.Stage();
            var meshes = new Dictionary<string, (Mesh mesh, Material material)>();
            BestiaryKits.AddMeshes(axePrefab, meshes);
            var (mesh, material, frame) = HeadsmanPieces.Axe();
            var pieces = new List<Mesh>();
            foreach (var (name, triangles) in HeadsmanPieces.Split(mesh, frame))
            {
                Mesh piece = HeadsmanPieces.Piece(mesh, frame, triangles, name);
                AssetDatabase.CreateAsset(piece, $"{built}/{piece.name}.asset");
                pieces.Add(piece);
                meshes[piece.name] = (piece, material);
            }
            meshes[GreataxeAxehead() + "/model"] = (Axehead(pieces, built), material);
            BestiaryBundles.Line($"greataxe: {pieces.Count} pieces");
            var assets = new List<string>();
            KitTree tree = BestiaryKits.ReadTree(folder);
            foreach (KitNode root in tree.nodes.Where(n => n.parent < 0))
                assets.Add(BestiaryKits.SaveTree(tree, root, meshes, built));
            string[] clips = BestiaryKits.Clips(folder).ToArray();
            assets.AddRange(clips);
            assets.Add(BuildController(folder, built));
            foreach (string icon in Directory.GetFiles(folder + "/_icons", "*.png").OrderBy(p => p, StringComparer.Ordinal))
            {
                string path = icon.Replace('\\', '/');
                Workshop.ImportSettings.Sprite(path);
                assets.Add(path);
            }
            return assets.ToArray();
        }

        private static string GreataxeAxehead() => "ecp_greataxe_axehead";

        /// <summary>HeadsmanBundle.AxeheadPrefab's mesh: the pieces above the haft as one mesh, centered and scaled.</summary>
        private static Mesh Axehead(List<Mesh> pieces, string built)
        {
            CombineInstance[] parts = pieces.Where(p => p.bounds.center.y > HeadsmanPieces.HeadStart)
                .Select(p => new CombineInstance { mesh = p, transform = Matrix4x4.identity }).ToArray();
            var head = new Mesh { name = GreataxeAxehead() };
            head.CombineMeshes(parts, true, true);
            Vector3 middle = head.bounds.center;
            head.vertices = head.vertices.Select(v => (v - middle) * GreataxeModel.Scale).ToArray();
            head.RecalculateBounds();
            AssetDatabase.CreateAsset(head, $"{built}/{head.name}.asset");
            return head;
        }

        private static AnimationClip Load(string folder, string name) =>
            AssetDatabase.LoadAssetAtPath<AnimationClip>($"{folder}/_clips/{name}.anim");

        /// <summary>HeadsmanController's parameters, stagger and attack states; Movement on the three slot clips.</summary>
        private static string BuildController(string folder, string built)
        {
            string path = $"{built}/{Controller}.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (string name in Floats)
                controller.AddParameter(name, AnimatorControllerParameterType.Float);
            foreach (string name in Bools)
                controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            foreach (string name in Triggers.Concat(Attacks))
                controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimationClip[] slots = Slots.Select(name =>
            {
                string slotPath = $"{built}/{name}.anim";
                AssetDatabase.DeleteAsset(slotPath);
                var slot = new AnimationClip { name = name };
                AssetDatabase.CreateAsset(slot, slotPath);
                return slot;
            }).ToArray();
            AnimatorState movement = controller.CreateBlendTreeInController("Movement", out BlendTree tree);
            movement.tag = "idle";
            (tree.blendType, tree.blendParameter, tree.blendParameterY) = (BlendTreeType.FreeformDirectional2D, "sideway_speed", "forward_speed");
            tree.AddChild(slots[0], Vector2.zero);
            tree.AddChild(slots[1], new Vector2(0f, 1f));
            tree.AddChild(slots[2], new Vector2(0f, 3f));
            tree.AddChild(slots[1], new Vector2(-0.75f, 0f));
            tree.AddChild(slots[1], new Vector2(0.75f, 0f));
            machine.defaultState = movement;

            AnimatorState stagger = machine.AddState("Stagger");
            (stagger.motion, stagger.tag, stagger.speed) = (Load(folder, HeadsmanController.StaggerSlot), "stagger", 0.7f);
            AnimatorStateTransition enter = machine.AddAnyStateTransition(stagger);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "stagger");
            (enter.hasExitTime, enter.duration, enter.hasFixedDuration, enter.canTransitionToSelf) = (false, 0.1f, true, false);
            AnimatorStateTransition back = stagger.AddTransition(movement);
            (back.hasExitTime, back.exitTime, back.duration, back.hasFixedDuration) = (true, 0.9f, Blend, true);

            foreach (string attack in Attacks)
            {
                AnimationClip clip = Load(folder, attack) ?? throw new InvalidOperationException("no rebuilt clip " + attack);
                AnimatorState state = machine.AddState(attack);
                (state.motion, state.tag) = (clip, "attack");
                AnimatorStateTransition into = machine.AddAnyStateTransition(state);
                into.AddCondition(AnimatorConditionMode.If, 0f, attack);
                (into.hasExitTime, into.duration, into.hasFixedDuration, into.canTransitionToSelf) = (false, Blend, true, false);
                AnimatorStateTransition leave = state.AddTransition(movement);
                (leave.hasExitTime, leave.exitTime, leave.duration, leave.hasFixedDuration) =
                    (true, (clip.length - Blend) / clip.length, Blend, true);
            }
            AssetDatabase.SaveAssets();
            BestiaryBundles.Line($"animator {path}: {controller.parameters.Length} parameters, {machine.states.Length} states, " +
                                 "Movement on slots " + string.Join(", ", Slots));
            return path;
        }
    }
}
