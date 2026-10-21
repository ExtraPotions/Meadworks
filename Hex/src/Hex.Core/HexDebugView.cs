// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ExtraPotions.Hex;

/// <summary>Debug Mode's Shift+C: vertical mouse movement turns the local time of day, horizontal the camera's field of view (the game's own debug rates).</summary>
public static class HexDebugView
{
    public const float DayPerMouse = 0.005f, FovMin = 0.5f, FovMax = 165f;
    public static float Day(float fraction, float mouseY)
    {
        var day = (fraction + mouseY * DayPerMouse) % 1f;
        return day < 0f ? day + 1f : day;
    }
    public static float Fov(float fov, float mouseX) => Math.Max(FovMin, Math.Min(FovMax, fov + mouseX));
}
