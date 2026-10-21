// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Attributes;
using ExtraPotions.Almanac.Editors;
using ExtraPotions.Almanac.Values;
using UnityEngine;

namespace ExtraPotions.Almanac.Runtime.Editing
{
    public sealed class NativeParsedValue
    {
        internal NativeParsedValue(object? value,string serialized,bool clamped,string? error=null){Value=value;Serialized=serialized;WasClamped=clamped;Error=error;}
        public object? Value { get; }
        public string Serialized { get; }
        public bool WasClamped { get; }
        public string? Error { get; }
        public bool Valid=>Error==null;
    }
    /// <summary>Live BepInEx type/constraint/codec adapter; editor choice and validation use the existing portable model.</summary>
    public sealed class NativeEntry
    {
        public NativeEntry(ConfigEntryBase entry)
        {
            Entry=entry??throw new ArgumentNullException(nameof(entry));
            Tags=NativeTags.Read(entry.Description.Tags);Shape=ShapeOf(entry);
        }
        public ConfigEntryBase Entry { get; }
        public NativeTags Tags { get; }
        public EntryAttributes Attributes=>Tags.Attributes;
        public SettingShape Shape { get; }
        public EditorChoice Editor=>EditorSelector.Select(Shape,Attributes,Text());
        public Delegate? CustomDrawer=>Tags.First(Shape.Kind==SettingKind.KeyCode||Shape.Kind==SettingKind.KeyboardShortcut?"CustomHotkeyDrawer":"CustomDrawer") as Delegate
            ??Tags.First("CustomDrawer") as Delegate;
        /// <summary>The mod's ConfigDrawers native uGUI drawer, under any of the names ConfigDrawers reads.</summary>
        public Delegate? UguiDrawer=>AttributeReader.UguiDrawerMembers.Select(name=>Tags.First(name) as Delegate).FirstOrDefault(d=>d!=null);
        // Serialize the actual live value. Sync systems may patch GetSerializedValue to return the
        // player's own disk value, which is not the current displayed/server-provided value.
        public string RawSerialized()=>TomlTypeConverter.ConvertToString(Entry.BoxedValue,Entry.SettingType);
        public string Text()
        {
            if(EditorSelector.Codec(Shape,Attributes)==CodecChoice.ModDelegates)
                return (string?)((Delegate)Attributes.ObjToStr!).DynamicInvoke(Entry.BoxedValue)??"";
            return Shape.HasConverter?RawSerialized():Entry.BoxedValue?.ToString()??"";
        }
        public NativeParsedValue Parse(string text)
        {
            try
            {
                var codec=EditorSelector.Codec(Shape,Attributes);
                if(codec==CodecChoice.None)return new NativeParsedValue(null,"",false,"Almanac cannot edit this type.");
                object? value;bool clamped=false;
                if(codec==CodecChoice.ModDelegates)value=((Delegate)Attributes.StrToObj!).DynamicInvoke(text);
                else
                {
                    // Keep the portable numeric/key/enum validity rules, but use the actual native serializer
                    // for string escaping, registered converters, colors and Unity structs.
                    var kind=Shape.Kind;
                    if(kind==SettingKind.Bool||kind==SettingKind.Integer||kind==SettingKind.Floating||kind==SettingKind.Enum||kind==SettingKind.KeyCode||kind==SettingKind.KeyboardShortcut)
                    {
                        var valid=ValueCodec.Parse(Shape,text);
                        if(!valid.Ok)return new NativeParsedValue(null,"",false,valid.Error);
                        text=valid.Text;clamped=valid.WasClamped;
                    }
                    value=TomlTypeConverter.ConvertToValue(text,Entry.SettingType);
                }
                if(value==null ? Entry.SettingType.IsValueType : !Entry.SettingType.IsInstanceOfType(value))
                    return new NativeParsedValue(null,"",false,"The converter returned a different setting type.");
                if(!Finite(value))return new NativeParsedValue(null,"",false,"Enter a number in every field.");
                var constraint=Entry.Description.AcceptableValues;
                if(constraint!=null)
                {
                    if(Shape.AcceptableList!=null&&!constraint.IsValid(value))return new NativeParsedValue(null,"",false,"Choose one of the allowed values.");
                    var before=TomlTypeConverter.ConvertToString(value,Entry.SettingType);
                    value=constraint.Clamp(value);
                    if(!constraint.IsValid(value)||!Finite(value))return new NativeParsedValue(null,"",false,"The value does not satisfy the setting constraint.");
                    clamped|=!string.Equals(before,TomlTypeConverter.ConvertToString(value,Entry.SettingType),StringComparison.Ordinal);
                }
                return new NativeParsedValue(value,TomlTypeConverter.ConvertToString(value,Entry.SettingType),clamped);
            }
            catch(Exception error){return new NativeParsedValue(null,"",false,error.GetBaseException().Message);}
        }
        static bool Finite(object? value)
        {
            bool Number(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
            switch(value)
            {
                case float f:return Number(f);case double d:return Number(d);
                case Vector2 v:return Number(v.x)&&Number(v.y);
                case Vector3 v:return Number(v.x)&&Number(v.y)&&Number(v.z);
                case Vector4 v:return Number(v.x)&&Number(v.y)&&Number(v.z)&&Number(v.w);
                case Quaternion q:return Number(q.x)&&Number(q.y)&&Number(q.z)&&Number(q.w);
                case Color c:return Number(c.r)&&Number(c.g)&&Number(c.b)&&Number(c.a);
                default:return true;
            }
        }
        static bool Convention(Type type,string name)
        {
            for(var current=type;current!=null;current=current.BaseType)
                if(current.IsGenericType&&current.GetGenericTypeDefinition().FullName=="BepInEx.Configuration."+name+"`1")return true;
            return false;
        }
        static SettingShape ShapeOf(ConfigEntryBase entry)
        {
            var type=entry.SettingType;string name=type.FullName??type.Name;
            var kind=SettingShape.KindOf(name);
            var shape=type.IsEnum&&kind!=SettingKind.KeyCode?SettingShape.Enum(name,Enum.GetNames(type),type.IsDefined(typeof(FlagsAttribute),false))
                :SettingShape.Of(kind,name,TomlTypeConverter.CanConvert(type));
            if(kind==SettingKind.KeyCode||kind==SettingKind.KeyboardShortcut)shape=shape.WithKeyNames(Enum.GetNames(typeof(KeyCode)));
            var constraint=entry.Description.AcceptableValues;
            if(constraint==null)return shape;
            if(Convention(constraint.GetType(),"AcceptableValueRange"))
            {
                var typeInfo=constraint.GetType();
                var min=Convert.ToDouble(typeInfo.GetProperty("MinValue")!.GetValue(constraint),CultureInfo.InvariantCulture);
                var max=Convert.ToDouble(typeInfo.GetProperty("MaxValue")!.GetValue(constraint),CultureInfo.InvariantCulture);
                shape=shape.WithRange(min,max);
            }
            if(Convention(constraint.GetType(),"AcceptableValueList"))
                shape=shape.WithList(((IEnumerable)constraint.GetType().GetProperty("AcceptableValues")!.GetValue(constraint)!).Cast<object>().Select(value=>TomlTypeConverter.ConvertToString(value,type)));
            return shape;
        }
    }
}
