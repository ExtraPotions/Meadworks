// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.18 (sighsorry1029), GPL-3.0, at f32966cad8303329b3490a40857205bd2a303b96.
// Changed by expDARE, 2026: Edge ownership, configuration and compatible-contract discovery.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Reflection;
using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Edge.Secondaries.Adopted;
using ExtraPotions.Edge.Integration;

// EpicLoot owns its proc chances, effect configuration and resource modifiers.
// Edge owns Burst scheduling and cross-stack ammo payment. Bridge at each actual shot.
internal static class EpicLootCompat
{
    internal const string PluginGuid = "randyknapp.mods.epicloot";
    private const string HarmonyId = "com.meadworks.edge.epicloot-burst";
    private static readonly AccessTools.FieldRef<Attack, Humanoid> Character =
        AccessTools.FieldRefAccess<Attack, Humanoid>("m_character");
    private static Api? _api;
    private static Attack? _manualCall;

    internal static void Initialize()
    {
        Dispose();
        if (!Chainloader.PluginInfos.TryGetValue(PluginGuid, out BepInEx.PluginInfo? plugin) ||
            plugin?.Instance == null)
        {
            return;
        }

        try
        {
            Api api = new(plugin.Instance.GetType().Assembly);
            api.ValidateRegistrations();
            Harmony harmony = new(HarmonyId);
            harmony.Patch(api.RollMethod, prefix: new HarmonyMethod(typeof(EpicLootCompat), nameof(RunAutomaticRoll)));
            harmony.Patch(api.PrepareMethod, prefix: new HarmonyMethod(typeof(EpicLootCompat), nameof(RunAutomaticPrepare)));
            _api = api;
            RuleDiagnostics.Info($"EpicLoot {plugin.Metadata.Version} Burst bridge enabled: per-shot multishot and ammo conservation.");
        }
        catch (Exception exception)
        {
            Dispose();
            RuleDiagnostics.Warning($"EpicLoot {plugin.Metadata.Version} Burst bridge unavailable: {exception.Message}");
        }
    }

    internal static void Dispose()
    {
        _api = null;
        _manualCall = null;
        new Harmony(HarmonyId).UnpatchSelf();
    }

    internal static bool HandlesAttack(Attack attack)
    {
        return _api != null && attack != null && EdgeModules.Active(EdgeModule.Secondaries) &&
               Character(attack) is Player player && player == Player.m_localPlayer &&
               !string.IsNullOrWhiteSpace(attack.GetWeapon()?.m_shared.m_ammoType) &&
               SecondaryAttackRuntimeContext.TryGetActiveAttack(attack, out ActiveSecondaryAttack? active) &&
               active?.Definition.Behavior is ProjectileSecondaryBehavior { Preset: SecondaryAttackPreset.Burst };
    }

    // HarmonyX runs later prefixes even when Edge skips the game method. Suppress
    // only these two EpicLoot callbacks, never its normal attacks or other effects.
    private static bool RunAutomaticRoll(Attack __0) => ReferenceEquals(_manualCall, __0) || !HandlesAttack(__0);

    private static bool RunAutomaticPrepare(Attack __0, ref HitData.DamageTypes? __1)
    {
        if (ReferenceEquals(_manualCall, __0) || !HandlesAttack(__0)) return true;
        __1 = null; // EpicLoot's original finalizer must not restore another shot's damage.
        return false;
    }

    internal static Shot? BeginShot(Attack attack) => HandlesAttack(attack) ? new Shot(_api!, attack) : null;

    internal static bool PreservesReload(Attack attack) => HandlesAttack(attack) &&
        SecondaryAttackRuntimeContext.TryGetActiveAttack(attack, out ActiveSecondaryAttack? active) &&
        active != null && active.BurstAmmoConserved;

    internal sealed class Shot : IDisposable
    {
        private readonly Api _api;
        private readonly Attack _attack;
        private readonly ItemDrop.ItemData.SharedData _shared;
        private readonly HitData.DamageTypes _damage;
        private readonly int _projectiles;
        private readonly float _accuracy;
        private readonly object? _pending;
        private readonly object? _triple;
        private readonly object? _count;
        private bool _disposed;

        internal Shot(Api api, Attack attack)
        {
            _api = api;
            _attack = attack;
            _shared = attack.GetWeapon().m_shared;
            _damage = _shared.m_damages;
            _projectiles = attack.m_projectiles;
            _accuracy = attack.m_projectileAccuracy;
            _pending = api.Pending.GetValue(null);
            _triple = api.Triple.GetValue(null);
            _count = api.Count.GetValue(null);
            Attack? previous = _manualCall;
            try
            {
                _manualCall = attack;
                api.Roll(attack);
                AmmoMultiplier = (bool)api.Triple.GetValue(null)!
                    ? Math.Max(1, (int)api.Count.GetValue(null)!) : 1;
                // Match UseAmmo's consumption of the ammo flags; keep pending
                // effect data for Prepare, isolated until this shot is disposed.
                api.Triple.SetValue(null, false);
                api.Count.SetValue(null, 0);
            }
            catch
            {
                Dispose();
                throw;
            }
            finally { _manualCall = previous; }
        }

        internal int AmmoMultiplier { get; }

        internal void PrepareProjectiles()
        {
            Attack? previous = _manualCall;
            try
            {
                _manualCall = _attack;
                HitData.DamageTypes? state = null;
                _api.Prepare(_attack, ref state);
            }
            finally { _manualCall = previous; }
        }

