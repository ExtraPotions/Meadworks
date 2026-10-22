// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using ExtraPotions.Bestiary.Spawning;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Adapters.Kit
{
    /// <summary>Local vanilla props on a graphical client. Materials and textures stay as the game shipped them.</summary>
    internal static class KitLook
    {
        public static void Dress(GameObject creature, string name)
        {
            GameObject source = null;
            var chosen = StandInProps.Choose(name, MeadworksHeadless.AllowsUi, prefab =>
            {
                source = Find(prefab);
                return source != null;
            });
            if (chosen == null || source == null)
            {
                if (MeadworksHeadless.AllowsUi && StandInProps.Names(name).Length > 0)
                    Log.Info("Bestiary " + name + " stand-in prop is missing in this game build; the fight still runs.");
                return;
            }
            // The creature copy is still on the inactive prefab bench. A live parent would wake the item.
            if (creature.activeInHierarchy)
            {
                Log.Info("Bestiary " + name + " stand-in prop skipped on an active object; the fight still runs.");
                return;
            }
            if (name == KitCreatures.SlagCharred)
            {
                var chest = Chest(creature);
                int count = StandInProps.Copies(name);
                for (int i = 0; i < count; i++)
                    Hang(creature.transform, chest, source, StandInProps.PlateMeters, StandInProps.PlateGrip(i, chest == creature.transform), true, false);
                return;
            }
            var hand = Hand(creature, out var onBody);
            Hang(creature.transform, hand, source, StandInProps.Meters(name), StandInProps.HoldGrip(name, onBody), false, name == KitCreatures.Javelineer && !onBody);
        }

        private static Transform Hand(GameObject creature, out bool onBody)
        {
            onBody = false;
            var equipment = creature.GetComponentInChildren<VisEquipment>(true);
            if (equipment != null && equipment.m_rightHand != null) return equipment.m_rightHand;
            var named = FindNamed(creature.transform, "RightHand_Attach");
            if (named != null) return named;
            var animator = creature.GetComponentInChildren<Animator>(true);
            var bone = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (bone != null) return bone;
            onBody = true;
            return creature.transform;
        }

        private static Transform Chest(GameObject creature)
        {
            var animator = creature.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                var bone = animator.GetBoneTransform(HumanBodyBones.Chest)
                    ?? animator.GetBoneTransform(HumanBodyBones.UpperChest)
                    ?? animator.GetBoneTransform(HumanBodyBones.Spine);
                if (bone != null) return bone;
            }
            return FindNamed(creature.transform, "Chest")
                ?? FindNamed(creature.transform, "Spine2")
                ?? FindNamed(creature.transform, "Spine1")
                ?? FindNamed(creature.transform, "Spine")
                ?? creature.transform;
        }

        private static GameObject Find(string prefab)
        {
            var scene = ZNetScene.instance;
            if (scene != null)
            {
                var found = scene.GetPrefab(prefab);
                if (found != null) return found;
            }
            var db = ObjectDB.instance;
            return db != null ? db.GetItemPrefab(prefab) : null;
        }

        private static void Hang(Transform body, Transform parent, GameObject source, float meters, StandInProps.Grip grip, bool faceOut, bool alongShaft)
        {
            var prop = UnityEngine.Object.Instantiate(Visual(source), parent, false);
            prop.name = "mw_prop";
            prop.SetActive(true);
            // The game spells this child "equiped".
            var equipped = prop.transform.Find("equiped");
            if (equipped != null) equipped.gameObject.SetActive(true);
            Strip(prop);
            var t = prop.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            LocalBounds(t, out var min, out var max);
            var size = max - min;
            float unit = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            var bone = parent.lossyScale;
            var local = PropFit.Scale(PropFit.WorldScale(meters, unit), new PropFit.Axes(bone.x, bone.y, bone.z));
            t.localScale = new Vector3(local.X, local.Y, local.Z);
            if (faceOut) FacePlate(body, t, min, max, Outward(grip));
            if (alongShaft) AimSpear(body, t, min, max);
            var world = body.TransformVector(new Vector3(grip.Right, grip.Up, grip.Forward));
            var placed = PropFit.IntoBone(
                new PropFit.Axes(world.x, world.y, world.z),
                Axes(parent.right), Axes(parent.up), Axes(parent.forward),
                new PropFit.Axes(bone.x, bone.y, bone.z));
            t.localPosition = new Vector3(placed.X, placed.Y, placed.Z);
        }

        private static PropFit.Axes Axes(Vector3 v) => new PropFit.Axes(v.x, v.y, v.z);

        // The lift is placement. Facing uses the horizontal part so a raised plate stays upright.
        private static Vector3 Outward(StandInProps.Grip grip)
        {
            var flat = new Vector3(grip.Right, 0f, grip.Forward);
            return flat.sqrMagnitude > 1e-6f ? flat : Vector3.forward;
        }

        private static void FacePlate(Transform body, Transform prop, Vector3 min, Vector3 max, Vector3 outward)
        {
            var size = max - min;
            var scale = prop.localScale;
            float ax = Mathf.Abs(scale.x);
            float ay = Mathf.Abs(scale.y);
            float az = Mathf.Abs(scale.z);
            var mid = (min + max) * 0.5f;
            var front = PropFit.Front(
                new PropFit.Axes(size.x * ax, size.y * ay, size.z * az),
                new PropFit.Axes(mid.x * ax, mid.y * ay, mid.z * az));
            var thin = new Vector3(Mathf.Abs(front.X), Mathf.Abs(front.Y), Mathf.Abs(front.Z));
            if (thin.sqrMagnitude < 1e-8f || outward.sqrMagnitude < 1e-8f) return;
            var thinWorld = prop.rotation * thin;
            var outWorld = body.TransformDirection(outward.normalized);
            if (thinWorld.sqrMagnitude < 1e-8f || outWorld.sqrMagnitude < 1e-8f) return;
            prop.rotation = Quaternion.FromToRotation(thinWorld, outWorld) * prop.rotation;
            // The positive thin side of the buckler is the grip. A half turn about the creature's up swaps the dome outward and leaves the plate upright.
            if (PropFit.FacesNegative(front))
                prop.rotation = Quaternion.AngleAxis(180f, body.up) * prop.rotation;
        }

        // Width picks the head. The idle pose puts GoblinSpear's wide end on the ground, so world height is the wrong end.
        private static void AimSpear(Transform body, Transform prop, Vector3 min, Vector3 max)
        {
            var size = max - min;
            var scale = prop.localScale;
            float sx = Mathf.Abs(size.x * scale.x);
            float sy = Mathf.Abs(size.y * scale.y);
            float sz = Mathf.Abs(size.z * scale.z);
            int axis = sx >= sy && sx >= sz ? 0 : sy >= sz ? 1 : 2;
            float axisMin = axis == 0 ? min.x : axis == 1 ? min.y : min.z;
            float axisMax = axis == 0 ? max.x : axis == 1 ? max.y : max.z;
            var mid = (min + max) * 0.5f;
            EndSpreads(prop, axis, axisMin, axisMax, mid, out float spreadMin, out float spreadMax);
            bool headAtMax = PropFit.HeadAtMax(spreadMin, spreadMax);
            var aim = PropFit.SpearAim(StandInProps.SpearTiltDegrees);
            var headLocal = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            if (!headAtMax) headLocal = -headLocal;
            var headWorld = prop.TransformDirection(headLocal);
            var aimWorld = body.TransformDirection(new Vector3(aim.X, aim.Y, aim.Z));
            if (headWorld.sqrMagnitude > 1e-8f && aimWorld.sqrMagnitude > 1e-8f)
                prop.rotation = Quaternion.FromToRotation(headWorld, aimWorld) * prop.rotation;
            // A head at max Z means the butt is min Z.
            float delta = PropFit.ShaftSlide(axisMin, axisMax, headAtMax, StandInProps.SpearFromButt);
            if (Mathf.Abs(delta) < 1e-6f) return;
            var shift = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            shift *= delta;
            for (int i = 0; i < prop.childCount; i++)
                prop.GetChild(i).localPosition += shift;
        }

        private static void EndSpreads(Transform prop, int axis, float axisMin, float axisMax, Vector3 mid, out float spreadMin, out float spreadMax)
        {
            var along = new List<float>();
            var off = new List<float>();
            void Take(Transform meshTransform, Mesh mesh)
            {
                if (mesh == null || !mesh.isReadable) return;
                var verts = mesh.vertices;
                for (int i = 0; i < verts.Length; i++)
                {
                    var local = prop.InverseTransformPoint(meshTransform.TransformPoint(verts[i]));
                    along.Add(axis == 0 ? local.x : axis == 1 ? local.y : local.z);
                    off.Add(PropFit.OffShaft(local.x - mid.x, local.y - mid.y, local.z - mid.z, axis));
                }
            }
            var filters = prop.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
                if (filters[i].sharedMesh != null) Take(filters[i].transform, filters[i].sharedMesh);
            var skins = prop.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skins.Length; i++)
                if (skins[i].sharedMesh != null) Take(skins[i].transform, skins[i].sharedMesh);
            PropFit.EndSpreads(along, off, axisMin, axisMax, out spreadMin, out spreadMax, out _, out _);
        }

        private static GameObject Visual(GameObject source)
        {
            var attach = source.transform.Find("attach");
            if (attach != null && HasMesh(attach)) return attach.gameObject;
            return source;
        }

        private static bool HasMesh(Transform root)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
                if (filters[i].sharedMesh != null) return true;
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skins.Length; i++)
                if (skins[i].sharedMesh != null) return true;
            return false;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }

        private static void LocalBounds(Transform prop, out Vector3 min, out Vector3 max)
        {
            var lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var hi = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var any = false;
            void Take(Transform meshTransform, Bounds box)
            {
                var center = box.center;
                var extents = box.extents;
                for (int i = 0; i < 8; i++)
                {
                    var corner = center + new Vector3(
                        (i & 1) == 0 ? -extents.x : extents.x,
                        (i & 2) == 0 ? -extents.y : extents.y,
                        (i & 4) == 0 ? -extents.z : extents.z);
                    var local = prop.InverseTransformPoint(meshTransform.TransformPoint(corner));
                    lo = Vector3.Min(lo, local);
                    hi = Vector3.Max(hi, local);
                    any = true;
                }
            }
            var filters = prop.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
                if (filters[i].sharedMesh != null) Take(filters[i].transform, filters[i].sharedMesh.bounds);
            var skins = prop.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skins.Length; i++)
                if (skins[i].sharedMesh != null) Take(skins[i].transform, skins[i].sharedMesh.bounds);
            min = any ? lo : Vector3.zero;
            max = any ? hi : Vector3.zero;
        }

        private static void Strip(GameObject prop)
        {
            // PropFit.StripPasses: a ConfigurableJoint keeps its Rigidbody, and Unity logs an error, if the body goes first.
            foreach (var pass in PropFit.StripPasses) StripPass(prop, pass);
            if (prop.GetComponentInChildren<Joint>(true) != null || prop.GetComponentInChildren<Rigidbody>(true) != null)
                foreach (var pass in PropFit.StripPasses) StripPass(prop, pass);
            foreach (var body in prop.GetComponentsInChildren<Rigidbody>(true))
            {
                if (body == null) continue;
                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }

        private static void StripPass(GameObject prop, string pass)
        {
            if (pass == PropFit.StripJoint)
            {
                foreach (var joint in prop.GetComponentsInChildren<Joint>(true))
                    if (joint != null) UnityEngine.Object.DestroyImmediate(joint);
                return;
            }
            if (pass == PropFit.StripBody)
            {
                foreach (var body in prop.GetComponentsInChildren<Rigidbody>(true))
                {
                    if (body == null) continue;
                    body.isKinematic = true;
                    body.detectCollisions = false;
                    UnityEngine.Object.DestroyImmediate(body);
                }
                return;
            }
            var doomed = new List<Component>();
            foreach (var component in prop.GetComponentsInChildren<Component>(true))
            {
                if (component == null || Kept(component) || component is Joint || component is Rigidbody) continue;
                doomed.Add(component);
            }
            foreach (var component in doomed)
                if (component != null) UnityEngine.Object.DestroyImmediate(component);
        }

        private static bool Kept(Component component) =>
            component is Transform || component is MeshFilter || component is MeshRenderer || component is SkinnedMeshRenderer || component is LODGroup || component is Light;
    }
}
