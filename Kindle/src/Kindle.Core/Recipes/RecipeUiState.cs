// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Kindle.Recipes
{
    /// <summary>
    /// What the recipe search row's buttons show. The crafting panel's tick writes the buttons (glyphs, tooltips, the sort
    /// word) only when this changes.
    /// <see cref="Row"/> is the row object, so a rebuilt row is always written; the default value matches no row.
    /// </summary>
    public readonly struct SearchButtonsState : IEquatable<SearchButtonsState>
    {
        public object? Row { get; }
        public bool Only { get; }
        public int View { get; }
        public bool ViewLocked { get; }
        public int Sort { get; }
        /// <summary>The texts' revision: an applied translation override writes the words again.</summary>
        public long Revision { get; }
        public SearchButtonsState(object? row, bool only, int view, bool viewLocked, int sort, long revision)
        { Row = row; Only = only; View = view; ViewLocked = viewLocked; Sort = sort; Revision = revision; }
        public bool Equals(SearchButtonsState other) => Row != null && ReferenceEquals(Row, other.Row) && Only == other.Only && View == other.View
            && ViewLocked == other.ViewLocked && Sort == other.Sort && Revision == other.Revision;
        public override bool Equals(object? obj) => obj is SearchButtonsState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Row, Only, View, ViewLocked, Sort, Revision);
    }

    /// <summary>The Track button's word (Track or Untrack); written again only when the button, the word or the texts change.</summary>
    public readonly struct TrackLabelState : IEquatable<TrackLabelState>
    {
        public object? Button { get; }
        public bool Tracked { get; }
        public long Revision { get; }
        public TrackLabelState(object? button, bool tracked, long revision) { Button = button; Tracked = tracked; Revision = revision; }
        public bool Equals(TrackLabelState other) => Button != null && ReferenceEquals(Button, other.Button) && Tracked == other.Tracked && Revision == other.Revision;
        public override bool Equals(object? obj) => obj is TrackLabelState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Button, Tracked, Revision);
    }
}
