using System.Collections.Generic;
using HarmonyLib;
using Burrow.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burrow.Reach
{
    /// <summary>
    /// Lines from a container to every crafting station, smelter, cooking station, fermenter and fireplace within
    /// its reach range: one thin world space <c>LineRenderer</c> per link in the storage colour, unlit, without
    /// shadows, destroyed after Link Seconds. Drawn when a container piece is placed and on the Link Key.
    /// </summary>
    public static class ReachLinks
    {
        private const float Lift = 0.6f;
        private const float Width = 0.05f;
        private static Material material;

        /// <summary>Lines from every reachable container.</summary>
        public static void ShowAll()
        {
            foreach (Container container in ReachCount.Containers())
                Show(container);
        }

        public static void Show(Container container)
        {
            if (container == null || !ReachSettings.ShowLinks.Value)
                return;
            Vector3 from = container.transform.position;
            foreach (Vector3 to in StationPoints(from, ReachRules.RangeFor(container)))
                Draw(from, to);
        }

        private static List<Vector3> StationPoints(Vector3 from, float range)
        {
            List<Vector3> points = new List<Vector3>();
            Add(points, CraftingStation.m_allStations, from, range);
            Add(points, Object.FindObjectsByType<Smelter>(FindObjectsSortMode.None), from, range);
            Add(points, Object.FindObjectsByType<CookingStation>(FindObjectsSortMode.None), from, range);
            Add(points, Object.FindObjectsByType<Fermenter>(FindObjectsSortMode.None), from, range);
            Add(points, Object.FindObjectsByType<Fireplace>(FindObjectsSortMode.None), from, range);
            return points;
        }

        private static void Add<T>(List<Vector3> points, IEnumerable<T> stations, Vector3 from, float range) where T : Component
        {
            foreach (T station in stations)
            {
                if (station != null && Vector3.Distance(from, station.transform.position) <= range)
                    points.Add(station.transform.position);
            }
        }

        private static void Draw(Vector3 from, Vector3 to)
        {
            GameObject holder = new GameObject("Burrow.ReachLink");
            LineRenderer line = holder.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, from + Vector3.up * Lift);
            line.SetPosition(1, to + Vector3.up * Lift);
            line.startWidth = Width;
            line.endWidth = Width;
            line.material = LinkMaterial();
            Color colour = ReachSettings.Colour();
            line.startColor = colour;
            line.endColor = colour;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            Object.Destroy(holder, Mathf.Max(0.1f, ReachSettings.LinkSeconds.Value));
        }

        /// <summary>An unlit material: the sprite shader when the build ships it, else the cart's own rope line material.</summary>
        private static Material LinkMaterial()
        {
            if (material != null)
                return material;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Particles/Standard Unlit");
            if (shader != null)
                material = new Material(shader);
            else
                material = CartLineMaterial();
            return material;
        }

        private static Material CartLineMaterial()
        {
            GameObject cart = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("Cart") : null;
            LineRenderer line = cart != null ? cart.GetComponent<LineRenderer>() : null;
            return line != null ? line.sharedMaterial : new Material(Shader.Find("Standard"));
        }
    }

    /// <summary>Draws the links when the local player places a piece that has a container.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
    public static class PlacedContainerPatch
    {
        private const float MatchDistance = 1.5f;

        [HarmonyPostfix]
        public static void Postfix(Player __instance, Piece piece, Vector3 pos)
        {
            if (__instance != Player.m_localPlayer || piece == null || !ReachSettings.Enabled.Value || !ReachSettings.ShowLinks.Value || ReachRules.PlayerOff)
                return;
            if (piece.GetComponentInChildren<Container>() == null)
                return;
            Container placed = Nearest(pos);
            if (placed != null)
                ReachLinks.Show(placed);
        }

        private static Container Nearest(Vector3 pos)
        {
            Container best = null;
            float bestDistance = MatchDistance;
            foreach (Container container in ContainerScan.All())
            {
                float distance = Vector3.Distance(pos, container.transform.position);
                if (distance <= bestDistance)
                {
                    best = container;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
