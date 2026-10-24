// SPDX-License-Identifier: GPL-3.0-only
// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0
// 47e45a6e8c3b389a51e42fc7387d1a4fb90af473
// Changed by expDARE, 2026: exact Reaping overlay ownership around original stored baselines and apply helpers.
using System.Runtime.CompilerServices;
using UnityEngine;
using ExtraPotions.Wildblood.Adopted.Runtime;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.AdoptedCm;

internal static partial class CreatureModifierManager
{
    internal static void ForgetCreatureCaches(Character character)
    {
        if (object.ReferenceEquals(character, null)) return;
        int id = character.GetInstanceID();
        if (ModifierHotPathStates.TryGetValue(id, out var hot)
            && object.ReferenceEquals(hot.Character, character))
            ModifierHotPathStates.Remove(id);
        if (ModifierHudRefreshStates.TryGetValue(id, out var hud)
            && object.ReferenceEquals(hud.Character, character))
            ModifierHudRefreshStates.Remove(id);
    }

    private sealed class ReapingOverlay
    {
        internal readonly long Generation = CruciblePlugin.Session.Generation;
        internal float HealthBase, HealthLast;
        internal Vector3 ScaleBase, ScaleLast;
        internal bool HealthOwned, HealthSuspended, HealthRestored, HealthBlocked;
        internal bool ScaleOwned, ScaleSuspended, ScaleRestored, ScaleBlocked, ScaleHashOwned;
    }
    private static readonly ConditionalWeakTable<Character, ReapingOverlay> ReapingOverlays = new();
    // Owner-written record of this layer's last exact health write; s_maxHealth persists it, a reload must unwind it.
    private const string ReapingLayerBaseHealthKey = "wildblood.cm.ReapingLayerBaseHealth";
    private const string ReapingLayerMaxHealthKey = "wildblood.cm.ReapingLayerMaxHealth";
    private static bool LifecycleFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool LifecycleFinite(Vector3 value) => LifecycleFinite(value.x) && LifecycleFinite(value.y) && LifecycleFinite(value.z);
    private static bool ReapingOwner(Character character)
    {
        if (character.m_nview == null || !character.m_nview.IsValid() || !character.m_nview.IsOwner()) return false;
        var controller = character.GetComponent<EliteController>();
        // Cleanup may run with modules/Ready disabled, but never owns a previous session's native state.
        return controller == null || controller.OwnsCurrentView();
    }

    private static ReapingOverlay ReapingOverlayFor(Character character, ZDO zdo)
    {
        return ReapingOverlays.GetValue(character, actor =>
        {
            var state = new ReapingOverlay();
            // Recognize persisted imported source overlays only when live values exactly corroborate stored baselines.
            // No native level/stat snapshot or private/canonical bonus field is changed by this lifecycle layer.
            if (!HasModifier(GetStoredModifierMask(zdo), ModifierMask.Reaping)) return state;
            float baseline = zdo.GetFloat(ReapingBaseMaxHealthKey, 0f);
            float bonus = Mathf.Max(0f, zdo.GetFloat(ReapingBonusHealthKey, 0f));
            float cap = Mathf.Max(0f, zdo.GetFloat(ReapingMaxHealthCapKey, ReapingDefaultMaxHealthCap));
            float target = baseline + Mathf.Min(bonus, baseline * cap);
            if (ReapingOwner(actor) && baseline > 0f && bonus > 0f && target > baseline &&
                LifecycleFinite(baseline) && LifecycleFinite(target) && actor.GetMaxHealth() == target)
            {
                state.HealthBase = baseline; state.HealthLast = target; state.HealthOwned = true;
            }
            else if (ReapingOwner(actor) && baseline > 0f && bonus > 0f && target > baseline && LifecycleFinite(target))
            {
                // A reloaded maximum still carrying this layer's own exact write, re-derived from the current saved bonus.
                float layerBase = zdo.GetFloat(ReapingLayerBaseHealthKey, 0f);
                float layered = zdo.GetFloat(ReapingLayerMaxHealthKey, 0f);
                if (layerBase > 0f && layered > layerBase && LifecycleFinite(layerBase) && LifecycleFinite(layered) &&
                    layerBase + (target - baseline) == layered && actor.GetMaxHealth() == layered)
                {
                    state.HealthBase = layerBase; state.HealthLast = layered; state.HealthOwned = true;
                }
            }
            Vector3 baseScale = zdo.GetVec3(ReapingBaseScaleKey, Vector3.zero);
            float scaleBonus = Mathf.Max(0f, zdo.GetFloat(ReapingScaleBonusKey, 0f));
            Vector3 scale = baseScale * (1f + scaleBonus);
            if (scaleBonus > 0f && !baseScale.Equals(Vector3.zero) && LifecycleFinite(baseScale) && LifecycleFinite(scale) &&
                !CreatureLevelManager.IsDungeonCreature(actor) && actor.transform.localScale.Equals(scale))
            {
                state.ScaleBase = baseScale; state.ScaleLast = scale; state.ScaleOwned = true;
                state.ScaleHashOwned = ReapingOwner(actor) && zdo.GetVec3(ZDOVars.s_scaleHash, Vector3.zero).Equals(scale);
            }
            return state;
        });
    }

