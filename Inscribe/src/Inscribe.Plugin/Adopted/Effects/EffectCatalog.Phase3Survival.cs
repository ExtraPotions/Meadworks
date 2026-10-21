// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using static ExtraPotions.Inscribe.Adopted.Effects.EffectParamKind;
using static ExtraPotions.Inscribe.Adopted.Effects.EffectPolarity;
using static ExtraPotions.Inscribe.Adopted.Effects.EffectRoute;
using static ExtraPotions.Inscribe.Adopted.Effects.EffectScope;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The Phase 3 survival effects (features/effects-phase3.md, "Survival"): meads, food, rest, adrenaline, blocking,
    /// bosses, burning and the air jump. Every one is player-global and runs on the local player's own client, where
    /// the game runs that player's consumption, food, status effects, blocks and movement: nothing is sent.
    /// </summary>
    internal static partial class EffectCatalog
    {
        static partial void RegisterPhase3Survival()
        {
            RegisterSurvivalSustain();
            RegisterSurvivalCombat();
        }

        private static void RegisterSurvivalSustain()
        {
            Add("mead_duration", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, M, "prefix SE_Stats.Setup while drinking: a non-restoring mead's status lasts longer");
            Add("mead_potency", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 50, M, "prefix SE_Stats.Setup while drinking: a restoring mead restores more");
            Add("mead_save", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 50, M, "prefix Inventory.RemoveOneItem while drinking: chance the mead is kept");
            Add("food_values", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 30, M, "postfix Player.GetTotalFoodValue: the foods' part is larger");
            Add("food_regen", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 50, M, "postfix SEMan.ModifyHealthRegen: the food health tick is larger");
            Add("rested_duration", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, M, "prefix SE_Rested.Setup/ResetTime: longer Rested time");
        }

        private static void RegisterSurvivalCombat()
        {
            Add("trinket_duration", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, M, "postfix SEMan.AddStatusEffect inside Player.AddAdrenaline: a trinket's full-adrenaline effect lasts longer");
            Add("adrenaline_gain", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, M, "prefix Player.AddAdrenaline inside Humanoid.BlockAttack");
            Add("block_restore", Hook, PlayerGlobal, ValueTypes.Flat, Resource, Raise, null, M, "postfix Humanoid.BlockAttack: a held block restores X of the resource");
            Add("boss_damage_taken", Hook, PlayerGlobal, ValueTypes.Percent, None, Lower, 40, M, "prefix Character.RPC_Damage on the local player: hits from bosses");
            Add("burning_taken", Hook, PlayerGlobal, ValueTypes.Percent, None, Lower, 75, M, "prefix Character.ApplyDamage on the local player: burning ticks and lava");
            Add("burning_decay", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 75, M, "postfix StatusEffect.UpdateStatusEffect: Burning on you ends sooner");
            Add("air_jump", Hook, PlayerGlobal, ValueTypes.Flag, None, Raise, null, M, "prefix Character.Jump: one jump in the air, given back on landing");
        }
    }
}
