// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the meadworks.items.sockets and meadworks.crafting.salvage_rune capabilities (parity #23, #68).
using System;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Api;
using ExtraPotions.Inscribe.Adopted.Display;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Sockets;
using ExtraPotions.Inscribe.Interop;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>
    /// Publishes the socket view other products draw (Mantle's backpack panel, <see cref="SocketsV1"/>) and the rune a
    /// salvage gives back (<see cref="SalvageRuneV1"/>, beside Kindle's <c>meadworks.crafting.salvage_item</c>). Both take the
    /// native <c>ItemDrop.ItemData</c> as an opaque object, never throw, and answer "none" for anything else.
    /// </summary>
    internal static class InscribeSockets
    {
        private static readonly Tuple<string, string, string, string> EmptySocket = Tuple.Create("", "", "", "");

        internal static void Publish(ProductRegistration product)
        {
            product.AddCapability(SocketsV1.Capability, SocketsV1.Version,
                Tuple.Create<Func<object, int>, Func<object, Tuple<string, string, string, string>[]>>(Count, Rows));
            product.AddCapability(SalvageRuneV1.Capability, SalvageRuneV1.Version,
                Tuple.Create<Func<object, string>, Func<object, float>>(
                    value => Safe(() => value is ItemDrop.ItemData item ? ApiItems.SalvageRune(item) : null, null)!,
                    value => Safe(() => value is ItemDrop.ItemData item ? ApiItems.SalvageRuneChance(item) : 0f, 0f)));
        }

        internal static int Count(object value) => Safe(() => SocketSwitch.On && value is ItemDrop.ItemData item ? ItemState.Read(item).Sockets : 0, 0);

        internal static Tuple<string, string, string, string>[] Rows(object value) => Safe(() =>
        {
            if (!SocketSwitch.On || !(value is ItemDrop.ItemData item)) return Array.Empty<Tuple<string, string, string, string>>();
            ItemState state = ItemState.Read(item);
            var rows = new Tuple<string, string, string, string>[state.Sockets];
            for (int socket = 0; socket < rows.Length; socket++) rows[socket] = EmptySocket;
            for (int index = 0; index < state.Gems.Count; index++)
            {
                int socket = state.GemSocketAt(index);
                if (socket < 0 || socket >= rows.Length) continue;
                GemRoll gem = state.Gems[index];
                rows[socket] = Tuple.Create(gem.GemId, GemLines.GemName(gem.GemId), "#" + ColorUtility.ToHtmlStringRGB(StoneVisuals.Tint(gem.GemId)),
                    AffixLines.Sentence(gem.Roll.Id, gem.Roll.Value, state.GemDefinitionAt(index)));
            }
            return rows;
        }, Array.Empty<Tuple<string, string, string, string>>());

        private static T Safe<T>(Func<T> read, T fallback)
        {
            try { return read(); }
            catch (Exception) { return fallback; }
        }
    }
}
