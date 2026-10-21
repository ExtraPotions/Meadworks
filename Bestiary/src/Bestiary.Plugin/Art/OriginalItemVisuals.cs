// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using ExtraPotions.Bestiary.Catalog;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Crossbow;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.OriginalArt;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Bestiary.Runtime.Prefabs
{
    internal static class OriginalItemVisuals
    {
        internal static int Applied { get; private set; }
        internal static void Install(Harmony harmony, MeadworksLog log) => NetPrefabs.OnSceneAwake(harmony, scene => Apply(scene, log));

        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        static bool Under(Transform transform, string name)
        {
            for (var cursor = transform; cursor != null; cursor = cursor.parent) if (cursor.name == name) return true;
            return false;
        }

        static void Apply(ZNetScene scene, MeadworksLog log)
        {
            Applied = 0;
            if (MeadworksHeadless.IsHeadless) return;
            foreach (var definition in ItemCatalog.All)
            {
                try
                {
                    var prefab = scene.GetPrefab(definition.Prefab);
                    if (prefab == null || !Native.Adapters.NativeRuntime.Owns(prefab)) continue;
                    if (Find(prefab.transform, "meadworks_original_model") == null)
                    {
                        if (definition.Prefab == XbowItem.PrefabName) Crossbow(scene, prefab);
                        else Replace(scene, prefab, definition.Prefab, definition.Kind == ItemKind.Weapon || definition.Kind == ItemKind.Shield);
                    }
                    prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons = new[] { OriginalItemArt.Icon(definition.Prefab) };
                    Applied++;
                }
                catch (Exception error) { log.Warning("Original item art unavailable for " + definition.Prefab + "; registered prefab retained: " + error.Message); }
            }
            TryProjectile(scene, "ECP_ArrowBone_projectile", "ECP_ArrowBone", -1.128f, log);
            TryProjectile(scene, "ECP_SkeletonArrow_projectile", "ECP_ArrowBone", -1.128f, log);
            TryProjectile(scene, "ECP_BoltBoneBlunt_projectile", "ECP_BoltBoneBlunt", -.52f, log);
            log.Verbose("Bestiary original item models/icons applied: " + Applied);
        }

        static void TryProjectile(ZNetScene scene, string projectile, string item, float offset, MeadworksLog log)
        {
            try { ReplaceProjectile(scene, projectile, item, offset); }
            catch (Exception error) { log.Warning("Original projectile art unavailable for " + projectile + "; registered prefab retained: " + error.Message); }
        }

        static void Replace(ZNetScene scene, GameObject owner, string id, bool held)
        {
            var holder = owner.transform.Find("attach") ?? owner.transform.Find("model") ?? owner.transform;
            Quaternion? shieldTurn = null;
            if (id == "ECP_ShieldKraken")
            {
                var nativeModel = holder.Find("model");
                if (nativeModel == null) throw new InvalidOperationException("Kraken shield native grip frame missing");
                shieldTurn = nativeModel.localRotation;
            }
            var originals = holder.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
            var colliders = owner.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray();
            var visual = OriginalItemArt.Instantiate(scene, id, holder);
            if (shieldTurn.HasValue)
            {
                var turn = shieldTurn.Value; visual.transform.localRotation = turn;
                visual.transform.localPosition = -(turn * new Vector3(0f, 0f, -.09f));
            }
            if (id == "ECP_ArrowBone" || id == "ECP_BoltBoneBlunt") visual.transform.localPosition = -ModelBounds.In(visual, holder).center;
            foreach (var part in visual.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = holder.gameObject.layer;
            foreach (var renderer in originals) renderer.enabled = false;
            foreach (var collider in colliders) collider.enabled = false;
            foreach (var group in holder.GetComponentsInChildren<LODGroup>(true)) group.enabled = false;
            if (held)
            {
                var bounds = ModelBounds.In(visual, holder);
                Native.Arsenal.ArsenalLook.Glow(holder, bounds);
            }
        }

        static void Crossbow(ZNetScene scene, GameObject item)
        {
            var slot = Find(item.transform, XbowPlayerRig.Slot);
            if (slot == null) throw new InvalidOperationException("Bone crossbow animated rig missing");
            var frame = slot.Find("model") ?? slot;
            var body = Find(frame, "ecp_xbow_crossbow");
            if (body == null) throw new InvalidOperationException("Bone crossbow grip frame missing");
            var oldRenderers = frame.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
            var oldColliders = item.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray();
            var visual = OriginalItemArt.Instantiate(scene, XbowItem.PrefabName, body);
            foreach (var part in visual.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = frame.gameObject.layer;
            foreach (var renderer in oldRenderers)
                if (!Under(renderer.transform, "ecp_xbow_string_a") && !Under(renderer.transform, "ecp_xbow_string_b") && !Under(renderer.transform, XbowPlayerRig.GrooveBolt)) renderer.enabled = false;
            foreach (var collider in oldColliders) collider.enabled = false;
            foreach (string name in new[] { "ecp_xbow_string_a", "ecp_xbow_string_b" })
            {
                var old = Find(frame, name); var authored = Find(visual.transform, name);
                if (old == null || authored == null || old == authored) throw new InvalidOperationException("Crossbow string contract missing: " + name);
                var sourceMesh = authored.GetComponent<MeshFilter>(); var sourceRenderer = authored.GetComponent<MeshRenderer>();
                if (sourceMesh == null || sourceMesh.sharedMesh == null || sourceRenderer == null || sourceRenderer.sharedMaterial == null)
                    throw new InvalidOperationException("Crossbow authored cord mesh/material missing: " + name);
                // The animated legacy node is an FBX wrapper; its mesh is on a child.
                foreach (var renderer in old.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled = false;
                var filter = old.GetComponent<MeshFilter>() ?? old.gameObject.AddComponent<MeshFilter>();
                var replacement = old.GetComponent<MeshRenderer>() ?? old.gameObject.AddComponent<MeshRenderer>();
                filter.sharedMesh = sourceMesh.sharedMesh; replacement.sharedMaterial = sourceRenderer.sharedMaterial; replacement.enabled = true;
                Object.DestroyImmediate(authored.gameObject);
            }
            foreach (string name in new[] { "ecp_xbow_tip_a", "ecp_xbow_tip_b", "ecp_xbow_rest", "ecp_xbow_nut", "ecp_xbow_muzzle", "ecp_xbow_support" })
            {
                var duplicate = Find(visual.transform, name); if (duplicate != null) Object.DestroyImmediate(duplicate.gameObject);
            }
            var groove = Find(frame, XbowPlayerRig.GrooveBolt);
            if (groove != null)
            {
                var old = groove.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
                var bolt = OriginalItemArt.Instantiate(scene, "ECP_BoltBoneBlunt", groove);
                foreach (var collider in bolt.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var renderer in old) renderer.enabled = false;
            }
            var handBolt = Object.Instantiate(OriginalItemArt.Model(scene, "ECP_BoltBoneBlunt"));
            handBolt.name = "meadworks_original_hand_bolt";
            foreach (var collider in handBolt.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            Object.DontDestroyOnLoad(handBolt);
            XbowPlayerRig.Use(handBolt, handBolt.GetComponentInChildren<Renderer>(true).sharedMaterial);
            Native.Adapters.NativeRuntime.Own(handBolt);
        }

        static void ReplaceProjectile(ZNetScene scene, string projectile, string item, float offset)
        {
            var prefab = scene.GetPrefab(projectile);
            if (prefab == null || !Native.Adapters.NativeRuntime.Owns(prefab) || Find(prefab.transform, "meadworks_original_model") != null) return;
            var originals = prefab.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
            var model = OriginalItemArt.Instantiate(scene, item, prefab.transform); model.transform.localPosition = new Vector3(0, 0, offset);
            foreach (var part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = prefab.layer;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var renderer in originals) renderer.enabled = false;
        }
    }
}
