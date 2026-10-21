// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Clean-room deck pen from the pinned TheGreatestShips README behavior; no implementation read.
using System;
using System.Collections.Generic;
using ExtraPotions.Keel.Catalog;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Localization;
using ExtraPotions.Keel.Performance;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Managers;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using PenContract = System.Tuple<System.Func<ZNetView, UnityEngine.Bounds, int, System.IDisposable>, System.Func<UnityEngine.Vector3, System.Tuple<ZNetView, int, bool>>>;

namespace ExtraPotions.Keel.Runtime
{
    internal sealed class KeelPen : MonoBehaviour
    {
        static readonly HashSet<KeelPen> instances = new HashSet<KeelPen>();
        [SerializeField] internal Bounds Box;
        [SerializeField] internal Transform[] Gates = Array.Empty<Transform>();
        [SerializeField] internal BoxCollider[] Lips = Array.Empty<BoxCollider>();
        // Per gate, pen-local: the closed hinge, the open hinge above the hull's rail, the open gate's stretch along its
        // slope, and the boarding ramp from the deck up to the open hinge (null when the rail is no higher than the deck).
        [SerializeField] internal Vector3[] ClosedHinges = Array.Empty<Vector3>();
        [SerializeField] internal Vector3[] OpenHinges = Array.Empty<Vector3>();
        [SerializeField] internal float[] OpenStretch = Array.Empty<float>();
        [SerializeField] internal GameObject?[] Boarding = Array.Empty<GameObject?>();
        Ship? ship;
        ZNetView? view;
        Collider[] collision = Array.Empty<Collider>();
        IDisposable? lease;
        PenContract? provider;
        int registeredCapacity = -1;
        Vector3 registeredScale;
        long generation = -1;
        double nextProvider;
        bool warned;
        // Colliders are switched only when the wanted state changes; the Update below runs every frame for every pen.
        readonly ChangeGate<bool> collisionOn = new ChangeGate<bool>();
        readonly ChangeGate<bool> lipOn = new ChangeGate<bool>();
        bool[] gateOpen = Array.Empty<bool>();
        int[] posed = Array.Empty<int>();
        internal ZNetView? View => view;
        internal bool Live => Plugin.Ready && KeelCatalog.Active && view != null && view.IsValid() && ship != null;
        // Speed over the water: the waves' heave of a moored ship is not movement (PenRules.RestSpeed).
        internal float Speed
        {
            get
            {
                if (ship == null || ship.m_body == null) return 0f;
                var velocity = ship.m_body.linearVelocity;
                return PenRules.RestSpeed(velocity.x, velocity.y, velocity.z);
            }
        }
        // Open() is read for every gate of every pen on every frame; hash the keys once.
        static readonly int[] gateHashes = BuildGateHashes();
        static int[] BuildGateHashes()
        {
            var hashes = new int[PenRules.GatesPerPen];
            for (int i = 0; i < hashes.Length; i++) hashes[i] = PenRules.GateKey(i).GetStableHashCode();
            return hashes;
        }
        internal bool Open(int gate) => view != null && view.IsValid() && view.GetZDO().GetBool(gateHashes[gate], false);
        internal Vector3 GatePoint(int gate) => gate < ClosedHinges.Length ? transform.TransformPoint(ClosedHinges[gate]) : Gates[gate].position;

