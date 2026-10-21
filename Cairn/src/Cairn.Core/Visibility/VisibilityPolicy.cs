// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: when a removal toggle takes effect (user decisions 2026-10-07 Q1 and 2026-10-08 plain toggles).
using ExtraPotions.Cairn.Config;

namespace ExtraPotions.Cairn.Visibility
{
    public enum TargetAction { None, Deactivate, Restore }

    public static class VisibilityPolicy
    {
        /// <summary>Progression-gate effects held on servers without Cairn (Q1): the Wisplight exists to clear Mistlands mist.</summary>
        public static bool IsGateLike(VisibilityEffect effect) =>
            effect == VisibilityEffect.MistlandsMist || effect == VisibilityEffect.ParticleMist || effect == VisibilityEffect.CaveMist;

        /// <summary>Whether Cairn removes the effect now: the player asked, the server allows it, and no Q1 hold applies.</summary>
        public static bool Removes(VisibilityEffect effect, bool remove, bool serverAllows, bool held) =>
            remove && serverAllows && !(held && IsGateLike(effect));

        /// <summary>Why a requested removal is not applied (C03); None when nothing was requested or it applies.</summary>
        public static BlockReason Reason(VisibilityEffect effect, bool remove, bool serverAllows, SessionFacts facts)
        {
            if (!remove) return BlockReason.None;
            if (IsGateLike(effect) && GatePolicy.Holds(facts)) return GatePolicy.HeldReason(facts);
            if (!serverAllows) return BlockReason.SetByServer;
            return BlockReason.None;
        }

        /// <summary>
        /// A scene object: deactivate it while removed; when the removal ends, reactivate only an object Cairn deactivated,
        /// so the game's own inactive objects stay inactive.
        /// </summary>
        public static TargetAction SceneAction(bool removes, bool active, bool ownedByCairn)
        {
            if (removes) return active ? TargetAction.Deactivate : TargetAction.None;
            return !active && ownedByCairn ? TargetAction.Restore : TargetAction.None;
        }

        /// <summary>Smoke alpha after Cairn (BetterFog hook): 0 when removed, else scaled by the graded strength.</summary>
        public static float SmokeAlpha(float alpha, bool removes, float strength = 1f) => removes ? 0f : alpha * strength;

        /// <summary>
        /// The graded fraction of the game's effect in force: the player's strength (clamped to 0..1, unreadable is 1) when
        /// every gating effect may be removed (server allowance and the Q1 hold for gate-like effects), else 1.
        /// </summary>
        public static float Strength(float strength, bool serverAllows, bool held, bool gateLike)
        {
            if (!serverAllows || (held && gateLike) || float.IsNaN(strength)) return 1f;
            return strength < 0f ? 0f : strength > 1f ? 1f : strength;
        }
    }
}
