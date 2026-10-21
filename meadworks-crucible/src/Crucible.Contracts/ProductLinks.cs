// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// User decision 2026-10-12 22:41: MeadWorks products reach each other only through Crucible capabilities, never through
// BepInEx plugin lookups, another product's config file or custom data keys, object names or assembly references.
// Every contract here is a BCL shape (Crucible resolves by exact Type), registered with AddCapability<T>(Id, ContractVersion, ...)
// and read on the Unity main thread. An absent, throwing or invalid provider always reads as the documented fallback.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Meadworks.Contracts {
    /// <summary>
    /// Feature ownership, <c>meadworks.owns.&lt;feature&gt;</c> version 1.0: the product that owns a feature provides
    /// <c>Func&lt;bool&gt;</c> under <see cref="Id(string)"/>, returning whether the feature is on now (its config switch).
    /// It replaces "is product X loaded" checks: consumers ask about the feature, not the product, so the absent case is
    /// the consumer's own fallback and the "installed but switched off" case is the provider's answer.
    /// One provider per feature; two providers read as absent (Crucible refuses an ambiguous resolution).
    /// </summary>
    public static class FeatureOwnerContract {
        public const string Prefix="meadworks.owns.";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public static Type DelegateType=>typeof(Func<bool>);

        /// <summary>Mantle lays out the player inventory (its <c>1. Inventory / Enabled</c>). Read by Burrow, Kindle, Keel, Wildblood.</summary>
        public const string Inventory="inventory";
        /// <summary>Burrow places its Quick stack / Store all / Top up / Sort row. Read by Mantle (its button strip).</summary>
        public const string StowRow="stow_row";
        /// <summary>Burrow's Stacks feature writes item stack sizes (its <c>4. Stacks / Enabled</c>). Read by Mantle's key ring stacks.</summary>
        public const string Stacks="stacks";
        /// <summary>Cairn computes the map exploration radius and applies Keel's sea factor itself. Read by Keel.</summary>
        public const string ExploreRadius="explore_radius";
        /// <summary>Wildblood owns elite creature facts (stars, splits, copies) that Elite Creatures Reborn would otherwise write. Read by Inscribe, Bestiary.</summary>
        public const string EliteCreatures="elite_creatures";
        /// <summary>Timber owns the debug build mode and its cheats. Read by Hex.</summary>
        public const string DebugBuild="debug_build";

        /// <summary>The capability id for a feature name (lowercase letters, digits and underscores).</summary>
        public static string Id(string feature){
            if(!ValidFeature(feature))throw new ArgumentException("Expected a lowercase feature name.",nameof(feature));
            return Prefix+feature;
        }
        public static bool ValidFeature(string? feature){
            if(string.IsNullOrEmpty(feature)||feature!.Length>64||feature[0]<'a'||feature[0]>'z')return false;
            foreach(char c in feature)if(!(c>='a'&&c<='z'||c>='0'&&c<='9'||c=='_'))return false;
            return true;
        }

        /// <summary>Calls the provider; an absent or throwing provider gives false (the caller's fallback applies).</summary>
        public static bool TryRead(Func<bool>? provider,out bool on){
            on=false;
            if(provider==null)return false;
            try{on=provider();return true;}catch{on=false;return false;}
        }
    }

    /// <summary>
    /// Favorite items, <c>meadworks.items.favorites</c> version 1.0 (already provided by Burrow since 0.3.x): the
    /// favorite item names (prefab names or <c>$item_</c> tokens) of the local player. Read by Kindle's salvage guard.
    /// Without a provider nothing is a favorite; a throwing provider must be treated as "everything is protected" by a
    /// consumer about to destroy items (<see cref="TryRead"/> returns false then).
    /// </summary>
    public static class FavouritesContract {
        public const string Id="meadworks.items.favorites";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public static Type DelegateType=>typeof(Func<IReadOnlyCollection<string>>);

        public static bool TryRead(Func<IReadOnlyCollection<string>>? provider,out IReadOnlyCollection<string> names){
            names=Array.Empty<string>();
            if(provider==null)return false;
            try{var raw=provider();if(raw==null)return false;names=raw;return true;}catch{return false;}
        }
        /// <summary>True when either name of the item is listed.</summary>
        public static bool Contains(IReadOnlyCollection<string> names,string? prefabName,string? sharedName){
            if(names==null)return false;
            foreach(var name in names)if(name!=null&&(name==prefabName||name==sharedName))return true;
            return false;
        }
    }

    /// <summary>
    /// The inventory layout, <c>meadworks.inventory.layout</c> version 1.0: the product laying out the player inventory
    /// (Mantle) says whether it does and what the main grid is, replacing reads of its config and of the
    /// <c>Mantle.mainGrid</c> custom data key. Shape <c>Func&lt;Tuple&lt;bool,int,int,int&gt;&gt;</c>: lays out, main grid
    /// width, main rows, blocked cells at the end of the last main row. Read by Burrow and Kindle.
    /// </summary>
    public static class InventoryLayoutContract {
        public const string Id="meadworks.inventory.layout";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public const int MaximumWidth=64,MaximumRows=128;
        public static Type DelegateType=>typeof(Func<Tuple<bool,int,int,int>>);

        public static Tuple<bool,int,int,int> Pack(InventoryLayout layout)=>Tuple.Create(layout.LaysOut,layout.Width,layout.Rows,layout.Blocked);

        /// <summary>A grid is valid when the width is 1..64, rows 0..128, blocked under the width and zero without rows. Not laying out needs no grid.</summary>
        public static bool Valid(bool laysOut,int width,int rows,int blocked)=>!laysOut||
            width>=1&&width<=MaximumWidth&&rows>=0&&rows<=MaximumRows&&blocked>=0&&blocked<width&&(rows>0||blocked==0);

        public static bool TryRead(Func<Tuple<bool,int,int,int>>? provider,out InventoryLayout layout){
            layout=InventoryLayout.None;
            if(provider==null)return false;
            Tuple<bool,int,int,int> raw;
            try{raw=provider();}catch{return false;}
            if(raw==null||!Valid(raw.Item1,raw.Item2,raw.Item3,raw.Item4))return false;
            layout=raw.Item1?new InventoryLayout(true,raw.Item2,raw.Item3,raw.Item4):InventoryLayout.None;
            return true;
        }
    }

    /// <summary>One reading of <see cref="InventoryLayoutContract"/>.</summary>
    public readonly struct InventoryLayout:IEquatable<InventoryLayout> {
        public static InventoryLayout None=>default;
        public bool LaysOut{get;}
        public int Width{get;}
        public int Rows{get;}
        public int Blocked{get;}
        public InventoryLayout(bool laysOut,int width,int rows,int blocked){
            if(!InventoryLayoutContract.Valid(laysOut,width,rows,blocked))throw new ArgumentOutOfRangeException(nameof(width));
            LaysOut=laysOut;Width=laysOut?width:0;Rows=laysOut?rows:0;Blocked=laysOut?blocked:0;
        }
        /// <summary>The cell is in the main grid (not a blocked cell, not an equipment or hotbar row the layout owns).</summary>
        public bool InMain(int x,int y)=>LaysOut&&x>=0&&y>=0&&x<Width&&y<Rows&&!(y==Rows-1&&x>=Width-Blocked);
        public bool Equals(InventoryLayout other)=>LaysOut==other.LaysOut&&Width==other.Width&&Rows==other.Rows&&Blocked==other.Blocked;
        public override bool Equals(object? obj)=>obj is InventoryLayout other&&Equals(other);
        public override int GetHashCode()=>(((LaysOut?1:0)*397^Width)*397^Rows)*397^Blocked;
    }

    /// <summary>
    /// The key ring, <c>meadworks.inventory.keyring</c> version 1.0: the inventory owner's key stack size and key item
    /// names (Mantle's <c>Key Stack</c> and <c>Key Items</c>), replacing reads of its config. Shape
    /// <c>Func&lt;Tuple&lt;int,IReadOnlyCollection&lt;string&gt;&gt;&gt;</c>. A stack of 0 means the key rule is off.
    /// Read by Burrow's Stacks. Without a provider no key rule applies.
    /// </summary>
    public static class KeyRingContract {
        public const string Id="meadworks.inventory.keyring";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public const int MaximumStack=100000;
        public static Type DelegateType=>typeof(Func<Tuple<int,IReadOnlyCollection<string>>>);

        public static bool TryRead(Func<Tuple<int,IReadOnlyCollection<string>>>? provider,out int stack,out IReadOnlyCollection<string> items){
            stack=0;items=Array.Empty<string>();
            if(provider==null)return false;
            Tuple<int,IReadOnlyCollection<string>> raw;
            try{raw=provider();}catch{return false;}
            if(raw==null||raw.Item2==null||raw.Item1<0||raw.Item1>MaximumStack)return false;
            stack=raw.Item1;items=raw.Item2;
            return true;
        }
    }

    /// <summary>
    /// HUD occupancy, <c>meadworks.hud.occupied</c> version 1.0, one provider per product (read with
    /// <c>ResolveEach</c>): the rectangle a product's HUD panel covers now, so another product's panel can stay clear of
    /// it without finding it by object name (Ember used <c>root.Find("Cairn_Hud")</c>). Shape
    /// <c>Func&lt;Tuple&lt;bool,float,float,float,float&gt;&gt;</c>: shown, left, top, width, height in the HUD root's units,
    /// origin at the root's top-left corner, y growing downward. Crucible.UI's <c>KitHudOccupancy.Measure</c> computes it.
    /// </summary>
    public static class HudOccupiedContract {
        public const string Id="meadworks.hud.occupied";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public const float MaximumExtent=100000f;
        public static Type DelegateType=>typeof(Func<Tuple<bool,float,float,float,float>>);

        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&v>=-MaximumExtent&&v<=MaximumExtent;

        /// <summary>A shown rectangle is finite with a positive size; a hidden one reads as not shown.</summary>
        public static bool TryRead(Func<Tuple<bool,float,float,float,float>>? provider,out bool shown,out float left,out float top,out float width,out float height){
            shown=false;left=top=width=height=0f;
            if(provider==null)return false;
            Tuple<bool,float,float,float,float> raw;
            try{raw=provider();}catch{return false;}
            if(raw==null)return false;
            if(!raw.Item1)return true;
            if(!Finite(raw.Item2)||!Finite(raw.Item3)||!Finite(raw.Item4)||!Finite(raw.Item5)||raw.Item4<=0f||raw.Item5<=0f)return false;
            shown=true;left=raw.Item2;top=raw.Item3;width=raw.Item4;height=raw.Item5;
            return true;
        }
    }

    /// <summary>
    /// The HUD column square, <c>meadworks.hud.column_square</c> version 1.0: the product that dresses the HUD column
    /// (Mantle) makes a square in its look, replacing Cairn's and Keel's copy of an object found by Mantle's names.
    /// Shape <c>Func&lt;object,Tuple&lt;object,object,object,object&gt;&gt;</c>: given the column (a <c>RectTransform</c>),
    /// it returns a new inactive square parented there as (square <c>RectTransform</c>, icon <c>Image</c>, value
    /// <c>TMP_Text</c>, background <c>Image</c>), or null while it cannot dress one yet. Crucible.UI's
    /// <c>KitColumnSquare</c> is the consumer; products use that, not this contract.
    /// </summary>
    public static class HudColumnSquareContract {
        public const string Id="meadworks.hud.column_square";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public static Type DelegateType=>typeof(Func<object,Tuple<object,object,object,object>>);

        public static bool TryMake(Func<object,Tuple<object,object,object,object>>? provider,object column,out Tuple<object,object,object,object>? square){
            square=null;
            if(provider==null||column==null)return false;
            try{square=provider(column);}catch{square=null;return false;}
            if(square==null||square.Item1==null||square.Item2==null||square.Item3==null||square.Item4==null){square=null;return false;}
            return true;
        }
    }

    /// <summary>
    /// The crafting API, <c>meadworks.crafting.api</c> version 1.0: Inscribe publishes the endpoints of its
    /// EliteCrafting-compatible API as named delegates, replacing Mantle's reflection into
    /// <c>ExtraPotions.Inscribe.Adopted.Api.InscribeApi</c>. Shape <c>IReadOnlyDictionary&lt;string,Delegate&gt;</c>; the
    /// endpoint names are the API's method names and <see cref="ApiVersionKey"/> holds a <c>Func&lt;int&gt;</c>.
    /// </summary>
    public static class CraftingApiContract {
        public const string Id="meadworks.crafting.api";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public const string ApiVersionKey="GetApiVersion";
        public static Type DelegateType=>typeof(IReadOnlyDictionary<string,Delegate>);

        /// <summary>The endpoint as the exact delegate type the consumer expects; another shape reads as absent.</summary>
        public static bool TryGet<T>(IReadOnlyDictionary<string,Delegate>? api,string name,out T? endpoint) where T:Delegate {
            endpoint=null;
            if(api==null||name==null||!api.TryGetValue(name,out var raw))return false;
            endpoint=raw as T;
            return endpoint!=null;
        }
        /// <summary>The published API version, or 0 when it cannot be read.</summary>
        public static int ApiVersion(IReadOnlyDictionary<string,Delegate>? api){
            if(!TryGet<Func<int>>(api,ApiVersionKey,out var read))return 0;
            try{return Math.Max(0,read!());}catch{return 0;}
        }
    }

    /// <summary>
    /// Salvage runes, <c>meadworks.crafting.salvage_rune</c> version 1.0: Inscribe (its SocketsV1 <c>SalvageRuneV1</c>)
    /// says which rune a salvaged inscribed item may give back. Shape <c>Tuple&lt;Func&lt;object,string&gt;,Func&lt;object,float&gt;&gt;</c>,
    /// both taking the native <c>ItemDrop.ItemData</c>: Item1 the rune prefab or null, Item2 its chance 0-1 (0 when the
    /// server turned it off). The consumer rolls. Read by Kindle's salvage. Absent or throwing: no rune.
    /// </summary>
    public static class SalvageRuneContract {
        public const string Id="meadworks.crafting.salvage_rune";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public static Type DelegateType=>typeof(Tuple<Func<object,string>,Func<object,float>>);
    }

    /// <summary>
    /// Item decoration, <c>meadworks.items.decoration</c> version 1.0: Inscribe (its <c>DecorationV1</c>) gives an
    /// item's decorated name (Item1, "" for none), rarity color (Item2, null for a plain item) and stat rows (Item3). Shape <c>Tuple&lt;Func&lt;object,string&gt;,Func&lt;object,string&gt;,
    /// Func&lt;object,Tuple&lt;string,string,float,string,string,string&gt;[]&gt;&gt;</c>, each taking the native
    /// <c>ItemDrop.ItemData</c>; a row is (stable key, localized label, summed value, flat|percent|flag,
    /// Higher|Lower|None, localized tooltip text). Read by Kindle's salvage lists (presentation only). Absent or
    /// throwing: the plain item name and no rows.
    /// </summary>
    public static class ItemDecorationContract {
        public const string Id="meadworks.items.decoration";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public static Type DelegateType=>typeof(Tuple<Func<object,string>,Func<object,string>,Func<object,Tuple<string,string,float,string,string,string>[]>>);
    }

    /// <summary>
    /// Wear exemption, <c>meadworks.items.wear_exempt</c> version 1.0: a product (Hex's "no durability loss") says an
    /// item used by a character loses no durability this time. Shape <c>Func&lt;object,object,bool&gt;</c>
    /// (<c>Character</c>, <c>ItemDrop.ItemData</c>). The product spending durability (Edge's secondary attacks) asks
    /// before draining, replacing Hex's Harmony patch on Edge's private method. Absent or throwing: wear applies.
    /// </summary>
    public static class WearExemptContract {
        public const string Id="meadworks.items.wear_exempt";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public static Type DelegateType=>typeof(Func<object,object,bool>);
        public static bool Exempt(Func<object,object,bool>? provider,object character,object item){
            if(provider==null||character==null||item==null)return false;
            try{return provider(character,item);}catch{return false;}
        }
    }

    /// <summary>
    /// The admin console command, <c>meadworks.admin.command</c> version 1.0: Thing runs one of its commands with the
    /// given arguments through its own admin, module and audit checks, replacing Hex's reflection into
    /// <c>ThingCommand.Run</c>. Shape <c>Func&lt;string[],bool&gt;</c>: true when Thing accepted the command for handling.
    /// </summary>
    public static class AdminCommandContract {
        public const string Id="meadworks.admin.command";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public static Type DelegateType=>typeof(Func<string[],bool>);
        public static bool TryRun(Func<string[],bool>? provider,params string[] args){
            if(provider==null||args==null||args.Length==0)return false;
            foreach(var a in args)if(a==null)return false;
            try{return provider((string[])args.Clone());}catch{return false;}
        }
    }

    /// <summary>
    /// Delve administration, <c>meadworks.delve.admin</c> version 1.0: Delve runs an admin operation on the server and
    /// answers in lines, replacing Thing's compile-time reference to Delve's assemblies. Shape
    /// <c>Func&lt;string,IReadOnlyList&lt;string&gt;,Tuple&lt;bool,IReadOnlyList&lt;string&gt;&gt;&gt;</c>: operation, arguments
    /// -&gt; (handled, reply lines). Delve keeps its authority checks; the caller audits. Unknown operations reply
    /// (false, empty). Operations and their arguments:
    /// <list type="bullet">
    /// <item><see cref="Status"/>: none; one line per reset kind.</item>
    /// <item><see cref="Actions"/>: none; the last actions, newest first.</item>
    /// <item><see cref="DryRun"/>: kind, "on" or "off".</item>
    /// <item><see cref="Request"/>: one or more kinds; asks Delve to run them at its next check.</item>
    /// <item><see cref="Sites"/> and <see cref="Nature"/>: x, z of the caller; the known sites near it.</item>
    /// <item><see cref="Nearest"/>: x, z; queues the nearest full-reset site for one guarded evaluation.</item>
    /// <item><see cref="SurfaceNature"/> and <see cref="SurfaceResources"/>: x, y, z, caller name; surface reset around the point.</item>
    /// </list>
    /// </summary>
    public static class DelveAdminContract {
        public const string Id="meadworks.delve.admin";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public const string Status="status",Actions="actions",DryRun="dry_run",Request="request",Sites="sites",Nature="nature",
            Nearest="nearest",SurfaceNature="surface_nature",SurfaceResources="surface_resources";
        public const int MaximumLines=4096;
        public static Type DelegateType=>typeof(Func<string,IReadOnlyList<string>,Tuple<bool,IReadOnlyList<string>>>);

        /// <summary>Runs an operation; absent, throwing, unhandled or malformed replies give false and no lines.</summary>
        public static bool TryRun(Func<string,IReadOnlyList<string>,Tuple<bool,IReadOnlyList<string>>>? provider,string operation,IReadOnlyList<string> arguments,out IReadOnlyList<string> lines){
            lines=Array.Empty<string>();
            if(provider==null||string.IsNullOrEmpty(operation)||arguments==null)return false;
            Tuple<bool,IReadOnlyList<string>> raw;
            try{raw=provider(operation,arguments);}catch{return false;}
            if(raw==null||!raw.Item1||raw.Item2==null||raw.Item2.Count>MaximumLines)return false;
            foreach(var line in raw.Item2)if(line==null)return false;
            lines=raw.Item2;
            return true;
        }
    }
}