        internal static bool Build(GameObject root, ShipType type)
        {
            if (!type.HasPen) return true;
            var pole = PrefabManager.Instance.GetPrefab("wood_pole");
            var beam = PrefabManager.Instance.GetPrefab("wood_beam");
            var poleMesh = pole != null ? pole.GetComponentInChildren<MeshFilter>(true) : null;
            var beamMesh = beam != null ? beam.GetComponentInChildren<MeshFilter>(true) : null;
            if (!poleMesh || !beamMesh || !poleMesh.sharedMesh || !beamMesh.sharedMesh
                || !poleMesh.GetComponent<MeshRenderer>() || !beamMesh.GetComponent<MeshRenderer>()) return false;
            if (!Usable(poleMesh.sharedMesh.bounds.size) || !Usable(beamMesh.sharedMesh.bounds.size)) return false;
            var node = new GameObject("Keel deck pen");
            node.SetActive(false);
            node.transform.SetParent(root.transform, false);
            try
            {
                var pen = node.AddComponent<KeelPen>();
                // Root-local footprint. Root catalog scale multiplies it, including Shepherd's inverse transform.
                // Deck height is sampled from the native hull colliders before our own geometry exists.
                float width = type.Ship == KeelShip.SmallByrding ? 2f : 2.4f;
                float length = type.Ship == KeelShip.SmallByrding ? 3f : 6.4f;
                float height = type.Ship == KeelShip.GreaterByrding ? 1.8f : 1.3f;
                float x = width / 2, z = length / 2, gap = 1.6f;
                float floor = Measure(root, x, out var rails);
                pen.Box = new Bounds(new Vector3(0, floor + height / 2, 0), new Vector3(width, height + 0.8f, length));
                var lips = new List<BoxCollider>();
                foreach (float side in new[] { -1f, 1f })
                {
                    foreach (float end in new[] { -1f, 1f })
                    {
                        Member(node.transform, poleMesh, new Vector3(side * x, floor + height / 2, end * z), Vector3.up, height, 0.14f);
                        float section = (length - gap) / 2;
                        var center = new Vector3(side * x, floor + height / 2, end * (gap / 2 + section / 2));
                        Fence(node.transform, beamMesh, center, section, height, true);
                    }
                    var endCenter = new Vector3(0, floor + height / 2, side * z);
                    Fence(node.transform, beamMesh, endCenter, width, height, false);
                    lips.Add(Block(node.transform, new Vector3(side * x, floor + height + 1, 0), new Vector3(0.12f, 2, length)));
                    lips.Add(Block(node.transform, new Vector3(0, floor + height + 1, side * z), new Vector3(width, 2, 0.12f)));
                }
                // Gate gaps join the lip: under way above Containment Speed an open gate no longer lets animals out.
                foreach (float side in new[] { -1f, 1f })
                    lips.Add(Block(node.transform, new Vector3(side * x, floor + height / 2, 0), new Vector3(0.12f, height, gap)));
                pen.Lips = lips.ToArray();
                pen.Gates = new Transform[PenRules.GatesPerPen];
                pen.ClosedHinges = new Vector3[PenRules.GatesPerPen];
                pen.OpenHinges = new Vector3[PenRules.GatesPerPen];
                pen.OpenStretch = new float[PenRules.GatesPerPen];
                pen.Boarding = new GameObject?[PenRules.GatesPerPen];
                // Walkable floor level with the deck: the pen's footprint is solid down past the waterline (PenRules.FloorSolid),
                // plus a thin strip under a gate's gap out to its ramp foot when that lies outboard of the pen side
                // (PenRules.FloorReach).
                float slab = PenRules.FloorThickness;
                var solid = PenRules.FloorSolid(floor);
                Block(node.transform, new Vector3(0, solid.Centre, 0), new Vector3(width, solid.Height, length));
                for (int i = 0; i < pen.Gates.Length; i++)
                {
                    float side = i == 0 ? -1 : 1;
                    var hinge = new GameObject("Keel gate " + i);
                    hinge.transform.SetParent(node.transform, false);
                    hinge.transform.localPosition = new Vector3(side * x, floor, 0);
                    pen.Gates[i] = hinge.transform;
                    // Open, the gate folds outboard and down from above the hull's rail to below the deck, reached by a
                    // boarding ramp from the deck inside the gap (PenRules.OpenRamp; native e5db2aac: an upward gate left
                    // its end 0.92 m above the deck and animals did not climb it).
                    var pose = PenRules.OpenRamp(x, floor, height, rails[i]);
                    pen.ClosedHinges[i] = hinge.transform.localPosition;
                    pen.OpenHinges[i] = new Vector3(side * pose.HingeOut, pose.HingeHeight, 0);
                    pen.OpenStretch[i] = pose.Length / height;
                    float reach = PenRules.FloorReach(x, pose);
                    if (reach > x + 0.01f)
                        Block(node.transform, new Vector3(side * (x + reach) / 2, floor - slab / 2, 0), new Vector3(reach - x, slab, gap));
                    if (pose.BoardingRise > 0.01f && pose.BoardingLength > 0.05f)
                    {
                        var board = new GameObject("Keel gate " + i + " boarding");
                        board.transform.SetParent(node.transform, false);
                        board.transform.localPosition = new Vector3(side * pose.BoardingStart, floor, 0);
                        board.transform.localRotation = Quaternion.Euler(0, 0, PenRules.BoardingAngle(i, pose));
                        float run = pose.BoardingLength, wide = gap - 0.1f;
                        for (int plank = 0; plank < 8; plank++)
                            Member(board.transform, beamMesh, new Vector3(0, run / 2, (plank - 3.5f) * wide / 8), Vector3.up, run, wide / 8);
                        Block(board.transform, new Vector3(0, run / 2, 0), new Vector3(0.15f, run, wide));
                        board.SetActive(false);
                        pen.Boarding[i] = board;
                    }
                    if (!pose.Walkable)
                        KeelCatalog.Warn("deck pen gate " + i, new InvalidOperationException("the boarding ramp over the rail is "
                            + pose.BoardingSlope.ToString("0") + " degrees, steeper than animals climb"));
                    // Solid plank ramp; wood mesh copies contain no native gameplay components.
                    for (int plank = 0; plank < 8; plank++)
                        Member(hinge.transform, beamMesh, new Vector3(0, height / 2, (plank - 3.5f) * gap / 8), Vector3.up, height, gap / 8);
                    var collider = Block(hinge.transform, new Vector3(0, height / 2, 0), new Vector3(0.15f, height, gap));
                    var handle = collider.gameObject.AddComponent<KeelPenGate>();
                    handle.Index = i;
                }
                node.SetActive(true);
                return true;
            }
            catch (Exception error) { Destroy(node); KeelCatalog.Warn("deck pen construction", error); return false; }
        }

