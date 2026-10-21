// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Native lifecycle, recipe facts and character state adapted from OpenKeep 3.0.0
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 Recipes/{SearchBar,RecipeFilter,RecipeFavourites,RecipeKeys}.cs.
// Copyright (C) 2026 Gerald Glasgow. GNU GPL version 3 or later; see THIRD_PARTY_NOTICES.
// Sort persistence adapted from CraftingSearch 1.0.2 038d154dc47f5d3f23da78da0acfa0af35240aaa RecipeList.cs.
// Copyright (c) 2026 Ab5oluteZer0; MIT notice: THIRD_PARTY_NOTICES/CraftingSearch-MIT.txt.
// Changed by expDARE, 2026: existing Kindle portable pipeline/config and Crucible kit controls; no upstream UI; the
// category row (KindleRecipes.Categories.cs).
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using ExtraPotions.Kindle.Config;
using ExtraPotions.Kindle.Recipes;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Kindle.Runtime
{
    public static partial class KindleRecipes
    {
        static KindleConfig config;
        static MeadworksLog log;
        static Harmony patches;
        static InventoryGui owner;
        static Player cachedFor;
        static FavouriteRecipes favourites=FavouriteRecipes.Parse(null,null);
        static readonly RecipeKeyIndex<Recipe> index=new RecipeKeyIndex<Recipe>(Key,r=>r && r.m_item);
        static KitSearchRow search;
        static KitTileList tiles;
        static KitLatch latch=new KitLatch();
        static RecipeQuery query=RecipeQuery.All;
        static string queryText="";
        static bool rebuilding;
        static readonly Dictionary<string,KitIconButton> searchButtons=new Dictionary<string,KitIconButton>();
        public static Action<Recipe,int,int,bool> ToggleTracking { get; set; }
        public static Func<Recipe,int,bool,bool> IsTracked { get; set; }
        public static bool Active => config!=null && Plugin.GameplayAvailable && Plugin.AllowsUi && Player.m_localPlayer && ZNet.instance;
        public static RecipeQuery Query => search!=null && search.Shown ? query : RecipeQuery.All;
        public static Recipe Selected(InventoryGui gui)=>gui ? gui.m_selectedRecipe.Recipe : null;
        static bool CustomTabSelected(InventoryGui gui)=>gui && gui.m_tabCraft && KitTabs.HasSelected(gui.m_tabCraft.transform.parent as RectTransform);
        public static int Quality(ItemDrop.ItemData item)=>item==null?1:item.m_quality+1;
        public static bool Upgrader=>Player.m_localPlayer && Player.m_localPlayer.GetCurrentCraftingStation() && Player.m_localPlayer.GetCurrentCraftingStation().m_upgrader;
        // The crafting panel asks for the selected recipe's key every frame, and each Object.name read builds a new string.
        // shortcut: a recipe's key is kept while the same recipe object is asked again; drop the cache if a mod ever renames recipes in play.
        static Recipe keyRecipe;static string keyText="";
        public static string Key(Recipe recipe)
        {
            if(!recipe || !recipe.m_item)return "";
            if(ReferenceEquals(recipe,keyRecipe))return keyText;
            keyText=RecipeKeys.Of(recipe.name,recipe.m_item.gameObject.name);keyRecipe=recipe;return keyText;
        }
        public static Recipe Find(string key)=>ObjectDB.instance ? index.Find(ObjectDB.instance,ObjectDB.instance.m_recipes,key) : null;
        public static void Initialize(KindleConfig settings,MeadworksLog logger)
        {
            Shutdown();config=settings;log=logger;
            config.Changed+=Changed;
            patches=new Harmony(Plugin.Guid+".recipes");patches.CreateClassProcessor(typeof(KindleRecipePatches)).Patch();patches.CreateClassProcessor(typeof(TypingStill)).Patch();
        }
        static void Changed(object sender,EventArgs args)
        {
            Guard(()=>{shownSearch=default;DetachCategories();search?.Detach();search=null;searchButtons.Clear();query=RecipeQuery.Parse(queryText,config.Get(KindleSettings.RecipeList.IgnoreAccents));Rebuild();});
        }
        internal static void Guard(Action action){try{action();}catch(Exception error){log?.Warning("Recipe list: "+error.GetType().Name+": "+error.Message);}}
        static void Bind(InventoryGui gui)
        {
            if(owner==gui)return;
            ClearUi();owner=gui;queryText="";query=RecipeQuery.All;latch=new KitLatch();picked=RecipeCategory.None;
        }
        static void ReadFavourites()
        {
            var player=Player.m_localPlayer;if(cachedFor==player)return;
            cachedFor=player;
            player.m_customData.TryGetValue(FavouriteRecipes.SetKey,out var set);player.m_customData.TryGetValue(FavouriteRecipes.OnlyKey,out var only);
            favourites=FavouriteRecipes.Parse(set,only);
        }
        static void WriteFavourites()
        {
            var data=Player.m_localPlayer.m_customData;
            Write(data,FavouriteRecipes.SetKey,favourites.SerializeSet());Write(data,FavouriteRecipes.OnlyKey,favourites.SerializeOnly());Rebuild();
        }
        static void Write(Dictionary<string,string> data,string key,string value){if(value==null)data.Remove(key);else data[key]=value;}
        static bool Favourite(Recipe recipe)=>config.Get(KindleSettings.RecipeList.Favourites) && favourites.Contains(Key(recipe));
        static RecipeSort Sort
        {
            get{var player=Player.m_localPlayer;player.TryGetUniqueKeyValue(RecipeSorts.GameKey,out var method);return RecipeSorts.FromStored(method,player.TryGetUniqueKeyValue(RecipeSorts.DescendingKey,out _));}
        }
        static void CycleSort()
        { CycleSort(KitClickKind.Plain); }
        static void CycleSort(KitClickKind kind)
        {
            if(!Active)return;
            var mode=kind==KitClickKind.Shift?RecipeSorts.Previous(Sort):RecipeSorts.Next(Sort);var stored=RecipeSorts.ToStored(mode);var player=Player.m_localPlayer;
            player.RemoveUniqueKeyValue(RecipeSorts.GameKey);player.RemoveUniqueKeyValue(RecipeSorts.DescendingKey);
            if(stored.Game!=null)player.AddUniqueKeyValue(RecipeSorts.GameKey,stored.Game);
            if(stored.Descending)player.AddUniqueKeyValue(RecipeSorts.DescendingKey,"1");Rebuild();
        }
        public static void Rebuild()
        {
            if(!Active || rebuilding || !InventoryGui.instance || !InventoryGui.IsVisible())return;
            Guard(()=>{rebuilding=true;try{InventoryGui.instance.UpdateCraftingPanel(false);}finally{rebuilding=false;}});
        }
        static RecipeFacts Facts(InventoryGui.RecipeDataPair pair)
        {
            var recipe=pair.Recipe;if(!recipe || !recipe.m_item)return null;
            var materials=recipe.m_resources.Where(r=>r.m_resItem).Select(r=>new RecipeMaterial(Localize(r.m_resItem.m_itemData.m_shared.m_name),r.m_resItem.gameObject.name,r.GetAmount(Quality(pair.ItemData)),r.m_upgraderResource)).ToArray();
            return new RecipeFacts(Key(recipe),Localize(recipe.m_item.m_itemData.m_shared.m_name),recipe.m_item.gameObject.name,materials,pair.CanCraft,recipe.m_listSortWeight,pair.ItemData?.m_quality??0,CategoryOf(recipe.m_item.m_itemData));
        }
        static string Localize(string text)=>Localization.instance!=null?Localization.instance.Localize(text):text;
        internal static void Apply(InventoryGui gui)
        {
            if(!Active || CustomTabSelected(gui))return;
            Prepare(gui);ReadFavourites();
            RecipePipeline.Apply(gui.m_availableRecipes,Facts,new RecipeListState(Query,favourites,Sort,config.Get(KindleSettings.RecipeList.Favourites),config.Get(KindleSettings.RecipeList.FavouritesFirst),Upgrader,categories:PickedCategories),pair=>{if(pair.InterfaceElement)UnityEngine.Object.Destroy(pair.InterfaceElement);});
            tiles?.Forget();tiles=KitTileList.For(gui.m_recipeListRoot,gui.m_recipeListSpace,gui.m_recipeListBaseSize);
            if(tiles==null)return;
            tiles.View=(KitTileView)(int)config.Get(KindleSettings.RecipeList.RecipeView);
            tiles.Arrange(gui.m_availableRecipes.Where(p=>p.InterfaceElement).Select(p=>(RectTransform)p.InterfaceElement.transform).ToList());
            Decorate(gui);
        }
        internal static void Prepare(InventoryGui gui)
        {
            if(!Active)return;
            Bind(gui);ReadFavourites();
            KitTabs.RestoreEmptyRecipeCaption(gui,gui.m_selectedRecipe.Recipe!=null);
            if(search==null && !latch.Failed)
            {
                var view=gui.m_recipeListRoot ? gui.m_recipeListRoot.parent : null;
                var panel=view ? view.parent as RectTransform : null;
                var buttons=new List<KitSearchButton>();
                buttons.Add(new KitSearchButton{Id="view",Glyph=KitGlyph.Bars,Click=kind=>Guard(()=>CycleView(kind))});
                if(config.Get(KindleSettings.RecipeList.Favourites))buttons.Add(new KitSearchButton{Id="only",Glyph=KitGlyph.Star,Click=kind=>Guard(()=>Only(kind))});
                if(config.Get(KindleSettings.RecipeList.SortButton))buttons.Add(new KitSearchButton{Id="sort",Glyph=KitGlyph.SortDefault,Click=kind=>Guard(()=>CycleSort(kind))});
                search=KitSearchRow.Attach(panel,"Kindle_recipe_search",buttons,latch,KindleText.Text("recipe_search"));
                if(search!=null)
                {
                    search.ListWritten+=size=>gui.m_recipeListBaseSize=size.y;
                    search.Field.ReportChat=true;search.Field.Text=queryText;
                    search.Field.Changed+=text=>{queryText=text;query=RecipeQuery.Parse(text,config.Get(KindleSettings.RecipeList.IgnoreAccents));var scroll=gui.m_recipeListRoot.GetComponentInParent<ScrollRect>();if(scroll)scroll.verticalNormalizedPosition=1f;Rebuild();};
                    for(int i=0;i<search.Buttons.Count;i++)searchButtons[buttons[i].Id]=search.Buttons[i];
                    KitTooltip.Set(search.Field.Rect.gameObject,KindleText.Text("recipe_search_topic"),KindleText.Text("recipe_search_tip"));
                }
            }
            if(search!=null){search.ClearOnClose=config.Get(KindleSettings.RecipeList.ClearSearchOnClose);TickRows(gui,config.Get(KindleSettings.RecipeList.Search) && !CustomTabSelected(gui));RefreshSearch();}
        }
        static void CycleView(KitClickKind kind)
        {
            if(!Active || !config.TryChange(KindleSettings.RecipeList.RecipeView))return;
            var current=config.Get(KindleSettings.RecipeList.RecipeView);config.Entry(KindleSettings.RecipeList.RecipeView).BoxedValue=kind==KitClickKind.Shift?RecipeViews.Previous(current):RecipeViews.Next(current);
        }
        static readonly string[] viewKeys={"recipe_view_list","recipe_view_compact","recipe_view_small","recipe_view_medium","recipe_view_large"};
        static readonly KitGlyph[] sortGlyphs={KitGlyph.SortDefault,KitGlyph.SortAscending,KitGlyph.SortDescending,KitGlyph.SortType,KitGlyph.SortWeight};
        static readonly string[] sortKeys={"sort_default","sort_az","sort_za","sort_type","sort_weight"};
        // The buttons are written only when what they show changed (the row is rebuilt on a language change, which resets this).
        static SearchButtonsState shownSearch;
        static void RefreshSearch()
        {
            var now=new SearchButtonsState(search,favourites.OnlyFavourites(true),(int)config.Get(KindleSettings.RecipeList.RecipeView),config.IsLocked(KindleSettings.RecipeList.RecipeView),(int)Sort,KindleText.Revision);
            if(now.Equals(shownSearch))return;
            shownSearch=now;
            if(searchButtons.TryGetValue("only",out var only)){only.SetOn(now.Only);only.Tooltip(KindleText.Text("recipe_only_topic"),KindleText.Text("recipe_only_tip"));}
            if(searchButtons.TryGetValue("view",out var view)){view.SetGlyph((KitGlyph)now.View);view.Tooltip(KindleText.Text(viewKeys[now.View]),KindleText.Text("recipe_view_tip"));view.SetUsable(!now.ViewLocked);}
            if(searchButtons.TryGetValue("sort",out var sort)){sort.SetGlyph(sortGlyphs[now.Sort]);sort.Tooltip(KindleText.Format("sort_topic",KindleText.Text(sortKeys[now.Sort])),KindleText.Text("sort_tip"));}
        }
        internal static void Closed(){search?.Closed();if(config!=null && config.Get(KindleSettings.RecipeList.ClearSearchOnClose)){queryText="";query=RecipeQuery.All;}}
        public static void Tick()
        {
            if(!Active){HideRows();HideActions();return;}
            if(!InventoryGui.instance || !InventoryGui.IsVisible())return;
            Guard(()=>{Prepare(InventoryGui.instance);RefreshActions(InventoryGui.instance);InputTick();});
        }
        /// <summary>
        /// The game's language changed: the search row, track and favourite buttons are made again in the new language and
        /// the open crafting list and recipe text are rewritten at once (the game rewrites its list only when it changes).
        /// </summary>
        internal static void Relabel()
        {
            if(!Active)return;
            ClearUi();
            var gui=InventoryGui.instance;
            if(!gui || !InventoryGui.IsVisible() || CustomTabSelected(gui))return;
            Guard(()=>{gui.UpdateCraftingPanel(false);RefreshActions(gui);gui.UpdateRecipe(Player.m_localPlayer,0f);});
        }
        static void ClearUi(){shownSearch=default;shownTrack=default;DetachCategories();search?.Detach();search=null;tiles?.Forget();tiles=null;searchButtons.Clear();ClearActions();hoveredRow=null;}
        public static void Shutdown()
        {
            if(config!=null)config.Changed-=Changed;
            patches?.UnpatchSelf();patches=null;ClearUi();
            if(owner && Player.m_localPlayer && InventoryGui.IsVisible() && !CustomTabSelected(owner))Guard(()=>owner.UpdateCraftingPanel(false));
            owner=null;cachedFor=null;config=null;log=null;query=RecipeQuery.All;queryText="";
        }
    }
    [HarmonyPatch]
    internal static class KindleRecipePatches
    {
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.UpdateCraftingPanel)),HarmonyPrefix,HarmonyPriority(Priority.First)]
        static void Prepare(InventoryGui __instance)=>KindleRecipes.Guard(()=>KindleRecipes.Prepare(__instance));
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.UpdateCraftingPanel)),HarmonyPostfix]
        static void Prepared(InventoryGui __instance)=>KindleRecipes.Guard(()=>KindleRecipes.Prepare(__instance));
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.UpdateRecipeList)),HarmonyPostfix]
        static void Listed(InventoryGui __instance)=>KindleRecipes.Guard(()=>KindleRecipes.Apply(__instance));
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.UpdateRecipeGamepadInput)),HarmonyPrefix]
        static bool Pad(InventoryGui __instance)=>KindleRecipes.Gamepad(__instance);
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.Hide)),HarmonyPostfix]
        static void Closed()=>KindleRecipes.Guard(KindleRecipes.Closed);
    }
}
