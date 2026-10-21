// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The projectile of Throwing Grip (effect <c>throwable</c>): <c>ECF_ThrownWeapon</c>, a copy of the flint spear's
    /// thrown projectile, built from code once per process and added to every net scene's prefab list before its Awake,
    /// on every peer (server, host, clients), so the projectile ZDO a thrower creates exists everywhere. It keeps the
    /// spear projectile's flight, hit and item drop (Projectile.m_respawnItemOnHit: it lands as the very item thrown,
    /// with all its data), spins, and drops its item when its time runs out instead of losing it.
    /// <para>
    /// The look: the thrower writes the thrown item's prefab name into the projectile's ZDO under the game's own
    /// <c>visual</c> key (as the game's catapult does), and every client's Projectile, set to change visuals, swaps its
    /// mesh for that item's attach model (the model an item stand shows). An item without one keeps the spear's mesh.
    /// </para>
    /// Also holds the spear throw itself: the attack a thrown swing copies (<see cref="ThrowSwing"/>).
    /// </summary>
    internal static class ThrownWeapon
    {
        public const string PrefabName = "ECF_ThrownWeapon";
        private const string TemplateItem = "SpearFlint";
        private const float SpinDegreesPerSecond = 900f;

        public static readonly int Hash = PrefabName.GetStableHashCode();

        private static GameObject? _prefab;

        /// <summary>The spear throw a thrown swing copies, its projectile ours; null until built (or with no spear).</summary>
        public static Attack? Throw { get; private set; }

        /// <summary>ZNetScene.Awake prefix, every peer: built once, then added to each new scene's list.</summary>
        public static void Register(List<GameObject> prefabs)
        {
            if (_prefab == null)
            {
                Build(prefabs);
            }
            if (_prefab != null && !prefabs.Contains(_prefab))
            {
                prefabs.Add(_prefab);
            }
        }

        /// <summary>Whether the projectile is a thrown-weapon projectile of ours (by its ZDO's prefab).</summary>
        public static bool IsOurs(Projectile projectile)
        {
            ZNetView view = projectile.m_nview;
            ZDO? zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            return zdo != null && zdo.GetPrefab() == Hash;
        }

        /// <summary>
        /// The thrower's client, right after the projectile is set up: every client's copy shows the thrown item's own
        /// model. Only an item the object database knows and that has an attach model is named, so no client's visual
        /// swap looks up a missing prefab.
        /// </summary>
        public static void ShowItem(Projectile projectile, ItemDrop.ItemData item)
        {
            GameObject prefab = item.m_dropPrefab;
            if (prefab == null || ObjectDB.instance == null || !IsOurs(projectile))
            {
                return;
            }
            if (ObjectDB.instance.GetItemPrefab(prefab.name) != null && ItemStand.GetAttachPrefab(prefab) != null)
            {
                projectile.m_nview.GetZDO().Set(ZDOVars.s_visual, prefab.name);
            }
        }

        private static void Build(List<GameObject> prefabs)
        {
            Attack? spear = SpearThrow(prefabs);
            if (spear == null)
            {
                Log.Warn("no spear throw found to copy: Throwing Grip does nothing");
                return;
            }
            GameObject holder = new GameObject("ExtraPotions.Inscribe.Adopted_ThrownWeapon");
            global::ExtraPotions.Inscribe.Runtime.NativeGameplay.Own(holder);
            holder.SetActive(false);
            Object.DontDestroyOnLoad(holder);
            GameObject clone = Object.Instantiate(spear.m_attackProjectile, holder.transform, false);
            clone.name = PrefabName;
            Configure(clone.GetComponent<Projectile>());
            Attack thrown = spear.Clone();
            thrown.m_attackProjectile = clone;
            thrown.m_consumeItem = true;
            thrown.m_projectiles = 1;
            Throw = thrown;
            _prefab = clone;
        }

        // The flint spear's secondary attack, else the first spear throw that carries its item.
        private static Attack? SpearThrow(List<GameObject> prefabs)
        {
            Attack? first = null;
            foreach (GameObject go in prefabs)
            {
                Attack? attack = go != null ? ThrowOf(go) : null;
                if (attack != null && go!.name == TemplateItem)
                {
                    return attack;
                }
                first ??= attack;
            }
            return first;
        }

        private static Attack? ThrowOf(GameObject go)
        {
            ItemDrop drop = go.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData?.m_shared == null || drop.m_itemData.m_shared.m_skillType != Skills.SkillType.Spears)
            {
                return null;
            }
            Attack attack = drop.m_itemData.m_shared.m_secondaryAttack;
            if (attack == null || attack.m_attackType != Attack.AttackType.Projectile || !attack.m_consumeItem || attack.m_attackProjectile == null)
            {
                return null;
            }
            Projectile projectile = attack.m_attackProjectile.GetComponent<Projectile>();
            return projectile != null && projectile.m_respawnItemOnHit ? attack : null;
        }

        private static void Configure(Projectile projectile)
        {
            projectile.m_respawnItemOnHit = true;
            projectile.m_spawnOnTtl = true;
            projectile.m_rotateVisual = SpinDegreesPerSecond;
            if (projectile.m_visual == null)
            {
                projectile.m_visual = Model(projectile.transform);
            }
            projectile.m_canChangeVisuals = true;
        }

        // The spear's own mesh (the look of an item without an attach model); an empty child when there is none, so the
        // game's visual swap, which hides the old visual first, always has one.
        private static GameObject Model(Transform root)
        {
            MeshRenderer mesh = root.GetComponentInChildren<MeshRenderer>(true);
            if (mesh != null)
            {
                return mesh.gameObject;
            }
            GameObject empty = new GameObject("visual");
            empty.transform.SetParent(root, false);
            return empty;
        }
    }

    /// <summary>Every peer: the thrown-weapon projectile is in the net scene's list before its lookup is built.</summary>
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    internal static class ThrownWeaponRegistration
    {
        private static void Prefix(ZNetScene __instance) => ThrownWeapon.Register(__instance.m_prefabs);
    }
}