        /// <summary>
        /// The deck height under the pen and, per gate side (0 port, 1 starboard), the hull's top surface outboard of the pen
        /// side: (distance outboard, height) from downward rays across the gate's width, root-local.
        /// </summary>
        static float Measure(GameObject root, float penSide, out List<(float Out, float Height)>[] rails)
        {
            // Catalog templates are inactive. Copy only actual collision shapes into an owned active probe;
            // never activate a ship or clone its gameplay components to make Collider.Raycast available.
            var probe = new GameObject("Keel deck measurement") { hideFlags = HideFlags.HideAndDontSave, layer = 2 };
            try
            {
                var colliders = new List<Collider>();
                foreach (var original in root.GetComponentsInChildren<Collider>(true))
                {
                    if (!original.enabled || original.isTrigger || !ActiveBelowRoot(original.transform, root.transform)) continue;
                    var node = new GameObject(original.name) { layer = 2 }; // IgnoreRaycast; direct Collider.Raycast still measures it.
                    node.transform.SetParent(probe.transform, false);
                    node.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
                    node.transform.localScale = original.transform.lossyScale;
                    var shape = CopyShape(original, node);
                    if (shape != null) colliders.Add(shape);
                }
                Physics.SyncTransforms();
                var heights = new List<float>();
                foreach (float x in new[] { -0.6f, 0.6f }) foreach (float z in new[] { -1.4f, 1.4f })
                {
                    float highest = Highest(root, colliders, new Vector3(x, 4, z));
                    if (!float.IsNegativeInfinity(highest)) heights.Add(highest);
                }
                if (heights.Count < 2) throw new InvalidOperationException("native ship deck collider could not be measured");
                heights.Sort();
                rails = new[] { new List<(float Out, float Height)>(), new List<(float Out, float Height)>() };
                for (int gate = 0; gate < rails.Length; gate++)
                    for (int step = 1; step <= 80; step++)
                    {
                        float outboard = penSide + step * 0.05f, top = float.NegativeInfinity;
                        foreach (float z in new[] { -0.6f, 0f, 0.6f })
                            top = Mathf.Max(top, Highest(root, colliders, new Vector3(gate == 0 ? -outboard : outboard, 4, z)));
                        if (!float.IsNegativeInfinity(top)) rails[gate].Add((outboard, top));
                    }
                return heights[(heights.Count - 1) / 2] + 0.04f;
            }
            finally { DestroyImmediate(probe); }
        }

