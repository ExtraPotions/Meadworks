// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Reuses Shepherd's native prefab/recipe/hammer lifecycle and expDARE Keel mesh-only fence construction.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Shepherd.Pens;
using ExtraPotions.Shepherd.Troughs;
using ExtraPotions.Shepherd.Runtime.Feeding;
using ExtraPotions.Shepherd.Runtime.Integration;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Shepherd.Runtime.Pens
{
    public static class LandPenPieces
    {
        static Func<int>? capacity;
        static Func<string>? recipe;
        static Harmony? harmony;
        static GameObject? holder, prefab;
        static string appliedRecipe = "";
        // A recipe text without native materials is parsed once; it is retried only after the text changes.
        static string rejectedRecipe = "";
        static ObjectDB? hammerDatabase;
        static bool failed;
        static double retryAt;
        public static bool Active => harmony != null && Plugin.Ready && NativeFeeding.ProductActive;
        public static bool Ready => prefab != null;
        internal static int Capacity => LandPenDefinition.Capacity(capacity?.Invoke() ?? LandPenDefinition.DefaultCapacity);

        public static void Initialize(Func<int> readCapacity, Func<string> readRecipe)
        {
            Shutdown();
            capacity = readCapacity ?? throw new ArgumentNullException(nameof(readCapacity));
            recipe = readRecipe ?? throw new ArgumentNullException(nameof(readRecipe));
            harmony = new Harmony(Plugin.Guid + ".land_pen");
            foreach (var type in new[] { typeof(SceneAwake), typeof(DatabaseAwake), typeof(GateHover), typeof(Requirements) })
                harmony.CreateClassProcessor(type).Patch();
            Install();
        }
        public static void Tick()
        {
            if (harmony == null) return;
            try
            {
                if (prefab == null && Time.realtimeSinceStartupAsDouble >= retryAt)
                { retryAt = Time.realtimeSinceStartupAsDouble + 1; Install(); }
                if (prefab != null) { prefab.GetComponent<Piece>().m_enabled = Active; if (ApplyRecipe()) AddToHammer(); }
            }
            catch (Exception error) { if (!failed) Warn("land pen refresh", error); failed = true; }
        }
        public static void Shutdown()
        {
            LandPenRegistration.Clear(); harmony?.UnpatchSelf(); harmony = null; capacity = null; recipe = null; rejectedRecipe = ""; hammerDatabase = null;
            if (prefab != null) prefab.GetComponent<Piece>().m_enabled = false;
        }
        public static void Install()
        {
            if (harmony == null || ZNetScene.instance == null) return;
            try
            {
                if (prefab == null) Build(ZNetScene.instance);
                if (prefab == null) return;
                var scene = ZNetScene.instance;
                int hash = LandPenDefinition.Prefab.GetStableHashCode();
                if (scene.m_namedPrefabs.TryGetValue(hash, out var prior) && prior != prefab) throw new InvalidOperationException("land pen prefab name already registered");
                if (!scene.m_prefabs.Contains(prefab)) scene.m_prefabs.Add(prefab);
                scene.m_namedPrefabs[hash] = prefab;
                if (ObjectDB.instance == null) return;
                if (!ApplyRecipe()) return;
                hammerDatabase = null;
                AddToHammer();
                failed = false;
            }
            catch (Exception error) { if (!failed) Warn("land pen installation", error); failed = true; }
        }
        static void Build(ZNetScene scene)
        {
            var source = scene.GetPrefab(LandPenDefinition.NativeGate);
            var pole = scene.GetPrefab("wood_pole")?.GetComponentInChildren<MeshFilter>(true);
            var beam = scene.GetPrefab("wood_beam")?.GetComponentInChildren<MeshFilter>(true);
            if (source == null || source.GetComponent<Door>() == null || source.GetComponent<ZNetView>() == null
                || source.GetComponent<Piece>() == null || source.GetComponent<WearNTear>() == null || !Usable(pole) || !Usable(beam))
                throw new InvalidOperationException("native gate/wood parts missing or invalid");
            var gateBox = GateBox(source);
            if (gateBox.size.x < 1 || gateBox.size.x > LandPenDefinition.Width - 0.5f || gateBox.size.z > 1.5f || gateBox.size.y < 0.8f || gateBox.size.y > 5)
                throw new InvalidOperationException("native gate orientation/extent is outside the measured construction bounds");
            if (holder == null) { holder = new GameObject("Shepherd_LandPen_Prefabs"); holder.SetActive(false); Object.DontDestroyOnLoad(holder); }
            var copy = Object.Instantiate(source, holder.transform, false);
            copy.name = LandPenDefinition.Prefab;
            try
            {
                var piece = copy.GetComponent<Piece>();
                piece.m_name = ShepherdText.Token(LandPenDefinition.NameKey);
                piece.m_description = ShepherdText.Token(LandPenDefinition.DescriptionKey);
                piece.m_category = Piece.PieceCategory.Furniture;
                piece.m_craftingStation = scene.GetPrefab(LandPenDefinition.Station)?.GetComponent<CraftingStation>();
                copy.GetComponent<Door>().m_name = piece.m_name;
                float cx = gateBox.center.x, front = gateBox.center.z, floor = gateBox.min.y;
                float half = LandPenDefinition.Width / 2, length = LandPenDefinition.Length, height = LandPenDefinition.RailHeight;
                float gateHalf = gateBox.size.x / 2, segment = half - gateHalf;
                foreach (float side in new[] { -1f, 1f })
                {
                    foreach (float z in new[] { front, front + length })
                        Member(copy.transform, pole!, new Vector3(cx + side * half, floor + height / 2, z), Vector3.up, height, 0.16f);
                    Fence(copy.transform, beam!, new Vector3(cx + side * half, floor + height / 2, front + length / 2), length, height, true);
                    Fence(copy.transform, beam!, new Vector3(cx + side * (gateHalf + segment / 2), floor + height / 2, front), segment, height, false);
                }
                Fence(copy.transform, beam!, new Vector3(cx, floor + height / 2, front + length), LandPenDefinition.Width, height, false);
                copy.AddComponent<LandPenRegistration>().LocalBox = new Bounds(new Vector3(cx, floor + LandPenDefinition.Height / 2, front + length / 2),
                    new Vector3(LandPenDefinition.Width, LandPenDefinition.Height, length));
                prefab = copy;
            }
            catch { Object.Destroy(copy); throw; }
        }
        static Bounds GateBox(GameObject source)
        {
            var combined = new Bounds(); bool first = true;
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!Usable(filter)) continue;
                var bounds = filter.sharedMesh.bounds;
                foreach (float x in new[] { bounds.min.x, bounds.max.x }) foreach (float y in new[] { bounds.min.y, bounds.max.y }) foreach (float z in new[] { bounds.min.z, bounds.max.z })
                {
                    var point = source.transform.InverseTransformPoint(filter.transform.TransformPoint(new Vector3(x, y, z)));
                    if (first) { combined = new Bounds(point, Vector3.zero); first = false; } else combined.Encapsulate(point);
                }
            }
            if (first || !Finite(combined.center) || !Finite(combined.size)) throw new InvalidOperationException("native gate geometry unavailable");
            return combined;
        }
        static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        static bool Usable(MeshFilter? filter) => filter != null && filter.sharedMesh != null && filter.GetComponent<MeshRenderer>() != null
            && Finite(filter.sharedMesh.bounds.size) && filter.sharedMesh.bounds.size.x > 0 && filter.sharedMesh.bounds.size.y > 0 && filter.sharedMesh.bounds.size.z > 0;
        static void Member(Transform parent, MeshFilter source, Vector3 center, Vector3 direction, float length, float thickness)
        {
            var mesh = source.sharedMesh; var bounds = mesh.bounds;
            int axis = bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z ? 0 : bounds.size.y >= bounds.size.z ? 1 : 2;
            var from = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            var scale = new Vector3(thickness / bounds.size.x, thickness / bounds.size.y, thickness / bounds.size.z); scale[axis] = length / bounds.size[axis];
            var visual = new GameObject("Shepherd pen wood"); visual.transform.SetParent(parent, false);
            visual.transform.localRotation = Quaternion.FromToRotation(from, direction); visual.transform.localScale = scale;
            visual.transform.localPosition = center - visual.transform.localRotation * Vector3.Scale(bounds.center, scale);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
        }
        static void Fence(Transform parent, MeshFilter mesh, Vector3 center, float length, float height, bool alongZ)
        {
            var direction = alongZ ? Vector3.forward : Vector3.right;
            Member(parent, mesh, center + Vector3.up * height * 0.25f, direction, length, 0.14f);
            Member(parent, mesh, center - Vector3.up * height * 0.25f, direction, length, 0.14f);
            var block = new GameObject("Shepherd pen collision"); block.transform.SetParent(parent, false); block.layer = LayerMask.NameToLayer("piece");
            block.transform.localPosition = center;
            block.AddComponent<BoxCollider>().size = alongZ ? new Vector3(0.14f, height, length) : new Vector3(length, height, 0.14f);
        }
        // The hammer gets the pen once per ObjectDB, after a recipe with native materials applied; a corrected recipe adds it without a relog.
        static void AddToHammer()
        {
            if (prefab == null || ObjectDB.instance == null || hammerDatabase == ObjectDB.instance) return;
            var hammer = ObjectDB.instance.GetItemPrefab(TroughPieces.HammerItem)?.GetComponent<ItemDrop>();
            var table = hammer != null ? hammer.m_itemData.m_shared.m_buildPieces : null;
            if (table == null) return;
            if (!table.m_pieces.Contains(prefab)) table.m_pieces.Add(prefab);
            hammerDatabase = ObjectDB.instance;
        }
        static bool ApplyRecipe()
        {
            if (prefab == null || ObjectDB.instance == null) return false;
            string text = recipe?.Invoke() ?? LandPenDefinition.DefaultRecipe;
            if (appliedRecipe == text && prefab.GetComponent<Piece>().m_resources.Length > 0) return true;
            if (rejectedRecipe == text) return false;
            ItemDrop? Item(string name) => ObjectDB.instance.GetItemPrefab(name)?.GetComponent<ItemDrop>();
            var entries = TroughRecipe.Parse(text, name => Item(name) != null);
            var resources = entries.Where(e => Item(e.Item) != null).Select(e => new Piece.Requirement { m_resItem = Item(e.Item)!, m_amount = e.Amount, m_recover = true }).ToArray();
            if (resources.Length == 0) { rejectedRecipe = text; throw new InvalidOperationException("land pen recipe has no native materials"); }
            prefab.GetComponent<Piece>().m_resources = resources; appliedRecipe = text; rejectedRecipe = "";
            return true;
        }
        internal static void Warn(string context, Exception error) => NativeFeeding.Warn(context + ": " + error.GetType().Name + ": " + error.Message);
        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        sealed class SceneAwake { [HarmonyPostfix] static void Postfix() => Install(); }
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        sealed class DatabaseAwake { [HarmonyPostfix] static void Postfix() => Install(); }
        [HarmonyPatch(typeof(Door), nameof(Door.GetHoverText))]
        sealed class GateHover
        {
            [HarmonyPostfix] static void Postfix(Door __instance, ref string __result)
            {
                if (Active && __instance.GetComponent<LandPenRegistration>() != null)
                    __result += "\n" + ShepherdLocalization.Format(LandPenDefinition.CapacityKey, Capacity);
            }
        }
        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
        sealed class Requirements
        {
            [HarmonyPostfix] static void Postfix(Piece piece, ref bool __result) { if (prefab != null && piece == prefab.GetComponent<Piece>() && !Active) __result = false; }
        }
    }
}
