// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible
{
    public enum MeadworksProductOpen { Unavailable, Settings, Feature, Choices }

    public static class MeadworksMenuProducts
    {
        const string Prefix = "com.meadworks.";
        public static GridCell Cell(int index) => KitTileLayout.Cell(index, 2);
        public static int Rows(int count) => KitTileLayout.Rows(count, 2);

        public static IReadOnlyList<MeadworksMenuChoice> Roster(IEnumerable<MeadworksMenuChoice> plugins) => plugins
            .Where(p => p.Id.StartsWith(Prefix, StringComparison.Ordinal) && p.Id.Length > Prefix.Length && p.Id.IndexOf('.', Prefix.Length) < 0 && p.Id != "com.meadworks.testbridge")
            .GroupBy(p => p.Id, StringComparer.Ordinal)
            .Select(g => new MeadworksMenuChoice(g.Key.Substring(Prefix.Length), Label(g.First().Caption)))
            .OrderBy(p => p.Caption, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Id, StringComparer.Ordinal).ToArray();

        static string Label(string caption)
        {
            const string branding = "ExtraPotions Meadworks: ";
            return caption.StartsWith(branding, StringComparison.Ordinal) ? caption.Substring(branding.Length) : caption;
        }

        public static IReadOnlyList<MeadworksMenuChoice> Actions(string product, IEnumerable<MeadworksMenuChoice> choices) => choices
            .Where(c => c.Id == product || c.Id.StartsWith(product + ".", StringComparison.Ordinal) || c.Id.StartsWith(product + "-", StringComparison.Ordinal)).ToArray();

        /// <summary>Settings are the product's entry point; custom windows remain the standalone fallback.</summary>
        public static MeadworksProductOpen Open(string product, MeadworksMenuRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (MeadworksMenuSettings.Open(product)) return MeadworksProductOpen.Settings;
            var actions = Actions(product, registry.Choices());
            if (actions.Count > 1) return MeadworksProductOpen.Choices;
            return actions.Count == 1 && registry.Open(actions[0].Id) ? MeadworksProductOpen.Feature : MeadworksProductOpen.Unavailable;
        }
    }

    public static class MeadworksMenuSettings
    {
        sealed class Route : IDisposable
        {
            internal readonly Func<string, bool> Available;
            internal readonly Action<string> Open;
            internal Route(Func<string, bool> available, Action<string> open) { Available = available; Open = open; }
            public void Dispose() { if (ReferenceEquals(current, this)) current = null; }
        }
        static Route? current;
        public static IDisposable Register(Func<string, bool> available, Action<string> open)
        {
            if (available == null) throw new ArgumentNullException(nameof(available));
            if (open == null) throw new ArgumentNullException(nameof(open));
            return current = new Route(available, open);
        }
        public static bool Available(string product)
        {
            var route = current; var id = Resolve(product);
            try { return route != null && id != null && route.Available(id); } catch { return false; }
        }
        public static bool Open(string product)
        {
            var route = current; var id = Resolve(product);
            try { if (route == null || id == null || !route.Available(id)) return false; route.Open(id); return true; }
            catch { return false; }
        }
        static string? Resolve(string product)
        {
            if (string.IsNullOrWhiteSpace(product) || product.Any(char.IsWhiteSpace)) return null;
            return product.IndexOf('.') >= 0 ? product : "com.meadworks." + product;
        }
    }
}
