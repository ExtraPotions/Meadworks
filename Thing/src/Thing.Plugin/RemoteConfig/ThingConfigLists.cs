// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-owned reuse: Almanac 64a81614deb039db71d88ff70803ee648d3317dd SettingsWindow.Editors.ListEditor native element conversion/array/IList reconstruction, bounded for the existing remote owner route.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Thing.ConfigEditing.Editors;
using ExtraPotions.Thing.ConfigEditing.Values;
using ExtraPotions.Thing.RemoteConfig;
using ExtraPotions.Thing.Runtime.ConfigEditing;
using UnityEngine;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingConfigLists
    {
        static Type Element(NativeEntry native)
        {
            var type = native.Entry.SettingType;
            if (native.Shape.Kind != SettingKind.List || !native.Shape.HasConverter || native.Attributes.HasCodecDelegates) throw new NotSupportedException();
            var element = type.IsArray && type.GetArrayRank() == 1 ? type.GetElementType() :
                !type.IsAbstract && typeof(IList).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) != null ? type.GetGenericArguments().FirstOrDefault() : null;
            if (element == null || !TomlTypeConverter.CanConvert(element)) throw new NotSupportedException();
            return element;
        }
        static SettingShape Shape(Type element)
        {
            var name = element.FullName ?? element.Name; var kind = SettingShape.KindOf(name);
            var shape = element.IsEnum && kind != SettingKind.KeyCode ? SettingShape.Enum(name, Enum.GetNames(element), element.IsDefined(typeof(FlagsAttribute), false))
                : SettingShape.Of(kind, name, TomlTypeConverter.CanConvert(element));
            return kind == SettingKind.KeyCode || kind == SettingKind.KeyboardShortcut ? shape.WithKeyNames(Enum.GetNames(typeof(KeyCode))) : shape;
        }
        internal static IReadOnlyList<string> Read(string file, string entry, string version, NativeEntry native, bool editable)
        {
            var element = Element(native);
            var values = (native.Entry.BoxedValue as IEnumerable)?.Cast<object>().Take(ConfigListDocument.MaxItems + 1).Select(value => TomlTypeConverter.ConvertToString(value, element)).ToArray() ?? Array.Empty<string>();
            return new ConfigListDocument { Element = new ConfigEntryDocument { File = file, Entry = entry, Version = version, Shape = Shape(element), Editable = editable,
                Section = native.Entry.Definition.Section, Key = native.Entry.Definition.Key, Attributes = native.Attributes }, Values = values }.Encode();
        }
        internal static string Serialize(NativeEntry native, IReadOnlyList<string> values)
        {
            var element = Element(native); var shape = Shape(element); ConfigListDocument.PackValues(values);
            var converted = values.Select(value =>
            {
                var parsed = ValueCodec.Parse(shape, value);
                if (!parsed.Ok && !parsed.NeedsModConverter && !(shape.Kind == SettingKind.Enum && shape.IsFlags && value == "0")) throw new FormatException();
                return TomlTypeConverter.ConvertToValue(value, element);
            }).ToArray();
            object result;
            if (native.Entry.SettingType.IsArray)
            {
                var array = Array.CreateInstance(element, converted.Length);
                for (var i = 0; i < converted.Length; i++) array.SetValue(converted[i], i); result = array;
            }
            else
            {
                var list = (IList)Activator.CreateInstance(native.Entry.SettingType)!;
                foreach (var item in converted) list.Add(item); result = list;
            }
            var serialized = TomlTypeConverter.ConvertToString(result, native.Entry.SettingType);
            if (YamlTransfer.Bytes(serialized).Length > 4096) throw new FormatException(); return serialized;
        }
    }
}
