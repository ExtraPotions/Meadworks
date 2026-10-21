// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ExtraPotions.Hex;

/// <summary>Presentation time follows the native day fraction with an offset, or holds the selected minute.</summary>
public sealed class LocalClock
{
    private int selected = -1;
    private bool frozen;
    private float offset;
    public void Reset() { selected = -1; offset = 0f; }
    public float Resolve(float native, int minute, bool freeze)
    {
        if (minute < 0 || minute > 1439 || float.IsNaN(native) || float.IsInfinity(native)) { Reset(); return native; }
        var target = minute / 1440f;
        if (selected != minute || frozen != freeze)
        {
            selected = minute; frozen = freeze; offset = target - native;
        }
        var result = freeze ? target : native + offset;
        return result - (float)Math.Floor(result);
    }
}
