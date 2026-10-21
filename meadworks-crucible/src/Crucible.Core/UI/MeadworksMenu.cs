// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the registry behind the single "MeadWorks" pause-menu entry and the MeadWorks menu key.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExtraPotions.Meadworks.Crucible
{
    /// <summary>One settings window a product offers in the MeadWorks chooser, as listed now.</summary>
    public readonly struct MeadworksMenuChoice
    {
        public MeadworksMenuChoice(string id, string caption)
        {
            Id = id;
            Caption = caption;
        }

        public string Id { get; }
        public string Caption { get; }
    }

    /// <summary>
    /// The settings windows of every Meadworks product that is present. A product registers one entry (id, caption, open
    /// callback, optional gate) instead of adding a pause-menu button of its own; the graphical kit draws one "MeadWorks"
    /// pause-menu button that opens a chooser listing the entries whose gate is open. Portable (no Unity) so the rules are
    /// tested without the game. Registering an id again replaces the entry, and a stale registration handle cannot remove
    /// its replacement. A faulting caption or gate hides that one entry; a faulting open callback is contained.
    /// </summary>
    public sealed class MeadworksMenuRegistry
    {
        sealed class Entry
        {
            public string Id = "";
            public Func<string> Caption = () => "";
            public Action Open = () => { };
            public Func<bool>? Available;
            public int Order;
        }

        sealed class Handle : IDisposable
        {
            readonly MeadworksMenuRegistry owner;
            readonly Entry entry;

            public Handle(MeadworksMenuRegistry owner, Entry entry)
            {
                this.owner = owner;
                this.entry = entry;
            }

            public void Dispose() => owner.Remove(entry);
        }

        readonly object gate = new object();
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>Registered entries, listed or not.</summary>
        public int Count
        {
            get { lock (gate) return entries.Count; }
        }

        /// <param name="id">Stable lower-case id without spaces, for example <c>cairn</c>.</param>
        /// <param name="caption">The button text.</param>
        /// <param name="open">Opens the product's settings window. It also decides what happens to the pause menu.</param>
        /// <param name="available">Listed only while this returns true (for example admin only); null is always.</param>
        /// <param name="order">Lower first; ties by caption.</param>
        public IDisposable Register(string id, string caption, Action open, Func<bool>? available = null, int order = 100)
        {
            if (string.IsNullOrWhiteSpace(caption)) throw new ArgumentException("A menu entry needs a caption.", nameof(caption));
            return Register(id, () => caption, open, available, order);
        }

        /// <summary>As above with a caption read each time the chooser draws, so it can follow the language.</summary>
        public IDisposable Register(string id, Func<string> caption, Action open, Func<bool>? available = null, int order = 100)
        {
            if (string.IsNullOrEmpty(id) || id.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) throw new ArgumentException("A menu entry id is a short word without spaces.", nameof(id));
            if (caption == null) throw new ArgumentNullException(nameof(caption));
            if (open == null) throw new ArgumentNullException(nameof(open));
            var entry = new Entry { Id = id, Caption = caption, Open = open, Available = available, Order = order };
            lock (gate) entries[id] = entry;
            return new Handle(this, entry);
        }

        void Remove(Entry entry)
        {
            lock (gate)
            {
                if (entries.TryGetValue(entry.Id, out var current) && ReferenceEquals(current, entry)) entries.Remove(entry.Id);
            }
        }

        /// <summary>The entries whose gate is open, in display order, with their current captions.</summary>
        public IReadOnlyList<MeadworksMenuChoice> Choices()
        {
            Entry[] snapshot;
            lock (gate) snapshot = entries.Values.ToArray();
            var listed = new List<(Entry Entry, string Caption)>();
            foreach (var entry in snapshot)
            {
                try
                {
                    if (entry.Available != null && !entry.Available()) continue;
                    var caption = entry.Caption();
                    if (string.IsNullOrWhiteSpace(caption)) continue;
                    listed.Add((entry, caption));
                }
                catch (Exception)
                {
                    // One product's broken caption or gate must not take the others' entries down.
                }
            }
            return listed
                .OrderBy(item => item.Entry.Order)
                .ThenBy(item => item.Caption, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Entry.Id, StringComparer.Ordinal)
                .Select(item => new MeadworksMenuChoice(item.Entry.Id, item.Caption))
                .ToList();
        }

        /// <summary>Runs the entry's open callback when it is registered and listed; false otherwise or when the callback throws.</summary>
        public bool Open(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            Entry? entry;
            lock (gate) entries.TryGetValue(id, out entry);
            if (entry == null) return false;
            try
            {
                if (entry.Available != null && !entry.Available()) return false;
                entry.Open();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Changes whenever the listed entries or their captions change; the chooser redraws on a new value.</summary>
        public string Fingerprint()
        {
            var text = new StringBuilder();
            foreach (var choice in Choices()) text.Append(choice.Id).Append('=').Append(choice.Caption).Append('\n');
            return text.ToString();
        }
    }

    /// <summary>The process-wide registry products register with, and the one pause-menu caption.</summary>
    public static class MeadworksMenu
    {
        /// <summary>The single pause-menu button's caption.</summary>
        public const string Caption = "Meadworks";

        public static MeadworksMenuRegistry Registry { get; } = new MeadworksMenuRegistry();

        public static IDisposable Register(string id, string caption, Action open, Func<bool>? available = null, int order = 100) =>
            Registry.Register(id, caption, open, available, order);

        public static IDisposable Register(string id, Func<string> caption, Action open, Func<bool>? available = null, int order = 100) =>
            Registry.Register(id, caption, open, available, order);
    }

    /// <summary>What <see cref="MeadworksMenuReturn.Tick"/> asks the chooser to do this frame.</summary>
    public enum MeadworksMenuReturnAction { None, CloseGameMenu, Reopen, Forget }

    /// <summary>
    /// A window opened from the MeadWorks chooser returns to the chooser when it closes, not to the game's pause menu. After
    /// a choice, the first frame a window is open hides the pause menu (it stayed up under the window and took back the
    /// selection on every click); when no window is open any more the chooser reopens. An entry that opens no window within
    /// <see cref="Grace"/> seconds (a toggle such as Wildroot on/off) is forgotten, so a later unrelated close never pops it.
    /// </summary>
    public sealed class MeadworksMenuReturn
    {
        public const float Grace = 1f;
        bool pending, seen;
        float deadline;
        int generation;

        /// <summary>True between a choice and its window closing (or the grace running out).</summary>
        public bool Pending => pending;

        public void Chosen(float now) { generation++; pending = true; seen = false; deadline = now + Grace; }

        public void Clear() { generation++; pending = false; seen = false; }

        public sealed class Suspension : IDisposable
        {
            readonly MeadworksMenuReturn owner;
            readonly int generation;
            readonly bool pending;
            bool finished;
            internal Suspension(MeadworksMenuReturn owner, int generation, bool pending)
            { this.owner = owner; this.generation = generation; this.pending = pending; }
            public bool IsCurrent => !finished && owner.generation == generation;
            public void Resume(float now)
            {
                if (finished) return;
                finished = true;
                if (pending && owner.generation == generation) owner.Chosen(now);
            }
            public void Dispose() { finished = true; }
        }

        /// <summary>Temporarily hands return navigation to a child window; newer navigation invalidates the lease.</summary>
        public Suspension Suspend()
        {
            bool wasPending = pending;
            Clear();
            return new Suspension(this, generation, wasPending);
        }

        /// <param name="now">Unscaled time.</param>
        /// <param name="windowOpen">Any MeadWorks window is open.</param>
        public MeadworksMenuReturnAction Tick(float now, bool windowOpen)
        {
            if (!pending) return MeadworksMenuReturnAction.None;
            if (windowOpen)
            {
                if (seen) return MeadworksMenuReturnAction.None;
                seen = true;
                return MeadworksMenuReturnAction.CloseGameMenu;
            }
            if (seen) { Clear(); return MeadworksMenuReturnAction.Reopen; }
            if (now < deadline) return MeadworksMenuReturnAction.None;
            Clear();
            return MeadworksMenuReturnAction.Forget;
        }
    }

    /// <summary>What a MeadWorks menu key press does this frame.</summary>
    public enum MeadworksMenuKeyAction { None, Open, Close }

    /// <summary>
    /// The one MeadWorks menu key (Crucible.UI setting <c>General / Meadworks Menu Key</c>, F8 by default, empty means none). It
    /// opens the same chooser as Esc &gt; MeadWorks from anywhere in game; products keep their own keys but leave them unbound.
    /// </summary>
    public static class MeadworksMenuKey
    {
        public const string Section = "General";
        public const string Name = "Meadworks Menu Key";
        public const string DefaultChord = "F8";

        /// <summary>Works under the pause menu, the inventory and product windows; never while chat, the console or a text field has the keys.</summary>
        public const InputContext Context = InputContext.Global;

        /// <param name="pressed">The key went down this frame (input gate already applied).</param>
        /// <param name="chooserOpen">The chooser is open.</param>
        /// <param name="listed">Entries listed now.</param>
        public static MeadworksMenuKeyAction Decide(bool pressed, bool chooserOpen, int listed)
        {
            if (!pressed) return MeadworksMenuKeyAction.None;
            if (chooserOpen) return MeadworksMenuKeyAction.Close;
            return listed > 0 ? MeadworksMenuKeyAction.Open : MeadworksMenuKeyAction.None;
        }
    }
}
