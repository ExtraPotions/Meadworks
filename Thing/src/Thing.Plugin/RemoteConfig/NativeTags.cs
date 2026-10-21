// Meadworks-owned reuse: Almanac 64a81614deb039db71d88ff70803ee648d3317dd src/Almanac.Plugin/Editing/NativeTags.cs; namespace adaptation only.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
using ExtraPotions.Thing.ConfigEditing.Attributes;

namespace ExtraPotions.Thing.Runtime.ConfigEditing
{
    public sealed class NativeTags
    {
        internal NativeTags(IReadOnlyList<TagObject> tags,IReadOnlyList<string> errors){Tags=tags;Errors=errors;Attributes=AttributeReader.Read(tags);}
        public IReadOnlyList<TagObject> Tags { get; }
        public IReadOnlyList<string> Errors { get; }
        public EntryAttributes Attributes { get; }
        public object? First(string member)
        {
            foreach(var tag in Tags)if(tag.ShortName==AttributeReader.ManagerTag && tag.TryGet(member,out var value) && value!=null)return value;
            return null;
        }
        /// <summary>Reads public convention members without changing a tag, including shared ConfigDescription.Empty tags.</summary>
        public static NativeTags Read(IEnumerable<object>? objects)
        {
            var tags=new List<TagObject>();var errors=new List<string>();
            foreach(var value in objects??Array.Empty<object>())
            {
                if(value==null)continue;
                var type=value.GetType();var bag=new Dictionary<string,object?>(StringComparer.Ordinal);
                foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public))
                    try{if(!bag.ContainsKey(field.Name))bag.Add(field.Name,field.GetValue(value));}
                    catch(Exception error){errors.Add(type.FullName+"."+field.Name+": "+error.GetBaseException().Message);}
                foreach(var property in type.GetProperties(BindingFlags.Instance|BindingFlags.Public))
                {
                    if(property.GetIndexParameters().Length!=0 || property.GetGetMethod()==null || bag.ContainsKey(property.Name))continue;
                    try{bag.Add(property.Name,property.GetValue(value));}
                    catch(Exception error){errors.Add(type.FullName+"."+property.Name+": "+error.GetBaseException().Message);}
                }
                tags.Add(new TagObject(type.FullName??type.Name,bag));
            }
            // An unreadable convention could contain a lock. It must never make an entry editable.
            if(errors.Count!=0)tags.Add(new TagObject("Almanac.UnreadableTag",new Dictionary<string,object?>{{"ReadOnly",true}}));
            return new NativeTags(tags.AsReadOnly(),errors.AsReadOnly());
        }
    }
}
