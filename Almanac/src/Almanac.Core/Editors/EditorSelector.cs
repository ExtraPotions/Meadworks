// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Almanac.Attributes;

namespace ExtraPotions.Almanac.Editors
{
    /// <summary>Kit control an entry is edited with (design 4.2; AX numbers in the design).</summary>
    public enum EditorKind
    {
        Toggle, Cycle, FlagsDropdown, Dropdown, Slider, Spinner, TextField, MultilineText, KeyCapture, ShortcutCapture,
        ColorPicker, VectorRow, ListEditor, RawText, ReadOnlyText, CustomDrawer,
        /// <summary>A mod's native uGUI drawer (ConfigDrawers <c>CustomUguiDrawer</c>), built into the row with kit controls.</summary>
        UguiDrawer,
    }

    /// <summary>When an editor's value is written.</summary>
    public enum CommitRule { None, OnClick, OnChoice, OnClose, OnRelease, OnEnter, SaveButton, OnKey, OnKeyRelease, ApplyButton }

    /// <summary>How values convert to and from text: the mod's <c>ObjToStr</c>/<c>StrToObj</c>, BepInEx's <c>TomlTypeConverter</c>, or none.</summary>
    public enum CodecChoice { None, TomlConverter, ModDelegates }

    public sealed class EditorChoice
    {
        internal EditorChoice(EditorKind kind, CommitRule commit)
        {
            Kind = kind;
            Commit = commit;
        }

        public EditorKind Kind { get; }
        public CommitRule Commit { get; }
        /// <summary>Numeric fields in a vector row (2 to 4).</summary>
        public int Fields { get; internal set; }
        public bool Integral { get; internal set; }
        public bool Percent { get; internal set; }
        public double? Step { get; internal set; }
        public string? Unit { get; internal set; }

        public override string ToString() => Kind + " (" + Commit + ")";
    }

    /// <summary>The editor selection table (design 4.2).</summary>
    public static class EditorSelector
    {
        /// <summary>Strings longer than this open the multi-line editor.</summary>
        public const int MultilineThreshold = 120;

        public static EditorChoice Select(SettingShape shape, EntryAttributes attrs, string? currentText)
        {
            var hotkeyKind = shape.Kind == SettingKind.KeyCode || shape.Kind == SettingKind.KeyboardShortcut;
            if (attrs.HasCustomUguiDrawer) return new EditorChoice(EditorKind.UguiDrawer, CommitRule.None);
            if (attrs.HasCustomDrawer || (hotkeyKind && attrs.HasCustomHotkeyDrawer))
                return new EditorChoice(EditorKind.CustomDrawer, CommitRule.None);
            return Fallback(shape, attrs, currentText);
        }

        /// <summary>The typed editor, ignoring custom drawers (used after a custom drawer failed twice).</summary>
        public static EditorChoice Fallback(SettingShape shape, EntryAttributes attrs, string? currentText)
        {
            if (shape.Kind == SettingKind.Bool) return new EditorChoice(EditorKind.Toggle, CommitRule.OnClick);
            if (shape.AcceptableList != null) return new EditorChoice(EditorKind.Dropdown, CommitRule.OnChoice);
            switch (shape.Kind)
            {
                case SettingKind.Enum:
                    return shape.IsFlags ? new EditorChoice(EditorKind.FlagsDropdown, CommitRule.OnClose) : new EditorChoice(EditorKind.Cycle, CommitRule.OnChoice);
                case SettingKind.Integer:
                case SettingKind.Floating:
                    return Numeric(shape, attrs);
                case SettingKind.String:
                    if (!string.IsNullOrEmpty(attrs.ListSeparator)) return new EditorChoice(EditorKind.ListEditor, CommitRule.ApplyButton);
                    if (attrs.Multiline || (currentText != null && (currentText.IndexOf('\n') >= 0 || currentText.Length > MultilineThreshold)))
                        return new EditorChoice(EditorKind.MultilineText, CommitRule.SaveButton);
                    return new EditorChoice(EditorKind.TextField, CommitRule.OnEnter);
                case SettingKind.KeyCode: return new EditorChoice(EditorKind.KeyCapture, CommitRule.OnKey);
                case SettingKind.KeyboardShortcut: return new EditorChoice(EditorKind.ShortcutCapture, CommitRule.OnKeyRelease);
                case SettingKind.Color: return new EditorChoice(EditorKind.ColorPicker, CommitRule.OnClose);
                case SettingKind.Vector2: return Vector(2);
                case SettingKind.Vector3: return Vector(3);
                case SettingKind.Vector4:
                case SettingKind.Quaternion: return Vector(4);
                case SettingKind.List:
                    return shape.HasConverter ? new EditorChoice(EditorKind.ListEditor, CommitRule.ApplyButton) : ReadOnly();
                default:
                    return Codec(shape, attrs) != CodecChoice.None ? new EditorChoice(EditorKind.RawText, CommitRule.OnEnter) : ReadOnly();
            }
        }

        /// <summary>"Edit as text" is offered for every editable entry with a codec.</summary>
        public static bool CanEditAsText(SettingShape shape, EntryAttributes attrs)
            => Codec(shape, attrs) != CodecChoice.None && Fallback(shape, attrs, null).Kind != EditorKind.ReadOnlyText;

        /// <summary>Codec precedence: both mod delegates, then the BepInEx converter, else none.</summary>
        public static CodecChoice Codec(SettingShape shape, EntryAttributes attrs)
        {
            if (attrs.HasCodecDelegates) return CodecChoice.ModDelegates;
            return shape.HasConverter ? CodecChoice.TomlConverter : CodecChoice.None;
        }

        private static EditorChoice Numeric(SettingShape shape, EntryAttributes attrs)
        {
            var integral = shape.Kind == SettingKind.Integer;
            EditorChoice choice;
            if (shape.HasRange)
            {
                choice = new EditorChoice(EditorKind.Slider, CommitRule.OnRelease)
                {
                    Step = attrs.Step ?? (integral ? 1 : (shape.RangeMax - shape.RangeMin) / 100.0),
                    Percent = attrs.ShowRangeAsPercent == true,
                };
            }
            else
            {
                choice = new EditorChoice(EditorKind.Spinner, CommitRule.OnEnter) { Step = attrs.Step ?? (integral ? 1 : (double?)null) };
            }
            choice.Integral = integral;
            choice.Unit = attrs.Unit;
            return choice;
        }

        private static EditorChoice Vector(int fields) => new EditorChoice(EditorKind.VectorRow, CommitRule.OnEnter) { Fields = fields };

        private static EditorChoice ReadOnly() => new EditorChoice(EditorKind.ReadOnlyText, CommitRule.None);
    }
}
