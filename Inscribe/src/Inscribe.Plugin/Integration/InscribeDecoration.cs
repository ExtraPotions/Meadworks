// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Display;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;
using ExtraPotions.Inscribe.Adopted.Api;
using ExtraPotions.Inscribe.Integration;
using ExtraPotions.Inscribe.Interop;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Inscribe.Runtime
{
    internal static class InscribeDecoration
    {
        internal static void Publish(ProductRegistration product)
        {
            product.AddCapability<Action<object,object>>("meadworks.items.rarity_background",new System.Version(1,0),(surface,value)=>{
                if(!Plugin.AllowsUi||!(surface is UnityEngine.GameObject root))return;
                var icon=root.GetComponent<UnityEngine.UI.Image>();if(icon==null)return;
                if(Showing&&value is ItemDrop.ItemData item)ExtraPotions.Inscribe.Adopted.Display.Backdrops.IconBackdrop.Set(icon,item);
                else ExtraPotions.Inscribe.Adopted.Display.Backdrops.IconBackdrop.Hide(icon);
            });
            product.AddCapability(DecorationV1.Capability, DecorationV1.Version,
                Tuple.Create<Func<object, string>, Func<object, string>, Func<object, Tuple<string, string, float, string, string, string>[]>>(
                    value => value is ItemDrop.ItemData item && Showing ? InscribeExtraCommands.DecorateName(item, ApiItems.DecoratedName(item)) ?? "" : "",
                    value => value is ItemDrop.ItemData item && Showing && ItemState.Read(item).IsMagic ? ApiItems.RarityColor(item)! : null!, Rows));
        }
        private static bool Showing => InscribeModules.Active(InscribeModule.Display);
        private static Tuple<string, string, float, string, string, string>[] Rows(object value)
        {
            if (!Showing || !(value is ItemDrop.ItemData item)) return Array.Empty<Tuple<string, string, float, string, string, string>>();
            var result = new List<Tuple<string, string, float, string, string, string>>();
            var state = ItemState.Read(item);
            if (state.Rarity != null)
            {
                string name = DisplayWords.Name(state.Rarity.Name, state.Rarity.Id);
                result.Add(Tuple.Create("inscribe.rarity", "Rarity", (float)state.Rarity.Index, "flat", "Higher", name));
            }
            var sums = new Dictionary<string, float>(); var definitions = new Dictionary<string, AffixDef>();
            for (int index = 0; index < state.AffixCount; index++)
            {
                if (!state.IsActiveAt(index) || state.DefinitionAt(index) is not AffixDef definition) continue;
                var roll = state.Affixes[index]; sums.TryGetValue(roll.Id, out float sum); sums[roll.Id] = sum + roll.Value; definitions[roll.Id] = definition;
            }
            foreach (var pair in sums)
            {
                var definition = definitions[pair.Key]; string unit = definition.Value == AffixValueType.Percent ? "percent" : definition.Value == AffixValueType.Flag ? "flag" : "flat";
                result.Add(Tuple.Create("inscribe.insc." + pair.Key, DisplayWords.Name(definition.Name, definition.Id), pair.Value, unit, unit == "flag" ? "None" : "Higher", AffixLines.Sentence(pair.Key, pair.Value, definition)));
            }
            return result.ToArray();
        }
    }
}
