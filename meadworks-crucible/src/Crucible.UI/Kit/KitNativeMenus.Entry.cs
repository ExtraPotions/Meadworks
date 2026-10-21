// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        /// <summary>
        /// The native entries' look for a MeadWorks button copied from a native menu button (the pause menu's MeadWorks
        /// entry): the same wood button face and font while styling is on, the copied native look while it is off. Native
        /// surfaces skip MeadWorks-named controls, so the copy carries its own look. Call <see cref="Prepare"/> on the fresh
        /// copy before it is shown, then <see cref="Apply"/> each frame.
        /// </summary>
        internal sealed class EntryLook : IDisposable
        {
            readonly Button button;
            readonly ButtonLook image;
            readonly TextButtonLook text;
            readonly FontLook font;
            bool applied, on;

            EntryLook(Button button, ButtonLook image, TextButtonLook text, FontLook font)
            { this.button = button; this.image = image; this.text = text; this.font = font; }

            /// <summary>
            /// Strips the native look's own parts from a copy of <paramref name="source"/> (they belong to the source and stay
            /// inert on a copy) and gives the copy's labels the native fonts back, then dresses the copy. Null when the
            /// button has no single label or image to dress.
            /// </summary>
            internal static EntryLook Prepare(Button copy, Button source)
            {
                if (copy == null) return null;
                foreach (var part in copy.GetComponentsInChildren<KitNativeMenuTextButton>(true))
                    if (part != null && part.gameObject != copy.gameObject) Object.DestroyImmediate(part.gameObject);
                foreach (var part in copy.GetComponentsInChildren<Transform>(true))
                    if (part != null && part != copy.transform && part.name.StartsWith("Crucible_native_", StringComparison.Ordinal))
                        Object.DestroyImmediate(part.gameObject);
                var labels = copy.GetComponentsInChildren<TMP_Text>(true);
                var native = source != null ? source.GetComponentsInChildren<TMP_Text>(true) : Array.Empty<TMP_Text>();
                if (labels.Length == native.Length)
                    for (int i = 0; i < labels.Length; i++)
                        if (NativeFont(native[i], out var asset, out var material)) { labels[i].font = asset; labels[i].fontSharedMaterial = material; }
                var graphic = copy.targetGraphic;
                TMP_Text label = graphic as TMP_Text;
                if (label == null && labels.Length == 1) label = labels[0];
                if (graphic is Image face)
                {
                    // A copy of a dressed face would remember the kit's art as its native art.
                    if (source != null && source.targetGraphic is Image sourceFace && owned.Contains(sourceFace)) return null;
                    return new EntryLook(copy, new ButtonLook(copy, face), null, label != null ? new FontLook(label) : null);
                }
                if ((graphic == null || graphic is TMP_Text) && label != null && TextButtonLook.Supported)
                    return new EntryLook(copy, null, new TextButtonLook(copy, label), new FontLook(label));
                return null;
            }

            /// <summary>Follows the native menus' styling switch (<see cref="On"/>).</summary>
            internal void Apply()
            {
                if (button == null) return;
                bool now = On;
                // The text face hides itself while the menu is closed and stops its own refresh; this brings it back.
                text?.Apply(now);
                if (applied && now == on) return;
                applied = true; on = now;
                image?.Apply(now); font?.Apply(now);
            }

            public void Dispose()
            {
                image?.Dispose(); text?.Dispose(); font?.Apply(false);
            }
        }

        /// <summary>The font a styled native label had before styling, from the surface that dressed it.</summary>
        static bool NativeFont(TMP_Text label, out TMP_FontAsset font, out Material material)
        {
            font = null; material = null;
            if (label == null) return false;
            foreach (var surface in surfaces.Values)
                if (surface.NativeFont(label, out font, out material)) return true;
            return false;
        }
    }
}