        internal void Complete(ItemDrop.ItemData? ammo, bool paidAmmo)
        {
            bool previous = (bool)_api.SkipReload.GetValue(null)!;
            try
            {
                _api.SkipReload.SetValue(null, false);
                if (paidAmmo && ammo != null)
                {
                    bool used = true;
                    _api.Conserve(_attack, ref used, ammo);
                }

                if (SecondaryAttackRuntimeContext.TryGetActiveAttack(_attack, out ActiveSecondaryAttack? active) && active != null)
                    active.BurstAmmoConserved = (bool)_api.SkipReload.GetValue(null)!;
            }
            finally
            {
                // Burst finalization, not the next Player.Update, owns this
                // reload. Keep unrelated EpicLoot pending reloads untouched.
                _api.SkipReload.SetValue(null, previous);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _shared.m_damages = _damage;
            _attack.m_projectiles = _projectiles;
            _attack.m_projectileAccuracy = _accuracy;
            _api.Pending.SetValue(null, _pending);
            _api.Triple.SetValue(null, _triple);
            _api.Count.SetValue(null, _count);
        }
    }

    internal sealed class Api
    {
        internal delegate void PrepareShot(Attack attack, ref HitData.DamageTypes? state);
        internal delegate void ConserveAmmo(Attack attack, ref bool result, ItemDrop.ItemData ammo);
        internal readonly MethodInfo RollMethod;
        internal readonly MethodInfo PrepareMethod;
        internal readonly MethodInfo RefundMethod, FinalizerMethod;
        internal readonly Action<Attack> Roll;
        internal readonly PrepareShot Prepare;
        internal readonly ConserveAmmo Conserve;
        internal readonly FieldInfo Pending, Triple, Count, SkipReload;

        internal Api(Assembly assembly)
        {
            Type multi = assembly.GetType("EpicLoot.MagicItemEffects.MultiShot", true)!;
            Type conserve = assembly.GetType("EpicLoot.Magic.MagicItemEffects.AmmoConservation", true)!;
            RollMethod = Callback(multi, "Attack_OnAttackTrigger_Prefix", typeof(Attack));
            PrepareMethod = Callback(multi, "Attack_FireProjectileBurst_Prefix", typeof(Attack), typeof(HitData.DamageTypes?).MakeByRefType());
            Roll = (Action<Attack>)Delegate.CreateDelegate(typeof(Action<Attack>), RollMethod);
            Prepare = (PrepareShot)Delegate.CreateDelegate(typeof(PrepareShot), PrepareMethod);
            Type refundType = conserve.GetNestedType("AmmoConservation_Attack_UseAmmo_Patch", BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingMemberException(conserve.FullName, "AmmoConservation_Attack_UseAmmo_Patch");
            RefundMethod = Callback(refundType, "Postfix", typeof(Attack), typeof(bool).MakeByRefType(), typeof(ItemDrop.ItemData));
            MethodInfo refund = RefundMethod;
            FinalizerMethod = Callback(multi, "Attack_FireProjectileBurst_Finalizer", typeof(Attack), typeof(HitData.DamageTypes?));
            Conserve = (ConserveAmmo)Delegate.CreateDelegate(typeof(ConserveAmmo), refund);
            Pending = RequireField(multi, "_pendingShot", null);
            if (!Pending.FieldType.IsEnum || !new[]{"None", "TripleBow", "DoubleMagic"}.All(name => Enum.IsDefined(Pending.FieldType, name)))
                throw new MissingFieldException("EpicLoot pending shot contract changed.");
            Triple = RequireField(multi, "IsTripleShotActive", typeof(bool));
            Count = RequireField(multi, "ShotProjectiles", typeof(int));
            SkipReload = RequireField(conserve, "skipReload", typeof(bool));
        }

        private static MethodInfo Callback(Type type, string name, params Type[] parameters)
        {
            var matches = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.Name == name && method.ReturnType == typeof(void) && !method.ContainsGenericParameters
                    && method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameters)).ToArray();
            if (matches.Length != 1) throw new MissingMethodException(type.FullName, name + " exact callback contract");
            return matches[0];
        }

        internal void ValidateRegistrations()
        {
            bool Has(string target, MethodInfo callback, string kind)
            {
                Type[] parameters = target == "UseAmmo" ? new[]{typeof(ItemDrop.ItemData).MakeByRefType()} : Type.EmptyTypes;
                var method = typeof(Attack).GetMethod(target, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, parameters, null);
                var info = method == null ? null : Harmony.GetPatchInfo(method);
                if (info == null) return false;
                var patches = kind == "prefix" ? info.Prefixes : kind == "postfix" ? info.Postfixes : info.Finalizers;
                return patches.Any(patch => patch.PatchMethod == callback);
            }
            if (!Has("OnAttackTrigger", RollMethod, "prefix") || !Has("FireProjectileBurst", PrepareMethod, "prefix")
                || !Has("FireProjectileBurst", FinalizerMethod, "finalizer") || !Has("UseAmmo", RefundMethod, "postfix"))
                throw new InvalidOperationException("EpicLoot callback Harmony registrations do not match the required attack contracts.");
        }

        private static FieldInfo RequireField(Type type, string name, Type? fieldType)
        {
            FieldInfo? field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || field.IsInitOnly || field.IsLiteral ||
                (fieldType != null && field.FieldType != fieldType))
                throw new MissingFieldException(type.FullName, name);
            return field;
        }
    }
}