    private static bool CanApplyReapingHealth(Character character, ZDO zdo)
    {
        if (character == null || zdo == null || !ReapingOwner(character) || !CreatureLevelManager.AllowsModifierEffects(character)) return false;
        var controller = character.GetComponent<EliteController>();
        if (controller != null && !controller.Ready) return false;
        var state = ReapingOverlayFor(character, zdo);
        if (state.Generation != CruciblePlugin.Session.Generation || state.HealthSuspended || state.HealthBlocked) return false;
        if (state.HealthOwned && character.GetMaxHealth() != state.HealthLast)
        {
            state.HealthOwned = false; state.HealthBlocked = true;
            return false;
        }
        if (!state.HealthOwned && controller != null && controller.ReapingHealthBaselineOwned)
            state.HealthBase = character.GetMaxHealth();
        return true;
    }

    private static bool CanApplyReapingScale(Character character, ZDO zdo)
    {
        if (character == null || zdo == null || !CreatureLevelManager.AllowsModifierEffects(character)) return false;
        var controller = character.GetComponent<EliteController>();
        if (controller != null && (!controller.Ready || !controller.ReapingScaleReady)) return false;
        var state = ReapingOverlayFor(character, zdo);
        if (state.Generation != CruciblePlugin.Session.Generation || state.ScaleSuspended || state.ScaleBlocked) return false;
        if (state.ScaleOwned && (!character.transform.localScale.Equals(state.ScaleLast) ||
            (state.ScaleHashOwned && ReapingOwner(character) && !zdo.GetVec3(ZDOVars.s_scaleHash, Vector3.zero).Equals(state.ScaleLast))))
        {
            state.ScaleOwned = false; state.ScaleBlocked = true;
            return false;
        }
        if (!state.ScaleOwned && controller != null && controller.ReapingScaleBaselineOwned)
            state.ScaleBase = character.transform.localScale;
        return true;
    }

    // Preserve source saved gain/cap algebra; only attach that same saved bonus to a proven current ECR runtime base.
    private static float ReapingHealthTarget(Character character, ZDO zdo, float sourceTarget)
    {
        var state = ReapingOverlayFor(character, zdo);
        float storedBase = zdo.GetFloat(ReapingBaseMaxHealthKey, 0f);
        return state.HealthBase > 0f ? state.HealthBase + (sourceTarget - storedBase) : sourceTarget;
    }

    private static Vector3 ReapingScaleTarget(Character character, ZDO zdo, Vector3 sourceTarget, float scaleBonus)
    {
        var state = ReapingOverlayFor(character, zdo);
        return !state.ScaleBase.Equals(Vector3.zero) ? state.ScaleBase * (1f + scaleBonus) : sourceTarget;
    }

    private static void RememberReapingHealthWrite(Character character, ZDO zdo, float target)
    {
        var state = ReapingOverlayFor(character, zdo);
        float baseline = state.HealthBase > 0f ? state.HealthBase : zdo.GetFloat(ReapingBaseMaxHealthKey, 0f);
        if (!(baseline > 0f) || !LifecycleFinite(baseline) || !LifecycleFinite(target) || character.GetMaxHealth() != target)
        {
            state.HealthOwned = false; state.HealthBlocked = true;
            return;
        }
        state.HealthBase = baseline; state.HealthLast = target; state.HealthOwned = target != baseline;
        if (state.HealthOwned && ReapingOwner(character))
        {
            zdo.Set(ReapingLayerBaseHealthKey, baseline);
            zdo.Set(ReapingLayerMaxHealthKey, target);
        }
    }

