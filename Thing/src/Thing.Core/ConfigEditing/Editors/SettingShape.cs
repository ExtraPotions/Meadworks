// Meadworks-owned reuse: Almanac 64a81614deb039db71d88ff70803ee648d3317dd src/Almanac.Core/Editors/SettingShape.cs; namespace adaptation only.
// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Thing.ConfigEditing.Editors
{
    public enum SettingKind { Bool, Enum, Integer, Floating, String, KeyCode, KeyboardShortcut, Color, Vector2, Vector3, Vector4, Quaternion, List, Other }

    /// <summary>
    /// A setting's type and constraint as the plugin adapter reports them: kind, BepInEx type name, enum names,
    /// <c>AcceptableValueRange</c> or <c>AcceptableValueList</c> (as BepInEx text), known key names and whether a
    /// <c>TomlTypeConverter</c> exists. Immutable; the <c>With</c> methods return copies.
    /// </summary>
    public sealed class SettingShape
    {
        private static readonly string[] NoNames = new string[0];

        private SettingShape(SettingKind kind, string typeName, bool hasConverter)
        {
            Kind = kind;
            TypeName = typeName ?? "";
            HasConverter = hasConverter;
        }

        public SettingKind Kind { get; }
        public string TypeName { get; }
        public bool HasConverter { get; }
        public bool IsFlags { get; private set; }
        public IReadOnlyList<string> EnumNames { get; private set; } = NoNames;
        public bool HasRange { get; private set; }
        public double RangeMin { get; private set; }
        public double RangeMax { get; private set; }
        public IReadOnlyList<string>? AcceptableList { get; private set; }
        /// <summary>Known key names (Unity <c>KeyCode</c> names) for key validation; empty when unknown.</summary>
        public IReadOnlyList<string> KeyNames { get; private set; } = NoNames;

        /// <summary>BepInEx type name short form without namespace (Int32, Single, ...).</summary>
        public string ShortTypeName => Attributes.TagObject.ShortTypeName(TypeName);

        public static SettingShape Of(SettingKind kind, string typeName, bool hasConverter = true) => new SettingShape(kind, typeName, hasConverter);
        public static SettingShape Bool() => Of(SettingKind.Bool, "System.Boolean");
        public static SettingShape String() => Of(SettingKind.String, "System.String");
        public static SettingShape Integer(string typeName) => Of(SettingKind.Integer, typeName);
        public static SettingShape Floating(string typeName) => Of(SettingKind.Floating, typeName);

        public static SettingShape Enum(string typeName, IEnumerable<string> names, bool flags = false)
        {
            var shape = Of(SettingKind.Enum, typeName);
            shape.EnumNames = names.ToArray();
            shape.IsFlags = flags;
            return shape;
        }

        public SettingShape WithRange(double min, double max)
        {
            if (min > max) throw new ArgumentException("Range minimum is above maximum");
            var copy = Copy();
            copy.HasRange = true;
            copy.RangeMin = min;
            copy.RangeMax = max;
            return copy;
        }

        public SettingShape WithList(IEnumerable<string> values)
        {
            var copy = Copy();
            copy.AcceptableList = values.ToArray();
            return copy;
        }

        public SettingShape WithKeyNames(IEnumerable<string> names)
        {
            var copy = Copy();
            copy.KeyNames = names.ToArray();
            return copy;
        }

        /// <summary>Kind from a BepInEx setting type name. Enums cannot be recognized by name; the adapter uses <see cref="Enum"/>.</summary>
        public static SettingKind KindOf(string typeName)
        {
            var name = typeName ?? "";
            if (name.EndsWith("[]", StringComparison.Ordinal)) return SettingKind.List;
            var generic = name.IndexOf('`');
            if (generic >= 0)
            {
                var head = Attributes.TagObject.ShortTypeName(name.Substring(0, generic));
                return head == "List" || head == "IList" || head == "HashSet" ? SettingKind.List : SettingKind.Other;
            }
            switch (Attributes.TagObject.ShortTypeName(name))
            {
                case "Boolean": return SettingKind.Bool;
                case "SByte": case "Byte": case "Int16": case "UInt16": case "Int32": case "UInt32": case "Int64": case "UInt64": return SettingKind.Integer;
                case "Single": case "Double": case "Decimal": return SettingKind.Floating;
                case "String": return SettingKind.String;
                case "KeyCode": return SettingKind.KeyCode;
                case "KeyboardShortcut": return SettingKind.KeyboardShortcut;
                case "Color": return SettingKind.Color;
                case "Vector2": return SettingKind.Vector2;
                case "Vector3": return SettingKind.Vector3;
                case "Vector4": return SettingKind.Vector4;
                case "Quaternion": return SettingKind.Quaternion;
                default: return SettingKind.Other;
            }
        }

        private SettingShape Copy() => new SettingShape(Kind, TypeName, HasConverter)
        {
            IsFlags = IsFlags, EnumNames = EnumNames, HasRange = HasRange, RangeMin = RangeMin, RangeMax = RangeMax,
            AcceptableList = AcceptableList, KeyNames = KeyNames,
        };

        public override string ToString() => Kind + " " + TypeName;
    }
}
