// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Affixes
{
    /// <summary>
    /// Why an item's state was written, and the API's item-changed listeners (api.md section 5). A writer opens a scope
    /// around its <see cref="ItemState.Write"/> (<c>using (ItemChanges.Because(ItemChanges.Drop))</c>), so the reason
    /// travels without a parameter on the write path; <see cref="ItemStateCache"/> raises the listeners after every
    /// successful write with the reason in force. Reasons: <c>rune:&lt;id&gt;</c>, <c>drop</c>, <c>command</c>,
    /// <c>api</c>, <c>migration</c> (reserved: format migrations happen in memory and are stored by the next write of
    /// another reason), and <c>other</c> for a write outside every scope. Main thread only.
    /// </summary>
    internal static class ItemChanges
    {
        public const string Drop = "drop";
        public const string Command = "command";
        public const string Api = "api";
        public const string Migration = "migration";
        public const string Other = "other";

        private static string? _reason;

        public static readonly Callbacks<Action<ItemDrop.ItemData, string>> Listeners =
            new Callbacks<Action<ItemDrop.ItemData, string>>("item-changed listener");

        /// <summary>The reason of the innermost open scope, <see cref="Other"/> outside every scope.</summary>
        public static string Current => _reason ?? Other;

        /// <summary>The reason of a rune's write: <c>rune:&lt;id&gt;</c>.</summary>
        public static string Rune(string? runeId) => "rune:" + (runeId ?? "unknown");

        /// <summary>Opens a reason scope; dispose it (a <c>using</c>) right after the write.</summary>
        public static Scope Because(string reason)
        {
            Scope scope = new Scope(_reason);
            _reason = reason;
            return scope;
        }

        /// <summary>After a successful write: every listener, each guarded on its own.</summary>
        public static void Raise(ItemDrop.ItemData item)
        {
            Callbacks<Action<ItemDrop.ItemData, string>>.Entry[] listeners = Listeners.Snapshot;
            string reason = Current;
            foreach (Callbacks<Action<ItemDrop.ItemData, string>>.Entry listener in listeners)
            {
                try
                {
                    listener.Callback(item, reason);
                }
                catch (Exception e)
                {
                    Listeners.Failed(listener, e);
                }
            }
        }

        /// <summary>Restores the outer reason when disposed.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly string? _outer;

            internal Scope(string? outer)
            {
                _outer = outer;
            }

            public void Dispose() => _reason = _outer;
        }
    }
}
