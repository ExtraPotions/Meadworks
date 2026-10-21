// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Config;
using ExtraPotions.Wildblood.Adopted.Patches;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedCm;
using ExtraPotions.Wildblood.Runtime;
using TMPro;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Display
{
    /// <summary>
    /// With Trait Names Above Creature Name on, an ordinary creature's health bar shows its mutations, boss aspects and
    /// native modifiers once each on a smaller line above the name, and the name itself without trait words. The line
    /// copies the name's font and material, is centered on the drawn name, and is rebuilt only when the traits, the
    /// shown modifiers or the language change. Boss bars, mount bars and disguised creatures keep the decorated name.
    /// Turning the setting off, or the bar going away, removes the line and gives the name its trait words back.
    /// </summary>
    public sealed class TraitLine : MonoBehaviour
    {
        internal const string ObjectName = "Wildblood_TraitLine";
        private const string Separator = " · ";
        private const float SizeRatio = 0.72f;
        private const float Gap = 1f;
        private const int NameRefreshFrames = 30;

        /// <summary>Creatures whose health bar name is shown plain because their line carries the trait words.</summary>
        private static readonly HashSet<Character> PlainNames = new HashSet<Character>();
        private static readonly List<string> Names = new List<string>();
        private static readonly HashSet<string> Seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>True while the game updates the enemy health bars, so only that path reads the plain name.</summary>
        internal static bool InHudUpdate;

        private Character _character = null!;
        private TMP_Text _name = null!;
        private const float MarkGap = 2f;
        private static readonly Vector3[] Corners = new Vector3[4];
        private readonly Mark[] _marks = new Mark[2];

        /// <summary>A native alert or awareness mark lifted above the line, with the place the game gave it.</summary>
        private struct Mark
        {
            internal RectTransform? Rect;
            internal Vector2 Native;
            internal bool Moved;
        }
        private TextMeshProUGUI _text = null!;
        private int _mask = -1, _extras = -1, _language = -1, _nameFrame = -1000;
        private long _modifiers = -1;
        private Aspect _aspect;
        private bool _within = true, _removed;
        private string _plain = "";

        internal static bool ShowsPlainName(Character character) => InHudUpdate && character != null && PlainNames.Contains(character);

        /// <summary>Adds, keeps or removes the line for one health bar. Called each frame from the health bar update.</summary>
        internal static void Apply(EnemyHud.HudData data, EliteController controller)
        {
            Character character = data.m_character;
            TraitLine? line = data.m_gui.GetComponent<TraitLine>();
            bool wanted = Configuration.TraitNamesAboveName.Value && Configuration.ShowTraitNames.Value && data.m_name != null
                && !data.m_isMount && !character.IsBoss() && !CreatureKarmaManager.IsBossHudOnly(character) && !Disguise.Holds(character);
            if (!wanted)
            {
                if (line != null) line.Remove();
                return;
            }
            if (line == null) line = Create(data);
            line.Refresh(controller, data.m_gui);
        }

        private static TraitLine Create(EnemyHud.HudData data)
        {
            TraitLine line = data.m_gui.AddComponent<TraitLine>();
            line._character = data.m_character;
            line._name = data.m_name;
            line._marks[0].Rect = data.m_alerted;
            line._marks[1].Rect = data.m_aware;
            GameObject go = new GameObject(ObjectName, typeof(RectTransform));
            go.SetActive(false);
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(data.m_name.transform, false);
            RectTransform nameRect = data.m_name.rectTransform;
            rect.anchorMin = rect.anchorMax = nameRect.pivot; // anchored on the name's own origin, so text bounds map directly
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(Mathf.Max(200f, nameRect.rect.width), 20f);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            CreatureModifierManager.ApplyNameFont(text, data.m_name);
            text.raycastTarget = false;
            text.richText = false;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Bottom;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            line._text = text;

            PlainNames.Add(data.m_character);
            return line;
        }

        private void Refresh(EliteController controller, GameObject gui)
        {
            CreatureTraits traits = controller.Traits;
            long modifiers = CreatureModifierManager.HudModifierMask(_character, gui);
            bool within = HoverNamePatch.Within(_character);
            int language = WildbloodText.LanguageVersion;
            bool changed = traits.Mask != _mask || traits.Aspect != _aspect || traits.ExtraAspects != _extras || modifiers != _modifiers
                || language != _language || within != _within;
            if (changed)
            {
                _mask = traits.Mask; _aspect = traits.Aspect; _extras = traits.ExtraAspects; _modifiers = modifiers;
                _language = language; _within = within;
                string line = within ? Build(traits, modifiers) : "";
                _text.text = line;
                if (_text.gameObject.activeSelf != (line.Length > 0)) _text.gameObject.SetActive(line.Length > 0);
            }
            int frame = Time.frameCount;
            if (changed || frame - _nameFrame >= NameRefreshFrames) // a renamed tame or another mod's name change
            {
                _nameFrame = frame;
                _plain = global::Localization.instance != null ? global::Localization.instance.Localize(HoverNamePatch.Plain(_character)) : HoverNamePatch.Plain(_character);
            }
            if (!string.Equals(_name.text, _plain, StringComparison.Ordinal)) _name.text = _plain;
            if (_text.gameObject.activeSelf) { Place(); LiftMarks(); }
            else RestoreMarks();
        }

        private static string Build(CreatureTraits traits, long modifiers)
        {
            Names.Clear();
            foreach (Aspect aspect in traits.Aspects()) Names.Add(WildbloodText.Text("aspect_" + aspect.ToString().ToLowerInvariant()));
            foreach (Mutation mutation in traits.Active()) Names.Add(WildbloodText.Text("mutation_" + mutation.ToString().ToLowerInvariant()));
            CreatureModifierManager.AddHudModifierNames(modifiers, Names);
            Seen.Clear();
            var text = new System.Text.StringBuilder();
            foreach (string raw in Names)
            {
                string name = (raw ?? "").Trim();
                if (name.Length == 0 || !Seen.Add(name)) continue; // a mutation and a modifier with the same words show once
                if (text.Length > 0) text.Append(Separator);
                text.Append(name);
            }
            return text.ToString();
        }

        // Centered over the drawn name, just above its top: text bounds are in the name's local space, measured from its pivot.
        private void Place()
        {
            float size = Mathf.Max(8f, _name.fontSize * SizeRatio);
            if (!Mathf.Approximately(_text.fontSize, size)) _text.fontSize = size;
            if (_text.color != _name.color) _text.color = _name.color;
            Bounds bounds = _name.textBounds;
            float top = bounds.size.y > 0f ? bounds.max.y : _name.fontSize * 0.6f;
            float center = bounds.size.x > 0f ? bounds.center.x : 0f;
            Vector2 at = new Vector2(center, top + Gap);
            RectTransform rect = _text.rectTransform;
            if ((rect.anchoredPosition - at).sqrMagnitude > 0.01f) rect.anchoredPosition = at;
            float height = Mathf.Ceil(size * 1.25f); // the rect's top is the line's top, which the marks sit on
            if (!Mathf.Approximately(rect.sizeDelta.y, height)) rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        // The game's alert and awareness marks move up to sit just above the line, centered on it, while it shows. Each
        // target is worked out from the mark's native place, so repeated frames never add up.
        private void LiftMarks()
        {
            _text.rectTransform.GetWorldCorners(Corners);
            Vector3 lineTop = (Corners[1] + Corners[2]) * 0.5f;
            for (int i = 0; i < _marks.Length; i++)
            {
                RectTransform? mark = _marks[i].Rect;
                if (mark == null || !(mark.parent is RectTransform parent)) continue;
                if (!_marks[i].Moved) { _marks[i].Native = mark.anchoredPosition; _marks[i].Moved = true; }
                mark.GetWorldCorners(Corners);
                Vector2 bottom = parent.InverseTransformPoint((Corners[0] + Corners[3]) * 0.5f);
                Vector2 nativeBottom = bottom - (mark.anchoredPosition - _marks[i].Native);
                Vector2 wanted = (Vector2)parent.InverseTransformPoint(lineTop) + new Vector2(0f, MarkGap);
                Vector2 at = _marks[i].Native + (wanted - nativeBottom);
                if ((mark.anchoredPosition - at).sqrMagnitude > 0.01f) mark.anchoredPosition = at;
            }
        }

        private void RestoreMarks()
        {
            for (int i = 0; i < _marks.Length; i++)
            {
                if (!_marks[i].Moved) continue;
                if (_marks[i].Rect != null) _marks[i].Rect!.anchoredPosition = _marks[i].Native;
                _marks[i].Moved = false;
            }
        }

        /// <summary>Removes the line and gives the name its trait words back.</summary>
        internal void Remove()
        {
            if (_removed) return;
            _removed = true;
            PlainNames.Remove(_character);
            RestoreMarks();
            if (_name != null && _character != null && global::Localization.instance != null)
                _name.text = global::Localization.instance.Localize(_character.GetHoverName());
            if (_text != null) { _text.gameObject.SetActive(false); Destroy(_text.gameObject); }
            enabled = false;
            Destroy(this);
        }

        private void OnDestroy()
        {
            PlainNames.Remove(_character);
            RestoreMarks();
            if (_text != null) Destroy(_text.gameObject);
        }

        // For the test bridge and diagnostics: the line of the health bar the game shows for this creature.
        private static TraitLine? Of(Character character)
        {
            if (character == null || EnemyHud.instance == null || !EnemyHud.instance.m_huds.TryGetValue(character, out EnemyHud.HudData data)
                || data.m_gui == null) return null;
            TraitLine? line = data.m_gui.GetComponent<TraitLine>();
            return line != null && !line._removed ? line : null;
        }

        /// <summary>True when this creature's health bar shows a trait line now.</summary>
        public static bool IsActive(Character character)
        {
            TraitLine? line = Of(character);
            return line != null && line._text != null && line._text.gameObject.activeInHierarchy && line._text.text.Length > 0;
        }

        /// <summary>The line's rect, or null when the creature's health bar has none.</summary>
        public static RectTransform? LineRect(Character character) { TraitLine? line = Of(character); return line != null && line._text != null ? line._text.rectTransform : null; }

        /// <summary>The line's text, empty when there is none.</summary>
        public static string LineText(Character character) { TraitLine? line = Of(character); return line != null && line._text != null ? line._text.text : ""; }
    }
}
