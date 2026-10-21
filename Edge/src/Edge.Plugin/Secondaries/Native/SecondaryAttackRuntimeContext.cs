// Portions from SecondaryAttacks 1.2.18 (sighsorry1029), GPL-3.0, at f32966cad8303329b3490a40857205bd2a303b96.
// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackRuntimeContext.cs. Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespaces, canonical catalogs, Crucible settings and diagnostics;
// native source algorithms retained. Candidate stage, dispatch and prefab application are a separate unit.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Edge.Secondaries.Adopted;
using ExtraPotions.Edge.Config;

internal static class SecondaryAttackRuntimeContext
{
    private static readonly ConditionalWeakTable<Attack, ActiveSecondaryAttack> ActiveAttacks = new();
    private static readonly ConditionalWeakTable<Projectile, ProjectileAttackAttribution> ProjectileAttackAttributions = new();
    private static readonly List<ProjectileHitContext> ActiveProjectileHitContexts = new();
    private static int _generatedDamageDepth;

    internal static void ResetForEdge() { ActiveAttacks.Clear(); ProjectileAttackAttributions.Clear(); ActiveProjectileHitContexts.Clear(); _generatedDamageDepth = 0; }

    internal static bool IsGeneratedDamageActive => _generatedDamageDepth > 0;

    internal static GeneratedDamageScope BeginGeneratedDamage()
    {
        _generatedDamageDepth++;
        return new GeneratedDamageScope(active: true);
    }

    private static void EndGeneratedDamage()
    {
        if (_generatedDamageDepth > 0)
        {
            _generatedDamageDepth--;
        }
    }

    internal static void SetActiveAttack(Attack attack, ActiveSecondaryAttack activeAttack)
    {
        ActiveAttacks.Remove(attack);
        ActiveAttacks.Add(attack, activeAttack);
    }

    internal static bool TryGetActiveAttack(Attack attack, out ActiveSecondaryAttack? activeAttack)
    {
        return ActiveAttacks.TryGetValue(attack, out activeAttack);
    }

    internal static void SetProjectileAttackAttribution(Projectile projectile, ProjectileAttackAttribution attribution)
    {
        ProjectileAttackAttributions.Remove(projectile);
        ProjectileAttackAttributions.Add(projectile, attribution);
    }

    internal static bool TryGetProjectileAttackAttribution(Projectile projectile, out ProjectileAttackAttribution? attribution)
    {
        return ProjectileAttackAttributions.TryGetValue(projectile, out attribution);
    }

    internal static void PushProjectileHitContext(ProjectileHitContext context)
    {
        ActiveProjectileHitContexts.Add(context);
    }

    internal static void PopProjectileHitContext()
    {
        if (ActiveProjectileHitContexts.Count == 0)
        {
            return;
        }

        ActiveProjectileHitContexts.RemoveAt(ActiveProjectileHitContexts.Count - 1);
    }

    internal static bool TryPeekProjectileHitContext(out ProjectileHitContext context)
    {
        if (ActiveProjectileHitContexts.Count == 0)
        {
            context = default;
            return false;
        }

        context = ActiveProjectileHitContexts[ActiveProjectileHitContexts.Count - 1];
        return true;
    }

    internal struct GeneratedDamageScope : System.IDisposable
    {
        private bool _active;

        internal GeneratedDamageScope(bool active)
        {
            _active = active;
        }

        public void Dispose()
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            EndGeneratedDamage();
        }
    }
}

internal sealed class ActiveSecondaryAttack
{
    public ActiveSecondaryAttack(SecondaryAttackDefinition definition)
    {
        Definition = definition;
    }

    public SecondaryAttackDefinition Definition { get; }

    public bool Triggered { get; set; }

    public bool ProjectileTriggered { get; set; }

    public bool BurstTriggerHandled { get; set; }

    public bool BurstRuntimeStarted { get; set; }

    public bool BurstAmmoConserved { get; set; }

    public float NextHoldRepeatTime { get; set; }
}

internal sealed class ProjectileAttackAttribution
{
    public ProjectileAttackAttribution(
        string weaponPrefabName,
        bool secondaryAttack,
        SecondaryAttackDefinition? definition,
        bool disableCurrentAttackFallback)
    {
        WeaponPrefabName = weaponPrefabName;
        SecondaryAttack = secondaryAttack;
        Definition = definition;
        DisableCurrentAttackFallback = disableCurrentAttackFallback;
    }

    public string WeaponPrefabName { get; }

    public bool SecondaryAttack { get; }

    public SecondaryAttackDefinition? Definition { get; }

    public bool DisableCurrentAttackFallback { get; }
}

internal readonly struct ProjectileHitContext
{
    public ProjectileHitContext(
        Projectile projectile,
        ProjectileAttackAttribution? attribution)
    {
        Projectile = projectile;
        Attribution = attribution;
    }

    public Projectile Projectile { get; }

    public ProjectileAttackAttribution? Attribution { get; }
}
