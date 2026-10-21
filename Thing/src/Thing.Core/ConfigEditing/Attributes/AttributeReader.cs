// Meadworks-owned reuse: Almanac 64a81614deb039db71d88ff70803ee648d3317dd src/Almanac.Core/Attributes/AttributeReader.cs; namespace adaptation only.
// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Thing.ConfigEditing.Attributes
{
    /// <summary>
    /// One object from <c>ConfigDescription.Tags</c> as a name-keyed bag of its public fields and properties.
    /// The plugin adapter fills it by reflection; Almanac references no manager assembly and copies no manager code.
    /// </summary>
    public sealed class TagObject
    {
        public TagObject(string typeName, IReadOnlyDictionary<string, object?> members)
        {
            TypeName = typeName ?? "";
            Members = members ?? new Dictionary<string, object?>();
            ShortName = ShortTypeName(TypeName);
        }

        /// <summary>Full type name as reflection reports it (namespace and nesting included).</summary>
        public string TypeName { get; }

        /// <summary>Name without namespace or declaring types.</summary>
        public string ShortName { get; }

        public IReadOnlyDictionary<string, object?> Members { get; }

        public bool TryGet(string member, out object? value) => Members.TryGetValue(member, out value);

        public static string ShortTypeName(string typeName)
        {
            var name = typeName ?? "";
            var generic = name.IndexOf('`');
            if (generic >= 0) name = name.Substring(0, generic);
            var cut = Math.Max(name.LastIndexOf('.'), name.LastIndexOf('+'));
            return cut >= 0 ? name.Substring(cut + 1) : name;
        }
    }

    /// <summary>Everything Almanac reads from an entry's tags (design 4.1, AlmanacHints, Charter and generic read-only markers).</summary>
    public sealed class EntryAttributes
    {
        // BepInEx.ConfigurationManager convention
        public int? Order { get; internal set; }
        public bool? Browsable { get; internal set; }
        public string? Category { get; internal set; }
        public bool? IsAdvanced { get; internal set; }
        public bool? ReadOnly { get; internal set; }
        public string? DispName { get; internal set; }
        public string? Description { get; internal set; }
        public bool HasDefaultOverride { get; internal set; }
        public object? DefaultValue { get; internal set; }
        public bool? HideDefaultButton { get; internal set; }
        public bool? HideSettingName { get; internal set; }
        public bool? ShowRangeAsPercent { get; internal set; }
        public bool HasObjToStr { get; internal set; }
        public bool HasStrToObj { get; internal set; }
        public object? ObjToStr { get; internal set; }
        public object? StrToObj { get; internal set; }
        public bool HasCustomDrawer { get; internal set; }
        public bool HasCustomHotkeyDrawer { get; internal set; }

        // Jotunn extension of the same tag type
        public bool? IsAdminOnly { get; internal set; }
        public bool? IsUnlocked { get; internal set; }
        public bool HasEntryColor { get; internal set; }
        public bool HasDescriptionColor { get; internal set; }
        public bool IsJotunnShaped => IsAdminOnly.HasValue;

        // AlmanacHints
        public bool RequiresRestart { get; internal set; }
        public string? ListSeparator { get; internal set; }
        public bool Multiline { get; internal set; }
        public string? Unit { get; internal set; }
        public double? Step { get; internal set; }

        // Lock markers outside ConfigurationManagerAttributes
        /// <summary>A PackPanel Charter <c>ReadOnlyTag</c> with <c>ReadOnly == true</c>.</summary>
        public bool CharterReadOnly { get; internal set; }
        /// <summary>Any other tag object with a bool <c>ReadOnly == true</c> (reason ModReadOnly).</summary>
        public bool GenericReadOnly { get; internal set; }

        public bool HasCodecDelegates => HasObjToStr && HasStrToObj;
    }

    /// <summary>Reads tag objects by type short name and member name. Never mutates the bags.</summary>
    public static class AttributeReader
    {
        public const string ManagerTag = "ConfigurationManagerAttributes";
        public const string HintsTag = "AlmanacHints";
        public const string CharterTag = "ReadOnlyTag";

        public static EntryAttributes Read(IEnumerable<TagObject>? tags)
        {
            var a = new EntryAttributes();
            if (tags == null) return a;
            foreach (var tag in tags)
            {
                if (tag == null) continue;
                switch (tag.ShortName)
                {
                    case ManagerTag: ReadManager(tag, a); break;
                    case HintsTag: ReadHints(tag, a); break;
                    case CharterTag:
                        if (Bool(tag, "ReadOnly") == true) a.CharterReadOnly = true;
                        break;
                    default:
                        if (Bool(tag, "ReadOnly") == true) a.GenericReadOnly = true;
                        break;
                }
            }
            return a;
        }

        private static void ReadManager(TagObject tag, EntryAttributes a)
        {
            a.Order ??= Int(tag, "Order");
            a.Browsable ??= Bool(tag, "Browsable");
            a.Category ??= Text(tag, "Category");
            a.IsAdvanced ??= Bool(tag, "IsAdvanced");
            a.ReadOnly ??= Bool(tag, "ReadOnly");
            a.DispName ??= Text(tag, "DispName");
            a.Description ??= Text(tag, "Description");
            if (!a.HasDefaultOverride && tag.TryGet("DefaultValue", out var defaultValue) && defaultValue != null)
            {
                a.HasDefaultOverride = true;
                a.DefaultValue = defaultValue;
            }
            a.HideDefaultButton ??= Bool(tag, "HideDefaultButton");
            a.HideSettingName ??= Bool(tag, "HideSettingName");
            a.ShowRangeAsPercent ??= Bool(tag, "ShowRangeAsPercent");
            if (!a.HasObjToStr && Present(tag, "ObjToStr", out var objToStr)) { a.HasObjToStr = true; a.ObjToStr = objToStr; }
            if (!a.HasStrToObj && Present(tag, "StrToObj", out var strToObj)) { a.HasStrToObj = true; a.StrToObj = strToObj; }
            a.HasCustomDrawer |= Present(tag, "CustomDrawer", out _);
            a.HasCustomHotkeyDrawer |= Present(tag, "CustomHotkeyDrawer", out _);
            a.IsAdminOnly ??= Bool(tag, "IsAdminOnly");
            a.IsUnlocked ??= Bool(tag, "IsUnlocked");
            a.HasEntryColor |= Present(tag, "EntryColor", out _);
            a.HasDescriptionColor |= Present(tag, "DescriptionColor", out _);
        }

        private static void ReadHints(TagObject tag, EntryAttributes a)
        {
            if (Bool(tag, "RequiresRestart") == true) a.RequiresRestart = true;
            if (Bool(tag, "Multiline") == true) a.Multiline = true;
            a.ListSeparator ??= Text(tag, "ListSeparator");
            a.Unit ??= Text(tag, "Unit");
            a.Step ??= Number(tag, "Step");
        }

        private static bool Present(TagObject tag, string name, out object? value) => tag.TryGet(name, out value) && value != null;

        private static bool? Bool(TagObject tag, string name) => tag.TryGet(name, out var v) && v is bool b ? b : (bool?)null;

        private static string? Text(TagObject tag, string name) => tag.TryGet(name, out var v) && v is string s ? s : null;

        private static int? Int(TagObject tag, string name)
        {
            if (!tag.TryGet(name, out var v) || v == null) return null;
            long value;
            switch (v)
            {
                case int i: return i;
                case short s: value = s; break;
                case sbyte sb: value = sb; break;
                case byte b: value = b; break;
                case ushort us: value = us; break;
                case uint ui: value = ui; break;
                case long l: value = l; break;
                default: return null;
            }
            return value < int.MinValue || value > int.MaxValue ? (int?)null : (int)value;
        }

        private static double? Number(TagObject tag, string name)
        {
            if (!tag.TryGet(name, out var v) || v == null) return null;
            switch (v)
            {
                case double d: return d;
                case float f: return f;
                case decimal m: return (double)m;
                case int i: return i;
                case long l: return l;
                case short s: return s;
                default: return null;
            }
        }
    }
}