        static float Highest(GameObject root, List<Collider> colliders, Vector3 from)
        {
            var ray = new Ray(root.transform.TransformPoint(from), -root.transform.up);
            float highest = float.NegativeInfinity;
            foreach (var collider in colliders)
                if (collider.Raycast(ray, out var hit, 8 * root.transform.lossyScale.y))
                    highest = Mathf.Max(highest, root.transform.InverseTransformPoint(hit.point).y);
            return highest;
        }

        static bool ActiveBelowRoot(Transform node, Transform root)
        {
            for (var current = node; current != root; current = current.parent)
                if (current == null || !current.gameObject.activeSelf) return false;
            return true;
        }

        static Collider? CopyShape(Collider original, GameObject node)
        {
            if (original is BoxCollider box)
            {
                var copy = node.AddComponent<BoxCollider>(); copy.center = box.center; copy.size = box.size; return copy;
            }
            if (original is MeshCollider mesh && mesh.sharedMesh != null)
            {
                var copy = node.AddComponent<MeshCollider>(); copy.cookingOptions = mesh.cookingOptions;
                copy.convex = mesh.convex; copy.sharedMesh = mesh.sharedMesh; return copy;
            }
            if (original is SphereCollider sphere)
            {
                var copy = node.AddComponent<SphereCollider>(); copy.center = sphere.center; copy.radius = sphere.radius; return copy;
            }
            if (original is CapsuleCollider capsule)
            {
                var copy = node.AddComponent<CapsuleCollider>(); copy.center = capsule.center; copy.radius = capsule.radius;
                copy.height = capsule.height; copy.direction = capsule.direction; return copy;
            }
            return null;
        }

        static void Fence(Transform parent, MeshFilter mesh, Vector3 center, float length, float height, bool alongZ)
        {
            Vector3 direction = alongZ ? Vector3.forward : Vector3.right;
            Member(parent, mesh, center + Vector3.up * height * 0.25f, direction, length, 0.14f);
            Member(parent, mesh, center - Vector3.up * height * 0.25f, direction, length, 0.14f);
            Block(parent, center, alongZ ? new Vector3(0.14f, height, length) : new Vector3(length, height, 0.14f));
        }

        static void Member(Transform parent, MeshFilter source, Vector3 center, Vector3 direction, float length, float thickness)
        {
            var mesh = source.sharedMesh;
            var bounds = mesh.bounds;
            int axis = bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z ? 0 : bounds.size.y >= bounds.size.z ? 1 : 2;
            Vector3 from = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            var scale = new Vector3(thickness / bounds.size.x, thickness / bounds.size.y, thickness / bounds.size.z);
            scale[axis] = length / bounds.size[axis];
            var visual = new GameObject("Keel wood");
            visual.transform.SetParent(parent, false);
            visual.transform.localRotation = Quaternion.FromToRotation(from, direction);
            visual.transform.localScale = scale;
            visual.transform.localPosition = center - visual.transform.localRotation * Vector3.Scale(bounds.center, scale);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
        }
        static bool Usable(Vector3 size) => size.x > 0 && size.y > 0 && size.z > 0
            && !float.IsInfinity(size.x) && !float.IsInfinity(size.y) && !float.IsInfinity(size.z);

        static BoxCollider Block(Transform parent, Vector3 center, Vector3 size)
        {
            var block = new GameObject("Keel pen collision");
            block.layer = LayerMask.NameToLayer("piece");
            block.transform.SetParent(parent, false);
            block.transform.localPosition = center;
            var collider = block.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }

