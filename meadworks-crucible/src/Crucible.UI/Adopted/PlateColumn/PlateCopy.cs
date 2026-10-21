// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/PlateCopy.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// A mod's box is a copy of the game's armour box, already restyled - the same brown background, the same icon
    /// placement, the same font and number line - that keeps its background, its icon (showing the mod's sprite) and,
    /// when asked, its text (emptied, for the mod to write), and loses everything else: other children and every
    /// behaviour on its root, so nothing the game or another mod put there comes along. It is made inside the container,
    /// where the column puts it in rank order. The stripping is immediate, so a tooltip added in the same frame is the
    /// only one on it.
    /// </summary>
    internal static class PlateCopy
    {
        /// <summary>The copy, inside <paramref name="parent"/> (the column's container or the HUD row), else beside the source.</summary>
        public static Plate? Make(RectTransform source, PlateSpec spec, string name, Transform? parent = null)
        {
            GameObject go = Object.Instantiate(source.gameObject, parent != null ? parent : source.parent, false);
            go.name = name;
            Transform box = go.transform;
            TMP_Text? text = go.GetComponentInChildren<TMP_Text>(true);
            Transform? textChild = PlateParts.ChildHolding(box, text != null ? text.transform : null);
            Image? background = PlateParts.BackgroundOf(box);
            Image? icon = PlateParts.IconOf(box, textChild);
            if (icon == null)
            {
                Object.DestroyImmediate(go);
                return null;
            }
            Strip(go, background, icon, spec.WithText ? textChild : null);
            TMP_Text? kept = spec.WithText ? text : null;
            Dress(icon, kept, spec);
            go.SetActive(true);
            return new Plate((RectTransform)box, icon, kept);
        }

        /// <summary>A box this column already has, found again by its parts.</summary>
        public static Plate? Wrap(RectTransform box)
        {
            TMP_Text? text = box.GetComponentInChildren<TMP_Text>(true);
            Image? icon = PlateParts.IconOf(box, PlateParts.ChildHolding(box, text != null ? text.transform : null));
            return icon != null ? new Plate(box, icon, text) : null;
        }

        /// <summary>
        /// The mod's sprite in place of the armour icon, and no armour number until the mod writes its own; a box without
        /// a number shows its icon larger and centred rather than at the top above an empty line.
        /// </summary>
        private static void Dress(Image icon, TMP_Text? text, PlateSpec spec)
        {
            if (spec.Icon != null)
            {
                icon.sprite = spec.Icon;
            }
            if (text != null)
            {
                text.text = "";
                return;
            }
            BoxStyle.IconAlone(icon);
        }

        private static void Strip(GameObject go, Image? background, Image icon, Transform? keptText)
        {
            Transform box = go.transform;
            for (int i = box.childCount - 1; i >= 0; i--)
            {
                Transform child = box.GetChild(i);
                bool keep = (background != null && child == background.transform) || child == icon.transform || child == keptText;
                if (!keep)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
            foreach (Behaviour behaviour in go.GetComponents<Behaviour>())
            {
                Object.DestroyImmediate(behaviour);
            }
        }
    }
}
