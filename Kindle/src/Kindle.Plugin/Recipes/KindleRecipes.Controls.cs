// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Behavior adapted from OpenKeep 3.0.0 ad278ecf323901c3645eb1f9f6fac6833a0f8cb1,
// Recipes/{RecipeRows,RecipeFavourites,RecipeActions,RecipeInput,RecipeGamepad,RecipeStar}.cs.
// Copyright (C) 2026 Gerald Glasgow. GNU GPL version 3 or later; see THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Crucible kit buttons/tiles/navigation; Kindle keys, input and tracker hooks.
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ExtraPotions.Kindle.Config;
using ExtraPotions.Kindle.Recipes;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Kindle.Runtime
{
    public static partial class KindleRecipes
    {
        static TrackLabelState shownTrack;
        static GameObject hoveredRow;
        static Recipe hoveredRecipe;
        static KitIconButton favouriteButton;
        static Button trackButton;
        static RectTransform style;
        static Sprite starSprite;
        static Color starColour=new Color(1f,0.713f,0.361f,1f);
        static float nextStar;
        static readonly System.Collections.Generic.HashSet<string> latched=new System.Collections.Generic.HashSet<string>();
        static Recipe Hovered=>hoveredRow && hoveredRow.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)hoveredRow.transform,ZInput.pointerPosition) ? hoveredRecipe : null;
        static void Star()
        {
            if(starSprite || Time.unscaledTime<nextStar)return;
            nextStar=Time.unscaledTime+2f;
            var build=Hud.instance ? Hud.instance.m_buildUi : null;
            var button=build && build.m_pieceButtonPrefab ? build.m_pieceButtonPrefab.GetComponent<BuildUiPieceButton>() : null;
            var image=button ? button.m_favoriteStar : null;
            if(image && image.sprite){starSprite=image.sprite;starColour=image.color;}
        }
        static void Decorate(InventoryGui gui)
        {
            hoveredRow=null;Star();
            foreach(var pair in gui.m_availableRecipes)
            {
                var row=pair.InterfaceElement;var recipe=pair.Recipe;if(!row || !recipe)continue;
                var input=row.GetComponent<UIInputHandler>() ?? row.AddComponent<UIInputHandler>();
                int quality=Quality(pair.ItemData);
                input.m_onMiddleClick=_=>Guard(()=>ToggleFavourite(recipe));
                input.m_onRightClick=_=>Guard(()=>Track(gui,recipe,quality));
                input.m_onPointerEnter+=_=>{hoveredRow=row;hoveredRecipe=recipe;};
                input.m_onPointerExit+=_=>{if(hoveredRow==row)hoveredRow=null;};
                var rect=(RectTransform)row.transform;
                tiles.Mark(rect,Favourite(recipe),starSprite,starColour);
                tiles.Greyed(rect,!pair.CanCraft);
                if(RecipeViews.IsGrid(config.Get(KindleSettings.RecipeList.RecipeView)))
                    tiles.Tooltip(rect,Localize(recipe.m_item?.m_itemData?.m_shared?.m_name ?? recipe.m_item?.gameObject?.name ?? recipe.name),recipe.m_amount>1?"x"+recipe.m_amount:"");
            }
        }
        static void ToggleFavourite(Recipe recipe)
        {
            if(!Active || !recipe || !config.Get(KindleSettings.RecipeList.Favourites))return;
            ReadFavourites();bool on=favourites.Toggle(Key(recipe));WriteFavourites();
            KindleText.Notice(KindleText.Format(on?"recipe_favorite_on":"recipe_favorite_off",Localize(recipe.m_item?.m_itemData?.m_shared?.m_name ?? recipe.m_item?.gameObject?.name ?? recipe.name)));
        }
        static void Only(KitClickKind kind)
        {
            if(!Active || !config.Get(KindleSettings.RecipeList.Favourites))return;
            ReadFavourites();
            if(kind==KitClickKind.Shift)
            {
                if(favourites.Count==0){Message("recipe_none");return;}
                if(!UnifiedPopup.IsAvailable())return;
                var captured=Player.m_localPlayer;
                Action clear=()=>Guard(()=>{if(!Active || Player.m_localPlayer!=captured)return;ReadFavourites();favourites.Clear();WriteFavourites();Message("recipe_cleared");});
                string topic=KindleText.Text("recipe_only_topic"),ask=KindleText.Format("recipe_clear_ask",favourites.Count);
                if(Plugin.Source!=null && KitDialogs.Confirm(Plugin.Source,topic,ask,clear)!=null)return;
                UnifiedPopup.Push(new YesNoPopup(topic,ask,()=>{UnifiedPopup.Pop();clear();},UnifiedPopup.Pop));
                return;
            }
            var outcome=favourites.ToggleOnly();if(outcome!=OnlyToggle.RefusedNoFavourites)WriteFavourites();
            Message(outcome==OnlyToggle.On?"recipe_only_on":outcome==OnlyToggle.Off?"recipe_only_off":"recipe_none");
        }
        static void Message(string key)=>KindleText.Notice(KindleText.Text(key));
        static void Track(InventoryGui gui,Recipe recipe,int quality)
        {
            if(!Active || !recipe || !config.Get(KindleSettings.Tracker.Enabled))return;
            int amount=gui.InCraftTab() && Selected(gui)==recipe ? KindleBatch.NextCraft(gui,Player.m_localPlayer) : 1;
            ToggleTracking?.Invoke(recipe,quality,amount,Upgrader);
        }
        static void RefreshActions(InventoryGui gui)
        {
            if(!gui.m_variantButton){HideActions();return;}
            style=(RectTransform)gui.m_variantButton.transform;
            if(favouriteButton==null || !favouriteButton.Button)
            {
                favouriteButton=KitIconButton.Create((RectTransform)style.parent,"Kindle_favourite",KitGlyph.Star,Vector2.zero,style.rect.height,_=>Guard(()=>ToggleFavourite(Selected(gui))));
                trackButton=KitButton.Create((RectTransform)style.parent,"Kindle_track",KindleText.Text("tracker_track"),()=>Guard(()=>Track(gui,Selected(gui),Quality(gui.m_selectedRecipe.ItemData))),Vector2.zero,new Vector2(80f,style.rect.height));
                if(favouriteButton!=null)favouriteButton.Tooltip(KindleText.Text("recipe_favorite_topic"),KindleText.Text("recipe_favorite_tip"));
            }
            var recipe=Selected(gui);bool shown=recipe && !CustomTabSelected(gui);
            bool track=shown && config.Get(KindleSettings.Tracker.Enabled) && ToggleTracking!=null;
            bool favourite=shown && config.Get(KindleSettings.RecipeList.Favourites);
            if(trackButton)trackButton.gameObject.SetActive(track);
            if(favouriteButton!=null && favouriteButton.Button)favouriteButton.Button.gameObject.SetActive(favourite);
            if(!shown)return;
            float x=style.anchoredPosition.x-style.rect.width*style.pivot.x;if(style.gameObject.activeSelf)x+=style.rect.width+6f;
            if(track && trackButton)
            {
                bool tracked=IsTracked?.Invoke(recipe,Quality(gui.m_selectedRecipe.ItemData),Upgrader)==true;
                // The label and tooltip are written when the word changes, not every frame.
                var now=new TrackLabelState(trackButton,tracked,KindleText.Revision);
                if(!now.Equals(shownTrack))
                {
                    shownTrack=now;
                    string text=KindleText.Text(tracked?"tracker_untrack":"tracker_track");
                    if(trackButton.GetComponentInChildren<TMP_Text>(true) is TMP_Text label)label.text=text;
                    KitTooltip.Set(trackButton.gameObject,text,text);
                }
                x=PlaceAction((RectTransform)trackButton.transform,x);
            }
            if(favourite && favouriteButton!=null){Star();favouriteButton.SetSprite(starSprite);favouriteButton.SetOn(Favourite(recipe));PlaceAction((RectTransform)favouriteButton.Button.transform,x);}
        }
        static float PlaceAction(RectTransform rect,float x)
        {
            rect.anchorMin=style.anchorMin;rect.anchorMax=style.anchorMax;
            rect.anchoredPosition=new Vector2(x+rect.rect.width*rect.pivot.x,style.anchoredPosition.y);return x+rect.rect.width+6f;
        }
        static void HideActions(){if(trackButton)trackButton.gameObject.SetActive(false);if(favouriteButton!=null && favouriteButton.Button)favouriteButton.Button.gameObject.SetActive(false);}
        static void ClearActions()
        {
            if(trackButton)UnityEngine.Object.Destroy(trackButton.gameObject);
            if(favouriteButton!=null && favouriteButton.Button)UnityEngine.Object.Destroy(favouriteButton.Button.gameObject);
            trackButton=null;favouriteButton=null;style=null;starSprite=null;nextStar=0;latched.Clear();
        }
        static void InputTick()
        {
            if(CustomTabSelected(InventoryGui.instance) || KitTextField.Typing)return;
            if(config.Shortcut(KindleSettings.RecipeList.SearchKey).IsDown())search?.Focus();
            if(Hovered && config.Shortcut(KindleSettings.RecipeList.FavouriteRecipeKey).IsDown())ToggleFavourite(Hovered);
        }
        static bool Once(string button)
        {
            if(!ZInput.GetButton(button)){latched.Remove(button);return false;}
            return ZInput.GetButtonDown(button) && latched.Add(button);
        }
        internal static bool Gamepad(InventoryGui gui)
        {
            if(!Active || CustomTabSelected(gui))return true;
            if(KitTextField.Typing)return false;
            bool runGame=true;
            Guard(()=>{
                if(config.Get(KindleSettings.RecipeList.GamepadControls))
                {
                    if(Once("JoyRStickUp"))search?.Focus();
                    if(Once("JoyRStickDown"))Track(gui,Selected(gui),Quality(gui.m_selectedRecipe.ItemData));
                    if(Once("JoyRStickLeft"))ToggleFavourite(Selected(gui));
                    if(Once("JoyRStickRight"))Only(KitClickKind.Plain);
                    var sortButton=config.GamepadSortButton;if(sortButton.HasValue && Once(GamepadMap.ZInputName(sortButton.Value)))CycleSort();
                }
                if(tiles==null || tiles.Columns<=1)return;
                runGame=false;int x=0,y=0;
                if(ZInput.GetButtonDown("JoyLStickDown") || ZInput.GetButtonDown("JoyDPadDown"))y=1;
                else if(ZInput.GetButtonDown("JoyLStickUp") || ZInput.GetButtonDown("JoyDPadUp"))y=-1;
                else if(ZInput.GetButtonDown("JoyLStickRight") || (!KindleBatch.Shown && ZInput.GetButtonDown("JoyDPadRight")))x=1;
                else if(ZInput.GetButtonDown("JoyLStickLeft") || (!KindleBatch.Shown && ZInput.GetButtonDown("JoyDPadLeft")))x=-1;
                int selected=gui.GetSelectedRecipeIndex();if(selected<0 || gui.m_availableRecipes.Count==0 || (x==0 && y==0))return;
                var next=tiles.Navigator().Move(new GridCell(selected%tiles.Columns,selected/tiles.Columns),new GridCell(x,y));
                gui.SetRecipe(next.Y*tiles.Columns+next.X,true);
            });return runGame;
        }
    }
}