        void Awake()
        {
            instances.Add(this);
            ship = GetComponentInParent<Ship>();
            view = ship != null ? ship.GetComponent<ZNetView>() : null;
            collision = GetComponentsInChildren<Collider>(true);
        }
        void Update()
        {
            if (!Live) { Release(); ApplyCollision(false); return; }
            if (gateOpen.Length != Gates.Length) gateOpen = new bool[Gates.Length];
            if (posed.Length != Gates.Length) { posed = new int[Gates.Length]; for (int i = 0; i < posed.Length; i++) posed[i] = -1; }
            for (int i = 0; i < Gates.Length; i++)
            {
                gateOpen[i] = Open(i);
                int state = gateOpen[i] ? 1 : 0;
                if (posed[i] != state) { Pose(i, gateOpen[i]); posed[i] = state; }
            }
            bool lip = PenRules.LipActive(Speed, Plugin.Settings!.Get(KeelSettings.Pens.ContainmentSpeed), PenRules.EveryGateClosed(gateOpen));
            ApplyCollision(true);
            if (lipOn.Changed(lip)) foreach (var collider in Lips) collider.enabled = lip;
            RegisterPen();
        }
        /// <summary>Moves gate <paramref name="i"/> to its closed or open shape (only when its state changes).</summary>
        void Pose(int i, bool open)
        {
            var gate = Gates[i];
            bool shaped = i < ClosedHinges.Length && i < OpenHinges.Length && i < OpenStretch.Length;
            if (shaped) gate.localPosition = open ? OpenHinges[i] : ClosedHinges[i];
            gate.localRotation = Quaternion.Euler(0, 0, PenRules.GateAngle(i, open));
            gate.localScale = open && shaped && OpenStretch[i] > 0f ? new Vector3(1, OpenStretch[i], 1) : Vector3.one;
            var board = i < Boarding.Length ? Boarding[i] : null;
            if (board != null) board.SetActive(open);
        }

        void SetCollision(bool enabled) { foreach (var collider in collision) collider.enabled = enabled; }

        /// <summary>All colliders on or off, once per change; the lips are reapplied after switching on, as every frame did before.</summary>
        void ApplyCollision(bool enabled)
        {
            if (!collisionOn.Changed(enabled)) return;
            SetCollision(enabled);
            lipOn.Reset();
        }

        void RegisterPen()
        {
            if (Time.realtimeSinceStartupAsDouble < nextProvider) return;
            nextProvider = Time.realtimeSinceStartupAsDouble + 0.5;
            var registry = CruciblePlugin.Services.Capabilities;
            var entries = registry.Providers(PenRules.Capability);
            PenContract? current = null;
            if (entries.Count == 1 && entries[0].Owner == "com.meadworks.shepherd") registry.TryResolve(PenRules.Capability, new System.Version(1, 0), out current);
            var type = ShipCatalog.TryGetByPrefab(Utils.GetPrefabName(ship!.gameObject), out var found) ? found : null;
            int capacity = type != null ? Plugin.Settings!.Get(KeelSettings.Catalog(type.Ship).PenCapacity!) : 0;
            var scale = ship.transform.lossyScale;
            long session = CruciblePlugin.Session.Generation;
            if (ReferenceEquals(provider, current) && lease != null && capacity == registeredCapacity && scale == registeredScale && generation == session) return;
            Release();
            if (current == null) return;
            try
            {
                lease = current.Item1(view!, Box, PenRules.HerdLimit(capacity));
                provider = current; registeredCapacity = capacity; registeredScale = scale; generation = session;
            }
            catch (Exception error) { if (!warned) KeelCatalog.Warn("Shepherd pen registration", error); warned = true; }
        }
        void Release() { try { lease?.Dispose(); } catch (Exception error) { if (!warned) KeelCatalog.Warn("Shepherd pen disposal", error); warned = true; } lease = null; provider = null; }
        void OnDisable() => Release();
        void OnDestroy() { Release(); instances.Remove(this); }
        internal static void Shutdown()
        {
            foreach (var pen in new List<KeelPen>(instances)) if (pen != null) { pen.Release(); pen.SetCollision(false); pen.collisionOn.Reset(); pen.lipOn.Reset(); }
        }
    }

    internal sealed class KeelPenGate : MonoBehaviour, Hoverable, Interactable
    {
        [SerializeField] internal int Index;
        KeelPen? Pen => GetComponentInParent<KeelPen>();
        public string GetHoverName() => Pen != null ? KeelCatalog.ShipName(Pen.GetComponentInParent<Ship>()) : "";
        public float GetHoverOffset() => 0f;
        public string GetHoverText() => Pen != null && Pen.Live ? GetHoverName() + "\n[<color=yellow><b>$KEY_Use</b></color>] " + KeelText.Text(Pen.Open(Index) ? KeelTextKeys.GateClose : KeelTextKeys.GateOpen) : "";
        public bool Interact(Humanoid user, bool hold, bool alt) => !hold && user == Player.m_localPlayer && Pen != null && KeelPenCommands.Use(Pen, Index);
        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
    }
}
