using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Greataxe;

namespace Workshop.SkelArsenal
{
    /// <summary>
    /// Where the game's atgeir clips put a player's left fist, against the bone atgeir's haft and the game's bronze
    /// atgeir's (its mesh at its place in the attach frame), all in the right fist's frame (RightHand_Attach):
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.SkelArsenal.ArsenalAtgeirProbe.Run
    /// Logs each haft's line (middle and direction, by the mesh's longest spread) and the left fist's distance from it,
    /// in the stance and through the combo, and the fist's mean place.
    /// </summary>
    public static class ArsenalAtgeirProbe
    {
        private const string Controller = "Characters/Player/animation/Player_animator.controller";
        private const string Bronze = "GameElements/Items/weapons/_res/atgier/atgier.asset";

        public static void Run()
        {
            int code = 1;
            try
            {
                Probe();
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("atgeir probe failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static void Probe()
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ReferenceController.Import(Controller, "PlayerController"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject player = GreataxePlayer.Player();
            Transform fist = GreataxePlayer.Bone(player, "RightHand_Attach"), left = GreataxePlayer.Bone(player, "LeftHand_Attach");
            var ours = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bundles/ecp_skel_arsenal/ecp_skel_atgeir/ecp_skel_atgeir.prefab"), fist, false);
            Transform bronze = BronzeAtgeir(fist);
            (Vector3 at, Vector3 dir) oursLine = Line(ours.transform, fist), bronzeLine = Line(bronze, fist);
            Log.Info($"atgeir probe: ours middle {oursLine.at:F3} dir {oursLine.dir:F3}; bronze middle {bronzeLine.at:F3} dir {bronzeLine.dir:F3}");
            Animator animator = GreataxePlayer.Animator(player);
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.SetInteger("statei", 7);
            animator.SetFloat("statef", 7);
            animator.SetBool("onGround", true);
            Sample(animator, fist, left, oursLine, bronzeLine);
        }

        private static void Sample(Animator animator, Transform fist, Transform left, (Vector3 at, Vector3 dir) ours, (Vector3 at, Vector3 dir) bronze)
        {
            var parts = new List<(string part, Vector3 p)>();
            for (int i = 0; i < 40; i++)
                parts.Add(("stance", Step(animator, fist, left)));
            foreach (string trigger in new[] { "atgeir_attack0", "atgeir_attack1", "atgeir_attack2" })
            {
                animator.SetTrigger(trigger);
                for (int i = 0; i < 38; i++)
                {
                    Vector3 p = Step(animator, fist, left);
                    parts.Add((trigger, p));
                    if (trigger.EndsWith("2") && i % 3 == 0)
                        Log.Info($"atgeir probe frame {trigger} {i}: along {Vector3.Dot(p, ours.dir):F2} m, off {Vector3.Cross(p, ours.dir).magnitude:F2} m, grip z along haft {Vector3.Dot(fist.InverseTransformDirection(left.forward), ours.dir):F2}");
                }
            }
            foreach (var group in parts.GroupBy(p => p.part))
            {
                Vector3 mean = group.Aggregate(Vector3.zero, (s, p) => s + p.p) / group.Count();
                Log.Info($"atgeir probe {group.Key}: left fist mean {mean:F3} ({mean.magnitude:F3} m), off ours {Mean(group, ours):F3} m, off bronze {Mean(group, bronze):F3} m");
            }
        }

        private static Vector3 Step(Animator animator, Transform fist, Transform left)
        {
            animator.Update(1f / 30f);
            return fist.InverseTransformPoint(left.position);
        }

        private static float Mean(IEnumerable<(string part, Vector3 p)> points, (Vector3 at, Vector3 dir) line) =>
            points.Average(x => Vector3.Cross(x.p - line.at, line.dir).magnitude);

        /// <summary>The game's bronze atgeir mesh where its prefab puts it in the attach frame.</summary>
        private static Transform BronzeAtgeir(Transform fist)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ReferenceAssets.Import(Bronze, "Atgeir"));
            var spear = new GameObject("bronze_spear");
            spear.transform.SetParent(fist, false);
            spear.transform.localPosition = new Vector3(0.145f, -0.077f, 0.435f);   // AtgeirBronze attach/spear
            spear.transform.localRotation = new Quaternion(0.75249827f, -0.63284796f, -0.17515294f, 0.050707858f);
            spear.transform.localScale = Vector3.one * 0.7f;
            spear.AddComponent<MeshFilter>().sharedMesh = mesh;
            return spear.transform;
        }

        /// <summary>A weapon's line in the fist's frame: the middle of its vertices and the direction they spread furthest.</summary>
        private static (Vector3 at, Vector3 dir) Line(Transform weapon, Transform fist)
        {
            var points = weapon.GetComponentsInChildren<MeshFilter>().SelectMany(f => f.sharedMesh.vertices.Select(v => fist.InverseTransformPoint(f.transform.TransformPoint(v)))).ToList();
            Vector3 middle = points.Aggregate(Vector3.zero, (s, p) => s + p) / points.Count;
            (Vector3 a, Vector3 b) = (points.OrderBy(p => (p - middle).sqrMagnitude).Last(), Vector3.zero);
            b = points.OrderBy(p => (p - a).sqrMagnitude).Last();
            return (middle, (b - a).normalized);
        }
    }
}
