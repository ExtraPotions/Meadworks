// Inscribe - ExtraPotions Meadworks gear inscriptions. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Changed by expDARE, 2026: the rune check pipeline and the five verbs over portable facts and an IInscriptionSource
// (the inscription roller of R04b plugs in there), message ids returned instead of shown, and the confirm gate as a
// pure decision (design B4: a pad falls back from HoldShift to the dialog); a promote rune at Epic or Legendary refuses
// with its own message (2026-10-19: Legendary is boss and raid only).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCrafting 0.7.0 (Stones/StonePipeline.cs, StoneVerbs.cs,
// AddingVerbs.cs, RemovingVerbs.cs, RerollVerb.cs, CorruptVerb.cs, StoneResult.cs, ConfirmGate.cs),
// geraldjglasgow/ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Inscribe.Config;
using ExtraPotions.Inscribe.Items;
using ExtraPotions.Inscribe.Rarity;

namespace ExtraPotions.Inscribe.Runes
{
    /// <summary>
    /// Draws inscriptions for a rune (design A6). Every method is pure and all or nothing: it returns the inscriptions to
    /// add or the new list, or null when the pool cannot supply them; it never writes the item.
    /// </summary>
    public interface IInscriptionSource
    {
        /// <summary>Exactly <paramref name="count"/> new inscriptions for <paramref name="state"/> at <paramref name="rarity"/>, the limits widened by <paramref name="overflow"/>.</summary>
        IReadOnlyList<Inscription>? Add(ItemState state, RarityDef rarity, int count, int overflow);

        /// <summary>A fresh list at the rarity: count drawn in its range, every old inscription (dormant ones too) replaced.</summary>
        IReadOnlyList<Inscription>? Fresh(ItemState state, RarityDef rarity);

        /// <summary>The Serpent's chaotic list: any tier, ignoring item level and floors; null only when nothing fits.</summary>
        IReadOnlyList<Inscription>? Chaotic(ItemState state, RarityDef rarity);
    }

    /// <summary>The facts of one rune click on the client that holds the item (design B2, B3).</summary>
    public sealed class RuneJob
    {
        public RuneJob(RuneDef? rune, ItemState state)
        {
            Rune = rune;
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public RuneDef? Rune { get; }
        public ItemState State { get; set; }
        public bool TargetInOwnInventory { get; set; }

        /// <summary>The rune stack is in the player's own inventory or the open chest.</summary>
        public bool RuneWithinReach { get; set; }

        public bool IsMagicBase { get; set; }
        public bool IsEquipped { get; set; }

        /// <summary>The server's <c>2 - Runes / Modify Equipped Items</c>.</summary>
        public bool ModifyEquippedItems { get; set; } = true;

        public int RuneStack { get; set; }
    }

    /// <summary>
    /// What the pipeline decided: a refusal (message id <c>rune_msg_&lt;id&gt;</c>, words for its placeholders), or the
    /// dry-run state that commits unchanged with its feedback message. A refusal never changed anything.
    /// </summary>
    public sealed class RuneResult
    {
        private RuneResult(string? refusal, string? feedback, ItemState? state, int cost, string[] words)
        {
            Refusal = refusal;
            Feedback = feedback;
            State = state;
            Cost = cost;
            Words = words;
        }

        public string? Refusal { get; }
        public string? Feedback { get; }
        public ItemState? State { get; }

        /// <summary>Runes the commit takes from the held stack.</summary>
        public int Cost { get; }

        /// <summary>Ids for the message placeholders (rune id, rarity id, inscription id); the plugin localises them.</summary>
        public IReadOnlyList<string> Words { get; }

        public bool Refused => Refusal != null;

        public static RuneResult Refuse(string id, params string[] words) => new RuneResult(id, null, null, 0, words);

        public static RuneResult Success(ItemState state, int cost, string feedback, params string[] words) => new RuneResult(null, feedback, state, cost, words);
    }

    /// <summary>
    /// Design B3: the checks in order, the first failure refuses; then the verb's own precondition and its dry run on a
    /// copy. Steps: 1 target in the own inventory, 2 rune within reach, 3 magic base, 4 not a newer format, 5 live
    /// enabled rune, 6 not sealed, 7 equipped rule, 8 known rarity, 9 rarity in <c>applies_to</c>, 10 enough runes,
    /// 11 verb precondition, 12 dry run; 13 the confirm gate is <see cref="RuneConfirm"/>.
    /// </summary>
    public static class RunePipeline
    {
        public static RuneResult Evaluate(RuneJob job, RarityLadder ladder, IInscriptionSource source, IDice dice)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            if (ladder == null) throw new ArgumentNullException(nameof(ladder));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (dice == null) throw new ArgumentNullException(nameof(dice));

            if (!job.TargetInOwnInventory) return RuneResult.Refuse("not_own_inventory");
            if (!job.RuneWithinReach) return RuneResult.Refuse("stone_out_of_reach");
            if (!job.IsMagicBase) return RuneResult.Refuse("not_magic_base");
            if (job.State.IsNewerFormat) return RuneResult.Refuse("newer_format");
            var rune = job.Rune;
            if (rune == null || !rune.Enabled) return RuneResult.Refuse("stone_disabled", rune?.Id ?? "");
            if (job.State.IsSealed) return RuneResult.Refuse("sealed");
            if (job.IsEquipped && !job.ModifyEquippedItems) return RuneResult.Refuse("equipped");
            var rarity = ladder.Of(job.State);
            if (rarity == null) return RuneResult.Refuse("unknown_rarity");
            if (rune.Verb == RuneVerb.Promote && AtTop(ladder, rarity)) return RuneResult.Refuse("ascension_top", rarity.Id);
            if (!rune.AppliesToRarity(rarity.Id)) return RuneResult.Refuse("wrong_rarity", rune.Id, rarity.Id);
            var cost = rune.CostFor(rarity.Id);
            if (job.RuneStack < cost) return RuneResult.Refuse("not_enough_stones", cost.ToString(System.Globalization.CultureInfo.InvariantCulture), rune.Id);
            return Verb(job, rune, rarity, cost, ladder, source, dice);
        }

