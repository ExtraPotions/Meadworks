// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Recoverable arrows idea seen in BetterArchery (ishid4); implemented from its public feature list
// and the game's own Attack/Projectile code only.
using System;
using System.Collections.Generic;
using ExtraPotions.Edge.Archery;
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Integration;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Bows;

/// <summary>
/// Recoverable arrows. The game takes an arrow from the local player's inventory (<c>Attack.UseAmmo</c>): one credit for
/// that arrow type. When one of the local player's arrows stops (the projectile's <c>m_didHit</c> turns true), it spends a
/// credit and rolls the server's <see cref="EdgeSettings.Ranged.ArrowRecoveryChance"/>; on success the stuck arrow is
/// replaced by a pickable arrow item. The roll runs on the shooter's machine, which owns and simulates the projectile.
/// Each roll is logged at Verbose (Meadworks log level Verbose or Debug for Edge).
/// </summary>
internal static class ArrowRecoverySystem
{
    private static readonly ArrowLedger Ledger = new ArrowLedger();
    private static readonly HashSet<int> Rolled = new HashSet<int>();
    private static bool reported;

    internal static bool Enabled => Plugin.Settings != null && EdgeModules.Active(EdgeModule.Archery);

    internal static void Reset()
    {
        Ledger.Clear();
        Rolled.Clear();
    }

    internal static string AmmoKey(ItemDrop.ItemData? ammo) => ammo?.m_dropPrefab != null ? ammo.m_dropPrefab.name : "";

    internal static void Paid(Attack attack, ItemDrop.ItemData? ammo)
    {
        if (ammo == null || attack.m_weapon == null || attack.m_weapon.m_shared.m_skillType != Skills.SkillType.Bows) return;
        var local = Player.m_localPlayer;
        if (local == null || !ReferenceEquals(attack.m_character, local) || !Enabled) return;
        var key = AmmoKey(ammo);
        Ledger.Fired(key);
        Plugin.Log?.Verbose("Arrow fired: " + key + ", recovery credits " + Ledger.Outstanding(key) + ".");
    }

    internal static void Stopped(Projectile projectile, Collider? collider, Vector3 hitPoint, bool water, Vector3 normal)
    {
        var local = Player.m_localPlayer;
        if (local == null || !ReferenceEquals(projectile.m_owner, local) || projectile.m_ammo == null || !Enabled) return;
        if (Rolled.Count > 4096) Rolled.Clear();
        if (!Rolled.Add(projectile.GetInstanceID())) return;
        var ammo = projectile.m_ammo;
        var key = AmmoKey(ammo);
        var hit = ArrowHits.Classify(water,
            collider != null && collider.GetComponentInParent<Character>() != null,
            collider != null && collider.GetComponent<Heightmap>() != null,
            hitPoint.y, LiquidLevel(hitPoint));
        var chance = Plugin.Settings!.Get(EdgeSettings.Ranged.ArrowRecoveryChance);
        var roll = UnityEngine.Random.value;
        var result = Ledger.Resolve(key, hit, ammo.m_shared.m_damages.m_fire > 0f, chance, roll);
        if (result == RecoveryResult.NotOwned) return;
        Plugin.Log?.Verbose("Arrow recovery: projectile " + projectile.GetInstanceID() + " " + key + " hit " + hit + ", roll "
            + roll.ToString("0.000") + " vs " + chance + "% -> " + result + ".");
        if (result != RecoveryResult.Recovered) return;
        var up = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
        var drop = ItemDrop.DropItem(ammo, 1, hitPoint + up * 0.15f, projectile.transform.rotation);
        Plugin.Log?.Verbose("Arrow recovered: " + key + " x1 at " + drop.transform.position.ToString("F1") + ".");
        var view = projectile.GetComponent<ZNetView>();
        if (view != null && view.IsValid() && view.IsOwner() && ZNetScene.instance != null)
            ZNetScene.instance.Destroy(projectile.gameObject);
    }

    /// <summary>
    /// The liquid surface over <paramref name="point"/>: the game's <c>Floating.GetLiquidLevel</c> at the point and at
    /// sea level above it (a point deep on the seabed may lie outside the zone's water volume), so an arrow that went
    /// through the sea surface counts as in the water wherever it stopped.
    /// </summary>
    private static float LiquidLevel(Vector3 point)
    {
        var level = Floating.GetLiquidLevel(point);
        var zones = ZoneSystem.instance;
        if (zones != null && point.y < zones.m_waterLevel)
            level = Mathf.Max(level, Floating.GetLiquidLevel(new Vector3(point.x, zones.m_waterLevel - 0.5f, point.z)));
        return level;
    }

    internal static void Guard(string step, Action action)
    {
        try { action(); }
        catch (Exception error)
        {
            if (reported) return;
            reported = true;
            Plugin.Log?.Warning("Arrow recovery (" + step + ") failed; arrows behave as the game does. " + error.GetType().Name + ": " + error.Message);
        }
    }
}

[HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
internal static class ArrowRecoveryOnHitPatch
{
    private static void Prefix(Projectile __instance, out bool __state) => __state = __instance.m_didHit;

    private static void Postfix(Projectile __instance, Collider collider, Vector3 hitPoint, bool water, Vector3 normal, bool __state)
    {
        if (__state || !__instance.m_didHit) return;
        ArrowRecoverySystem.Guard("hit", () => ArrowRecoverySystem.Stopped(__instance, collider, hitPoint, water, normal));
    }
}
