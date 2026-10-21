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
    /// The Phase 3 gathering and crafting effects (features/effects-phase3.md, "Gathering and crafting"). The tool
    /// hits, fishing, crafting, trading and building run on the acting player's own client; Butcher's Cut and Sea Ward
    /// are applied by the owner of the dying animal or of the ship, from the player's published value (PlayerStats).
    /// </summary>
    internal static partial class EffectCatalog
    {
        static partial void RegisterPhase3Gathering()
        {
            RegisterGatheringTools();
            RegisterGatheringFishing();
            RegisterGatheringCrafting();
        }

        private static void RegisterGatheringTools()
        {
            Add("chop_damage", Hook, ItemLocal, ValueTypes.Percent, None, Raise, 100, E, "prefix TreeBase/TreeLog/Destructible.Damage on the attacker: this axe's chop part");
            Add("pickaxe_damage", Hook, ItemLocal, ValueTypes.Percent, None, Raise, 100, E, "prefix MineRock/MineRock5/Destructible.Damage on the attacker: this pickaxe's part");
            Add("butcher_yield", Hook, PlayerGlobal, ValueTypes.Flat, None, Raise, null, M, "postfix CharacterDrop.GenerateDropList on the animal's owner (player ZDO value)");
            Add("ship_damage_taken", Hook, PlayerGlobal, ValueTypes.Percent, None, Lower, 75, M, "prefix WearNTear.RPC_Damage on the ship's owner, for its helmsman (player ZDO value)");
        }

        private static void RegisterGatheringFishing()
        {
            Add("bait_save", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 75, M, "postfix FishingFloat.SetCatch: the bait a hooked fish takes comes back");
            Add("fish_size", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, M, "prefix FishingFloat.Catch: the fish's level rolled again at better odds");
            Add("reel_stamina", Hook, PlayerGlobal, ValueTypes.Percent, None, Lower, 60, M, "prefix Player.UseStamina inside FishingFloat.FixedUpdate");
        }

        private static void RegisterGatheringCrafting()
        {
            Add("craft_save", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 30, M, "InventoryGui.DoCrafting: a craft's payment given back");
            Add("craft_extra", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 30, M, "InventoryGui.DoCrafting: one more of the recipe's output");
            Add("trader_discount", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 30, M, "StoreGui price window: lower trader prices, shown and paid");
            Add("free_build", Hook, ItemLocal, ValueTypes.Flag, None, Raise, null, M, "prefix Player.HaveRequirements(Piece): no station needed with this tool");
        }
    }
}