        private static RuneResult Verb(RuneJob job, RuneDef rune, RarityDef rarity, int cost, RarityLadder ladder, IInscriptionSource source, IDice dice)
        {
            var state = job.State;
            switch (rune.Verb)
            {
                case RuneVerb.Promote:
                {
                    var next = ladder.Next(rarity);
                    if (next == null || next.SourceOnly) return RuneResult.Refuse("ascension_top", rarity.Id);
                    var promoted = state.WithRarity(next.Id);
                    var add = ladder.PromotionAdds(state.Count, next);
                    var added = add == 0 ? Array.Empty<Inscription>() : source.Add(promoted, next, add, 0);
                    return added == null ? RuneResult.Refuse("no_eligible_affix") : RuneResult.Success(promoted.Adding(added), cost, "promoted", next.Id);
                }
                case RuneVerb.Add:
                {
                    if (state.Count >= rarity.MaxInscriptions) return RuneResult.Refuse("affixes_full");
                    var added = source.Add(state, rarity, 1, 0);
                    return added == null ? RuneResult.Refuse("no_eligible_affix") : RuneResult.Success(state.Adding(added), cost, "affix_added", added[0].Id);
                }
                case RuneVerb.Reroll:
                {
                    if (rarity.IsBase) return RuneResult.Refuse("wrong_rarity", rune.Id, rarity.Id);
                    var fresh = source.Fresh(state, rarity);
                    return fresh == null ? RuneResult.Refuse("no_eligible_affix") : RuneResult.Success(state.WithInscriptions(fresh), cost, "rerolled");
                }
                case RuneVerb.Strip:
                    return RuneResult.Success(new ItemState(null, null, null, state.IsNewerFormat), cost, "stripped");
                case RuneVerb.Corrupt:
                    return Corrupt(state, rune, rarity, cost, source, dice);
                default:
                    return RuneResult.Refuse("stone_disabled", rune.Id);
            }
        }

        /// <summary>No rune raises this rarity: it is the top, or the next is source-only (Legendary: bosses and raids).</summary>
        public static bool AtTop(RarityLadder ladder, RarityDef rarity)
        {
            if (rarity.IsBase) return false;
            var next = ladder.Next(rarity);
            return next == null || next.SourceOnly;
        }

        // Draw one outcome by weight and carry it out on the copy; an outcome that cannot be carried out seals only.
        private static RuneResult Corrupt(ItemState state, RuneDef rune, RarityDef rarity, int cost, IInscriptionSource source, IDice dice)
        {
            var index = Weighted.Pick(rune.Outcomes.Select(o => o.Weight).ToList(), dice);
            var outcome = index < 0 ? SerpentOutcome.SealOnly : rune.Outcomes[index].Outcome;
            if (outcome == SerpentOutcome.AddInscription && !rarity.IsBase && state.Count < rarity.MaxInscriptions + rune.Overflow)
            {
                var added = source.Add(state, rarity, 1, rune.Overflow);
                if (added != null) return RuneResult.Success(state.Adding(added).Sealed(ItemKeys.EcSealedSerpent), cost, "corrupt_add", added[0].Id);
            }
            if (outcome == SerpentOutcome.ChaoticReroll)
            {
                var chaos = source.Chaotic(state, rarity);
                if (chaos != null && chaos.Count > 0) return RuneResult.Success(state.WithInscriptions(chaos).Sealed(ItemKeys.EcSealedSerpent), cost, "corrupt_chaos");
            }
            return RuneResult.Success(state.Sealed(ItemKeys.EcSealedSerpent), cost, "corrupt_seal");
        }
    }

    public enum ConfirmDecision { Commit, AskDialog, RefuseConfirmRequired }

    /// <summary>Design B4, step 13: only a use that would succeed is ever confirmed.</summary>
    public static class RuneConfirm
    {
        /// <param name="runeConfirms">The rune's <c>confirm</c> (server data).</param>
        /// <param name="mode">The player's Confirm Destructive Runes.</param>
        /// <param name="shiftHeld">Shift or the pad's left trigger is held.</param>
        /// <param name="padInput">The last input came from a gamepad: HoldShift falls back to the dialog.</param>
        /// <param name="dialogAvailable">The game's yes/no popup can be shown.</param>
        public static ConfirmDecision Decide(bool runeConfirms, ConfirmMode mode, bool shiftHeld, bool padInput, bool dialogAvailable)
        {
            if (!runeConfirms || mode == ConfirmMode.Off) return ConfirmDecision.Commit;
            var wantsDialog = mode == ConfirmMode.Dialog || (mode == ConfirmMode.HoldShift && padInput && !shiftHeld);
            if (!wantsDialog && shiftHeld) return ConfirmDecision.Commit;
            if (wantsDialog && dialogAvailable) return ConfirmDecision.AskDialog;
            return shiftHeld ? ConfirmDecision.Commit : ConfirmDecision.RefuseConfirmRequired;
        }
    }
}
