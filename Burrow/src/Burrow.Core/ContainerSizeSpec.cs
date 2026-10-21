// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (parity choice #8, behavior described by ChestSizeConfig's README; no code taken): the `double` and
// `vanilla` keywords and WIDTHxROWS sizes of Burrow.Containers.yml, and the one `chests:` line that sizes every buildable chest.
// The `wide` keyword is the default: the inventory's 8 columns holding at least the doubled slot count.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExtraPotions.Burrow
{
    public enum ContainerSizeKind { Exact, Double, Vanilla, Wide }

    /// <summary>One size entry: an exact grid, twice the game's rows, the inventory's width with as many slots, or the game's own size.</summary>
    public readonly struct ContainerSizeSpec
    {
        private ContainerSizeSpec(ContainerSizeKind kind, int width, int height)
        {
            Kind = kind;
            Width = width;
            Height = height;
        }

        public ContainerSizeKind Kind { get; }
        public int Width { get; }
        public int Height { get; }

        public static ContainerSizeSpec Exact(int width, int height)
        {
            if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width), "width and height must be at least 1");
            return new ContainerSizeSpec(ContainerSizeKind.Exact, width, height);
        }

        public static ContainerSizeSpec Double => new ContainerSizeSpec(ContainerSizeKind.Double, 0, 0);
        public static ContainerSizeSpec Vanilla => new ContainerSizeSpec(ContainerSizeKind.Vanilla, 0, 0);
        public static ContainerSizeSpec Wide => new ContainerSizeSpec(ContainerSizeKind.Wide, 0, 0);

        /// <summary>The player inventory's width, which <c>wide</c> chests take (and the container panel's).</summary>
        public const int InventoryColumns = 8;

        /// <summary><c>wide</c>, <c>double</c>, <c>vanilla</c> or <c>WIDTHxROWS</c> (spaces and case ignored).</summary>
        public static bool TryParse(string? text, out ContainerSizeSpec spec, out string error)
        {
            spec = Vanilla;
            error = "";
            string value = (text ?? "").Trim();
            if (value.Equals("double", StringComparison.OrdinalIgnoreCase))
            {
                spec = Double;
                return true;
            }
            if (value.Equals("wide", StringComparison.OrdinalIgnoreCase))
            {
                spec = Wide;
                return true;
            }
            if (value.Equals("vanilla", StringComparison.OrdinalIgnoreCase))
                return true;
            int x = value.IndexOfAny(new[] { 'x', 'X' });
            if (x > 0 && int.TryParse(value.Substring(0, x).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int width)
                && int.TryParse(value.Substring(x + 1).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int height)
                && width >= 1 && height >= 1)
            {
                spec = Exact(width, height);
                return true;
            }
            error = $"'{value}' is not a size: write wide, double, vanilla or WIDTHxROWS (for example 6x4)";
            return false;
        }

        /// <summary>The grid for a container whose game size is <paramref name="vanillaWidth"/> x <paramref name="vanillaHeight"/>.</summary>
        public (int Width, int Height) Resolve(int vanillaWidth, int vanillaHeight)
        {
            switch (Kind)
            {
                case ContainerSizeKind.Exact: return (Width, Height);
                case ContainerSizeKind.Double: return (vanillaWidth, Math.Max(1, vanillaHeight) * 2);
                case ContainerSizeKind.Wide:
                    // A chest already wider than the inventory keeps its width and doubles its rows.
                    if (vanillaWidth > InventoryColumns) return (vanillaWidth, Math.Max(1, vanillaHeight) * 2);
                    int slots = Math.Max(1, vanillaWidth) * Math.Max(1, vanillaHeight) * 2;
                    return (InventoryColumns, (slots + InventoryColumns - 1) / InventoryColumns);
                default: return (vanillaWidth, vanillaHeight);
            }
        }

        /// <summary>
        /// The grid to use for a container that should be <paramref name="width"/> x <paramref name="height"/> but holds an item as far out
        /// as column <paramref name="lastColumn"/> and row <paramref name="lastRow"/> (-1 when empty): grown just enough that no item is hidden.
        /// </summary>
        public static (int Width, int Height) Cover(int width, int height, int lastColumn, int lastRow) =>
            (Math.Max(width, lastColumn + 1), Math.Max(height, lastRow + 1));

        public override string ToString() => Kind == ContainerSizeKind.Exact ? Width + "x" + Height : Kind.ToString().ToLowerInvariant();
    }

    /// <summary>Which entry sizes a container prefab: its own line wins, then the <c>chests:</c> line for a buildable chest.</summary>
    public static class ContainerSizeDefaults
    {
        /// <summary>The top-level key of Burrow.Containers.yml that sizes every chest a player can build.</summary>
        public const string ChestsKey = "chests";

        /// <param name="keepsOwnSize">
        /// The container keeps the size its own mod gives it (a Shepherd feeding trough, which answers the <c>feed</c> container
        /// role): neither the <c>chests:</c> line nor an entry of its own resizes it.
        /// </param>
        public static ContainerSizeSpec? For(string prefab, bool buildableChest, IReadOnlyDictionary<string, ContainerSizeSpec> listed, ContainerSizeSpec? chests,
            bool keepsOwnSize = false)
        {
            if (keepsOwnSize)
                return null;
            if (listed != null)
                foreach (KeyValuePair<string, ContainerSizeSpec> entry in listed)
                    if (string.Equals(entry.Key, prefab, StringComparison.OrdinalIgnoreCase))
                        return entry.Value;
            return buildableChest ? chests : null;
        }
    }
}