    private static void RememberReapingScaleWrite(Character character, ZDO zdo, Vector3 target)
    {
        var state = ReapingOverlayFor(character, zdo);
        Vector3 baseline = !state.ScaleBase.Equals(Vector3.zero) ? state.ScaleBase : zdo.GetVec3(ReapingBaseScaleKey, Vector3.zero);
        if (baseline.Equals(Vector3.zero) || !LifecycleFinite(baseline) || !LifecycleFinite(target) || !character.transform.localScale.Equals(target))
        {
            state.ScaleOwned = false; state.ScaleBlocked = true;
            return;
        }
        state.ScaleBase = baseline; state.ScaleLast = target; state.ScaleOwned = !target.Equals(baseline);
        state.ScaleHashOwned = ReapingOwner(character) && zdo.GetVec3(ZDOVars.s_scaleHash, Vector3.zero).Equals(target);
    }

    // True only when this call took a requested hold; a nested caller must leave the outer holder's resume alone.
    internal static bool SuspendReapingRuntime(Character character, bool health = true, bool scale = true)
    {
        if (character == null || !TryGetZdo(character, out ZDO zdo)) return false;
        var state = ReapingOverlayFor(character, zdo);
        bool held = false;
        if (health && !state.HealthSuspended)
        {
            held = true;
            state.HealthSuspended = true; state.HealthRestored = false;
            if (state.HealthOwned && state.Generation == CruciblePlugin.Session.Generation && ReapingOwner(character))
            {
                if (character.GetMaxHealth() == state.HealthLast)
                {
                    float fraction = Mathf.Clamp01(character.GetHealth() / state.HealthLast);
                    character.SetMaxHealth(state.HealthBase);
                    character.SetHealth(state.HealthBase * fraction);
                    zdo.RemoveFloat(ReapingLayerBaseHealthKey);
                    zdo.RemoveFloat(ReapingLayerMaxHealthKey);
                    state.HealthRestored = true;
                }
                else state.HealthBlocked = true;
                state.HealthOwned = false;
            }
        }
        if (scale && !state.ScaleSuspended)
        {
            held = true;
            state.ScaleSuspended = true; state.ScaleRestored = false;
            if (state.ScaleOwned)
            {
                if (character.transform.localScale.Equals(state.ScaleLast) &&
                    (!state.ScaleHashOwned || !ReapingOwner(character) || zdo.GetVec3(ZDOVars.s_scaleHash, Vector3.zero).Equals(state.ScaleLast)))
                {
                    character.transform.localScale = state.ScaleBase;
                    if (state.ScaleHashOwned && state.Generation == CruciblePlugin.Session.Generation && ReapingOwner(character)) zdo.Set(ZDOVars.s_scaleHash, state.ScaleBase);
                    ScheduleReapingPhysicsSync();
                    state.ScaleRestored = true;
                }
                else state.ScaleBlocked = true;
                state.ScaleOwned = false;
            }
        }
        return held;
    }

    internal static void ResumeReapingRuntime(Character character, bool health = true, bool scale = true)
    {
        if (character == null || !TryGetZdo(character, out ZDO zdo) || !ReapingOverlays.TryGetValue(character, out var state)) return;
        if (health && state.HealthSuspended)
        {
            state.HealthSuspended = false;
            if (state.HealthRestored && character.GetMaxHealth() != state.HealthBase)
            {
                var controller = character.GetComponent<EliteController>();
                if (controller != null && controller.ReapingHealthBaselineOwned) state.HealthBase = character.GetMaxHealth();
                else state.HealthBlocked = true;
            }
            state.HealthRestored = false;
            if (CanApplyReapingHealth(character, zdo))
            {
                float before = character.GetMaxHealth();
                float fraction = before > 0f ? Mathf.Clamp01(character.GetHealth() / before) : 0f;
                ApplyStoredReapingHealth(character, zdo);
                float after = character.GetMaxHealth();
                if (state.HealthOwned && after == state.HealthLast) character.SetHealth(after * fraction);
            }
        }
        if (scale && state.ScaleSuspended)
        {
            state.ScaleSuspended = false;
            if (state.ScaleRestored && !character.transform.localScale.Equals(state.ScaleBase))
            {
                var controller = character.GetComponent<EliteController>();
                if (controller != null && controller.ReapingScaleBaselineOwned) state.ScaleBase = character.transform.localScale;
                else state.ScaleBlocked = true;
            }
            state.ScaleRestored = false;
            if (CanApplyReapingScale(character, zdo)) ApplyStoredReapingScale(character, zdo);
        }
    }
}
