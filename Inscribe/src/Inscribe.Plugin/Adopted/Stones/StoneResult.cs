// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; a returned gem.
namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>A player-facing message: <c>$ecf_msg_&lt;id&gt;</c> with up to three already-localized words for $1..$3.</summary>
    internal sealed class StoneMessage
    {
        public StoneMessage(string id, string[] words)
        {
            Id = id;
            Words = words;
        }

        public string Id { get; }
        public string[] Words { get; }

        public string Key => "$ecf_msg_" + Id;
    }

    /// <summary>
    /// What the pipeline decided for one click (applying-stones.md section 2): a refusal, or the dry-run state that
    /// will be written unchanged on commit, with its feedback message. A refusal never changed anything.
    /// </summary>
    internal sealed class StoneResult
    {
        private StoneResult(StoneMessage? refusal, Affixes.ItemState? state, StoneMessage? feedback)
        {
            Refusal = refusal;
            State = state;
            Feedback = feedback;
        }

        public StoneMessage? Refusal { get; }

        /// <summary>The dry-run result: exactly what commits (nothing is rolled twice).</summary>
        public Affixes.ItemState? State { get; }

        public StoneMessage? Feedback { get; }

        public bool Refused => Refusal != null;

        /// <summary>A gem onto an item whose sockets are all full: the player picks the one to replace (<see cref="GemChooser"/>).</summary>
        public bool NeedsSocket { get; private set; }

        public static StoneResult Refuse(string id, params string[] words) =>
            new StoneResult(new StoneMessage(id, words), null, null);

        public static StoneResult ChooseSocket() => new StoneResult(null, null, null) { NeedsSocket = true };

        /// <summary>A Honing Rune with more than one inscription it can change: the player picks (InscriptionChooser).</summary>
        public bool NeedsInscription { get; private set; }

        public static StoneResult ChooseInscription() => new StoneResult(null, null, null) { NeedsInscription = true };

        public static StoneResult Success(Affixes.ItemState state, string feedbackId, params string[] words) =>
            new StoneResult(null, state, new StoneMessage(feedbackId, words));

        /// <summary>A replaced gem that comes back after the commit (gem recovery, rolled in the dry run); null when none.</summary>
        public string? ReturnedGem { get; private set; }

        public StoneResult Returning(string? gemId)
        {
            ReturnedGem = gemId;
            return this;
        }
    }
}
