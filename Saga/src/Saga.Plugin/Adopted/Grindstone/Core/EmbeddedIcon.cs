// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/EmbeddedIcon.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A skill icon embedded in the DLL, <c>assets/&lt;name&gt;.png</c> (64x64, like the game's skill icons), decoded once
    /// per name. Null on a machine without graphics (a dedicated server never shows icons) and when the file is missing
    /// or will not decode; the log says which.
    /// </summary>
    public static class EmbeddedIcon
    {
        private static readonly Dictionary<string, Sprite> loaded = new Dictionary<string, Sprite>();

        public static Sprite Load(string name)
        {
            if (loaded.TryGetValue(name, out Sprite sprite))
                return sprite;
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                sprite = EmbeddedSprite.Load(typeof(EmbeddedIcon).Assembly, "Saga.assets." + name + ".png", name);
            if (sprite == null && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                NativeSaga.Log.LogWarning("The icon assets/" + name + ".png could not be loaded; the game's icon stays.");
            loaded[name] = sprite;
            return sprite;
        }
    }
}
