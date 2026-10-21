// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the vanilla members Inscribe will patch (design section 8), checked against assembly_valheim
// 1.0.17 metadata on 2026-10-07 (SHA256 prefix 25a0a107dce4d834). Member names compile against the publicized game
// assembly (nameof); parameter lists are resolved at startup. No patch is applied yet: the native units (R05b to R14b)
// add them one by one, each with its scenario.
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>One planned Harmony target: declaring type, method, exact parameter types, and the unit that patches it.</summary>
    public sealed class HarmonyPoint
    {
        public HarmonyPoint(Type type, string method, Type[] parameters, string unit, string purpose)
        {
            Type = type;
            Method = method;
            Parameters = parameters;
            Unit = unit;
            Purpose = purpose;
        }

        public Type Type { get; }
        public string Method { get; }
        public Type[] Parameters { get; }
        public string Unit { get; }
        public string Purpose { get; }

        public override string ToString() => Type.Name + "." + Method + "(" + string.Join(", ", Parameters.Select(p => p.Name)) + ")";
    }

    public static class HarmonyPoints
    {
        private static readonly Type ItemData = typeof(ItemDrop.ItemData);

        public static IReadOnlyList<HarmonyPoint> All { get; } = new[]
        {
            new HarmonyPoint(ItemData, nameof(ItemDrop.ItemData.GetArmor), new[] { typeof(int), typeof(float) }, "R06/R07b", "stat pipeline: armour"),
            new HarmonyPoint(ItemData, nameof(ItemDrop.ItemData.GetBaseBlockPower), new[] { typeof(int) }, "R06/R07b", "stat pipeline: block power"),
            new HarmonyPoint(ItemData, nameof(ItemDrop.ItemData.GetDeflectionForce), new[] { typeof(int) }, "R06/R07b", "stat pipeline: deflection"),
            new HarmonyPoint(ItemData, nameof(ItemDrop.ItemData.GetMaxDurability), new[] { typeof(int) }, "R06/R07b", "stat pipeline: durability"),
            new HarmonyPoint(ItemData, nameof(ItemDrop.ItemData.GetWeight), new[] { typeof(int) }, "R06/R07b", "stat pipeline: weight"),
            new HarmonyPoint(ItemData, nameof(ItemDrop.ItemData.GetNonStackedWeight), Type.EmptyTypes, "R06/R07b", "stat pipeline: weight"),
            new HarmonyPoint(ItemData, nameof(ItemDrop.ItemData.GetDamage), new[] { typeof(int), typeof(float) }, "R06/R07b", "stat pipeline: weapon damage"),
            new HarmonyPoint(ItemData, nameof(ItemDrop.ItemData.GetTooltip), new[] { ItemData, typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) }, "R10", "tooltip block"),
            new HarmonyPoint(typeof(ItemStand), nameof(ItemStand.GetHoverText), Type.EmptyTypes, "R10", "coloured names on stands"),
            new HarmonyPoint(typeof(ItemDrop), nameof(ItemDrop.GetHoverText), Type.EmptyTypes, "R10", "colored names on the ground"),
            new HarmonyPoint(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui), new[] { typeof(Player), ItemData }, "R10", "item badges (quality level)"),
            new HarmonyPoint(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem), new[] { typeof(InventoryGrid), ItemData, typeof(Vector2i), typeof(InventoryGrid.Modifier) }, "R05b", "rune gesture"),
            new HarmonyPoint(typeof(InventoryGui), nameof(InventoryGui.DoCrafting), new[] { typeof(Player) }, "R04b", "upgrade carry-over of both key families"),
            new HarmonyPoint(typeof(CharacterDrop), nameof(CharacterDrop.OnDeath), Type.EmptyTypes, "R09", "shared death hook on the creature's owner"),
            new HarmonyPoint(typeof(ItemDrop), nameof(ItemDrop.DropItem), new[] { ItemData, typeof(int), typeof(Vector3), typeof(Quaternion) }, "R09", "drops beside vanilla loot"),
            new HarmonyPoint(typeof(Container), nameof(Container.AddDefaultItems), Type.EmptyTypes, "R09", "world chest fill once"),
            new HarmonyPoint(typeof(ObjectDB), nameof(ObjectDB.Awake), Type.EmptyTypes, "R05b/R08", "rune and old scroll prefabs"),
            new HarmonyPoint(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB), new[] { typeof(ObjectDB) }, "R05b/R08", "rune and old scroll prefabs"),
            new HarmonyPoint(typeof(ZNetScene), nameof(ZNetScene.Awake), Type.EmptyTypes, "R05b/R08", "networked prefab registration"),
            new HarmonyPoint(typeof(Player), nameof(Player.OnSpawned), new[] { typeof(bool) }, "0.2.0", "enhancement conversion: local inventory"),
            new HarmonyPoint(typeof(Humanoid), nameof(Humanoid.Pickup), new[] { typeof(GameObject), typeof(bool), typeof(bool) }, "0.2.0", "enhancement conversion: after a pickup"),
            new HarmonyPoint(typeof(InventoryGui), nameof(InventoryGui.Show), new[] { typeof(Container), typeof(int) }, "0.2.0", "enhancement conversion: opened container"),
        };

        /// <summary>Resolves every planned target with its exact parameters; logs the count and names any that are missing.</summary>
        public static int Verify(MeadworksLog log)
        {
            var missing = All.Where(p => AccessTools.Method(p.Type, p.Method, p.Parameters) == null).ToList();
            foreach (var point in missing) log.Warning("Inscribe planned patch target missing in this game build: " + point + " (" + point.Unit + ").");
            log.Verbose("Inscribe patch targets resolved: " + (All.Count - missing.Count) + " of " + All.Count + ".");
            return missing.Count;
        }
    }
}
