// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Barter.Lists
{
    /// <summary>Which way a list trades: Buy lists are what the trader sells to the player, Sell lists what the player may sell to the trader.</summary>
    public enum ListKind
    {
        Buy,
        Sell
    }

    /// <summary>How a list file is written. Yaml also reads JSON (JSON is YAML flow syntax).</summary>
    public enum ListFormat
    {
        Yaml,
        Json,
        Csv
    }
}
