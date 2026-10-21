using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Workshop.Kraken
{
    /// <summary>
    /// The scene the kraken is for, around one of the game's ships (reference only): the head off the starboard side
    /// (+X) facing it in the attack pose - surfaced with its base 0.6 m under the water, raised 2.5 m, leaning in with
    /// its jaws open, so its column shows under it - three tentacles out of the water (one raised high astern, one
    /// across the deck, one off the port bow) and a 1.8 m player on deck; then the head merely surfaced, for comparison.
    /// The ship floats with its origin on the water line: the game's Ship keeps its float box (centred on the origin)
    /// at the water (Ship.FixedUpdate).
    /// </summary>
    public static class KrakenScene
    {
        public const float SurfacedBase = -0.6f, AttackRaise = 2.5f;

        public static void Render(string folder, GameObject tentaclePrefab, GameObject headPrefab, string shipPrefab, string prefix)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = KrakenStage.Build();
            KrakenStage.Water();
            var ship = KrakenShip.Load(shipPrefab);
            if (Hull(ship).size.x > Hull(ship).size.z)
                ship.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            ship.transform.position = Vector3.zero;
            Bounds hull = Hull(ship);
            float deck = KrakenShip.DeckHeight(ship, new Vector3(0.4f, 0f, hull.extents.z * 0.25f));
            float rail = KrakenShip.RailHeight(ship, hull.extents.x);
            float side = hull.extents.x, length = Mathf.Clamp(hull.extents.z / 10.5f, 0.45f, 1f);
            KrakenReport.Line($"preview {System.IO.Path.GetFileNameWithoutExtension(shipPrefab)}: hull {hull.min:F2} .. {hull.max:F2}; " +
                              $"deck floor {deck:F2} m and rail {rail:F2} m above the water");
            var head = Head(headPrefab, side);
            Tentacles(tentaclePrefab, side, rail, deck, length);
            KrakenStage.Player(new Vector3(-0.3f, deck, -1.4f * length), new Vector3(side + 3f, 0, 0.5f));
            Shots(folder, prefix, camera, side, deck);
            Lunge(folder, prefix, camera, head, side, deck);
            head.transform.position = new Vector3(head.transform.position.x, SurfacedBase, head.transform.position.z);
            KrakenPose.Rest(head);
            KrakenPreview.Shot(folder, prefix + "_surfaced", camera, new Vector3(-2.6f, deck + 1.75f, -5.2f), new Vector3(4.5f, 3.0f, 0.8f), 62f);
        }

        private static void Shots(string folder, string prefix, Camera camera, float side, float deck)
        {
            KrakenPreview.Shot(folder, prefix + "_wide", camera, new Vector3(-17f, 9f, -15f), new Vector3(1.8f, 3.4f, 0f), 42f);
            KrakenPreview.Shot(folder, prefix + "_deck", camera, new Vector3(-2.6f, deck + 1.75f, -5.2f), new Vector3(4.5f, 4.6f, 0.8f), 62f);
            KrakenPreview.Shot(folder, prefix + "_water", camera, new Vector3(17f, 1.8f, 21f), new Vector3(2f, 3.6f, 0f), 42f);
            KrakenPreview.Shot(folder, prefix + "_column", camera, new Vector3(side + 12f, 0.9f, 9f), new Vector3(side + 2.2f, 2.6f, 0.6f), 44f);
        }

        /// <summary>
        /// The bite over the rail: the head's root 1.5 m off the hull and 2 m up, leaning in 25 degrees with its jaws
        /// open, the column bent back 28 degrees at each of kh_body_1..3 so it goes down into the water outside the hull.
        /// </summary>
        private static void Lunge(string folder, string prefix, Camera camera, GameObject head, float side, float deck)
        {
            head.transform.position = new Vector3(side + 1.5f, 2.0f, 0.6f);
            KrakenPose.Neck(head, 25f);
            KrakenPose.Beak(head, 32f);
            KrakenPose.Body(head, new Vector3(28f, 0f, 0f));
            KrakenPreview.Shot(folder, prefix + "_lunge", camera, new Vector3(side + 1.5f, 1.6f, 19f), new Vector3(side + 1.2f, 1.6f, 0.6f), 44f);
            KrakenPreview.Shot(folder, prefix + "_lunge_deck", camera, new Vector3(-2.6f, deck + 1.75f, -5.2f), new Vector3(side, 2.6f, 0.6f), 62f);
            KrakenPose.Body(head, Vector3.zero);
        }

        private static Bounds Hull(GameObject ship)
        {
            var renderers = ship.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.name == "hull").ToArray();
            if (renderers.Length == 0)
                renderers = ship.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);
            return bounds;
        }

        /// <summary>The attack pose: base 0.6 m under water raised 2.5 m, facing the ship, leaning in, jaws open.</summary>
        private static GameObject Head(GameObject prefab, float side)
        {
            var at = new Vector3(side + 2.5f, SurfacedBase + AttackRaise, 0.6f);
            var head = KrakenPreview.Spawn(prefab, at, Quaternion.LookRotation(Vector3.left));
            KrakenPose.Neck(head, 20f);
            KrakenPose.Beak(head, 30f);
            return head;
        }

        /// <summary>One tentacle raised high astern, one across the deck, one off the port bow (z scaled to the hull).</summary>
        private static void Tentacles(GameObject prefab, float side, float rail, float deck, float length)
        {
            Tentacle(prefab, new List<Vector3>
            {
                new Vector3(side + 1.6f, -2.0f, -5.5f), new Vector3(side + 1.4f, 2.0f, -5.4f), new Vector3(side + 0.6f, 4.6f, -5.0f),
                new Vector3(side - 0.8f, 5.4f, -4.6f), new Vector3(side - 1.6f, 4.6f, -4.3f), new Vector3(side - 1.4f, 3.8f, -4.1f),
            }, length, (p, t) => (p - new Vector3(0, p.y, -4.5f * length)).normalized);
            Tentacle(prefab, new List<Vector3>
            {
                new Vector3(side + 1.3f, -1.2f, 3.4f), new Vector3(side + 0.9f, 0.8f, 3.3f), new Vector3(side + 0.2f, rail + 0.6f, 3.1f),
                new Vector3(side - 1.0f, deck + 0.32f, 2.8f), new Vector3(0f, deck + 0.26f, 2.5f), new Vector3(-side + 1.0f, deck + 0.28f, 2.2f),
                new Vector3(-side + 0.1f, rail + 0.4f, 2.0f), new Vector3(-side - 0.6f, rail - 0.6f, 1.8f),
            }, length, (p, t) => Vector3.up + new Vector3(Mathf.Sign(p.x), 0f, 0f));
            Tentacle(prefab, new List<Vector3>
            {
                new Vector3(-side - 2.2f, -2.0f, 6.5f), new Vector3(-side - 2.0f, 1.2f, 6.4f), new Vector3(-side - 1.2f, 3.6f, 6.0f),
                new Vector3(-side + 0.1f, 4.2f, 5.6f), new Vector3(-side + 0.6f, 3.2f, 5.3f), new Vector3(-side + 0.2f, 2.6f, 5.1f),
            }, length, (p, t) => (p - new Vector3(0, p.y, 5.5f * length)).normalized);
        }

        private static void Tentacle(GameObject prefab, List<Vector3> points, float length, System.Func<Vector3, Vector3, Vector3> up)
        {
            var scaled = points.Select(p => new Vector3(p.x, p.y, p.z * length)).ToList();
            var tentacle = KrakenPreview.Spawn(prefab, scaled[0], Quaternion.identity);
            KrakenPose.Follow(tentacle, scaled, up);
        }
    }
}
