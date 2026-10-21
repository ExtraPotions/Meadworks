// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Ember.Torches
{
    public static class FireComfort
    {
        static readonly HashSet<string> placed = new HashSet<string>(FirePieceList.Parse(FirePieceList.Default), StringComparer.Ordinal)
            { "fire_pit" };
        public static bool Placed(string? prefab) => prefab != null && placed.Contains(prefab);
        public static bool Held(string? prefab) => prefab == "Torch";
    }
}
