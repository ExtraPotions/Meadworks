// Kindle kit presentation over OpenKeep 3.0.0's salvage tab/list/panel behavior.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// OpenKeep UI construction is not copied: tab, rows and tooltips use existing Crucible.UI factories.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using ExtraPotions.Kindle.Config;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Kindle.Runtime.Salvage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using Object = UnityEngine.Object;
namespace ExtraPotions.Kindle.Runtime
{
    public static class KindleSalvage
    {
        static bool initialized, wasActive;
        static InventoryGui gui;
        static KitGameTab tab;
        static KitTileList layout;
        static readonly List<KeyValuePair<ItemDrop.ItemData,GameObject>> rows = new List<KeyValuePair<ItemDrop.ItemData,GameObject>>();
        static ItemDrop.ItemData selected;
        static int selectedIndex;
        static Action<string> warning;
        static Harmony patches;
        static bool configurationDirty;
        static DrawKey drawn;
        static readonly Color BlockedName=new Color(0.6f,0.6f,0.6f,1f);
        static int drawGeneration=1;
        static readonly HashSet<string> warned = new HashSet<string>();
        // Rows of gear in nearby chests (Burrow): the chest each listed item is in.
        static readonly Dictionary<ItemDrop.ItemData,Container> chestOf = new Dictionary<ItemDrop.ItemData,Container>();
        // Burrow's Reach color for what comes from containers, so a chest row reads like a chest amount in the crafting panel.
        const string ChestColour="#6fc3ff";
        public static bool Ready => initialized && Plugin.GameplayAvailable && Plugin.AllowsUi && SalvageYaml.Ready
            && CruciblePlugin.IsReady && CruciblePlugin.Session.WorldId != 0 && ZNet.instance != null
            && Player.m_localPlayer != null && !Player.m_localPlayer.IsDead() && !Player.m_localPlayer.IsTeleporting();
        public static bool Active => Ready && SalvageSettings.Enabled.Value && tab != null && tab.Selected;
        public static ItemDrop.ItemData Selected => selected;
        public static IReadOnlyDictionary<string,string> Files => SalvageYaml.Files;
        public static void Initialize(ConfigFile config, ProductRegistration registration, Action<string> warn = null)
        {
            Shutdown();
            warning=warn;initialized=true;
            SalvageYaml.Initialize(config,registration);
            if(Plugin.Settings!=null) Plugin.Settings.Changed+=Changed;
            if (Plugin.AllowsUi && !Plugin.SteppedAside)
            {
                patches=new Harmony(Plugin.Guid+".salvage");
                patches.CreateClassProcessor(typeof(SalvageGuiPatches)).Patch();
            }
        }
        internal static void Warn(string text) { if(warned.Add(text)){if(warning!=null)warning(text);else Debug.LogWarning("Kindle: "+text);} }
        internal static void Guard(Action action) { try{action();}catch(Exception error){Warn("Salvage UI: "+error.GetType().Name);} }
        internal static bool Guard(Func<bool> action) { try{return action();}catch(Exception error){Warn("Salvage UI: "+error.GetType().Name);Deselect();return true;} }
        static void Changed(object sender,EventArgs args){configurationDirty=true;drawGeneration++;}
        public static void Reload() => SalvageYaml.Reload();
        static int favouriteStamp;
        static float nextFavouriteCheck;
        /// <summary>Favoriting or unfavoriting an item (Burrow) shows in the open Salvage tab within 0.25 s: listed dimmed with
        /// the reason, or salvageable again. The favorites are read at most four times a second, only while the tab is open.</summary>
        static bool FavouritesChanged()
        {
            float now=Time.unscaledTime;
            if (wasActive && now<nextFavouriteCheck) return false;
            nextFavouriteCheck=now+0.25f;
            int stamp=SalvageInterop.FavouritesStamp();
            bool changed=wasActive && stamp!=favouriteStamp;
            favouriteStamp=stamp;
            return changed;
        }
        static int chestStamp;
        static float nextChestCheck;
        /// <summary>A chest item taken, added or moved (by anyone) shows in the open tab within half a second; read only while it is open.</summary>
        static bool ChestsChanged()
        {
            float now=Time.unscaledTime;
            if (wasActive && now<nextChestCheck) return false;
            nextChestCheck=now+0.5f;
            int stamp=SalvageChests.Stamp();
            bool changed=wasActive && stamp!=chestStamp;
            chestStamp=stamp;
            return changed;
        }
        public static void Tick()
        {
            if (!initialized) return;
            SalvageChests.Tick();
            SalvageYaml.Tick();
            if (!Plugin.AllowsUi || Plugin.SteppedAside) return;
            Ensure(InventoryGui.instance);
            if (tab == null || !tab.Button) { SalvageTabCapability.Active=false;return; }
            tab.Refresh();
            bool enabled=Ready && SalvageSettings.Enabled.Value;
            tab.Button.gameObject.SetActive(enabled);
            var caption=tab.Button.GetComponentInChildren<TMP_Text>(true);
            if (caption != null) caption.text=SalvageWords.Salvage;
            if (!enabled && tab.Selected) Leave();
            SalvageTabCapability.Active=Active && InventoryGui.IsVisible();
            bool favourites=SalvageTabCapability.Active && FavouritesChanged();
            bool chests=SalvageTabCapability.Active && ChestsChanged();
            if (SalvageTabCapability.Active && (!wasActive || configurationDirty || favourites || chests)) { configurationDirty=false;drawGeneration++;Rebuild(); }
            wasActive=SalvageTabCapability.Active;
        }
        public static void Ensure(InventoryGui instance)
        {
            if (!initialized || !Plugin.AllowsUi || Plugin.SteppedAside || instance == null || instance.m_tabUpgrade == null) return;
            if (gui == instance && tab != null && tab.Button != null) return;
            // A tab that could not be made for this window (another UI mod moved the tabs) is not torn down and made again every frame.
            if (failedGui == instance) return;
            Clear();layout?.Forget();tab?.Dispose();
            gui=instance;selected=null;selectedIndex=0;wasActive=false;
            failedGui=instance;
            tab=KitTabs.Append(instance.m_tabUpgrade.transform.parent as RectTransform,instance.m_tabUpgrade,"Kindle_SalvageTab",SalvageWords.Salvage,()=>Select(instance));
            layout=KitTileList.For(instance.m_recipeListRoot,instance.m_recipeListSpace,instance.m_recipeListBaseSize);
            if (tab != null && tab.Button != null) failedGui=null;
        }
        static InventoryGui failedGui;
        static void Select(InventoryGui instance)
        {
            if (!Ready || !SalvageSettings.Enabled.Value || instance != gui || instance.m_craftTimer >= 0f) { tab?.Select(false);return; }
            // Native reset invokes the existing owners' deselect hooks before Salvage takes the recipe column.
            instance.OnTabCraftPressed();
            tab.Select(true);SalvageTabCapability.Active=true;
            if (instance.m_uiGroups != null && instance.m_uiGroups.Length>3) instance.SetActiveGroup(instance.m_uiGroups[3]);
            instance.m_tabCraft.interactable=true;instance.m_tabUpgrade.interactable=true;
            instance.UpdateCraftingPanel();
        }
        /// <summary>Whether the Salvage tab could list anything now: salvage on and one item that may be salvaged.</summary>
        public static bool AnySalvageable(Player player)
            => Ready && SalvageSettings.Enabled.Value && player != null
               && player.GetInventory().GetAllItems().Any(item => SalvageInterop.ListedCell(player,item) && SalvageActions.CanSalvage(item));
        /// <summary>Opens the Salvage tab with <paramref name="item"/> chosen when it is listed (the station's Salvage button).</summary>
        public static void Open(InventoryGui instance, ItemDrop.ItemData item)
        {
            Ensure(instance);
            if (tab == null || instance != gui) return;
            if (item != null) selected=item;
            Select(instance);
        }
        public static void Deselect()
        {
            tab?.Select(false);SalvageTabCapability.Active=false;wasActive=false;Clear();drawGeneration++;
        }
        static void Leave()
        {
            Deselect();
            if (gui != null && Player.m_localPlayer != null) gui.OnTabCraftPressed();
        }
        public static bool BeforeCraftingPanel(InventoryGui instance,bool focusView)
        {
            Ensure(instance);
            if (!Active) { SalvageTabCapability.Active=false;return true; }
            SalvageTabCapability.Active=true;
            Player player=Player.m_localPlayer;
            var station=player.GetCurrentCraftingStation();
            bool free=player.NoCostCheat() || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost));
            instance.m_tabCraft.gameObject.SetActive(station==null || station.m_hasCraftTab);
            instance.m_tabUpgrade.gameObject.SetActive(station!=null || free);
            instance.m_tabCraft.interactable=true;instance.m_tabUpgrade.interactable=true;
            Rebuild(focusView);
            return false;
        }
        public static void Rebuild() { if (Active && gui != null) Rebuild(false); }
        static void Rebuild(bool center)
        {
            foreach (var pair in gui.m_availableRecipes) if (pair.InterfaceElement != null) Object.Destroy(pair.InterfaceElement);
            gui.m_availableRecipes.Clear();gui.m_selectedRecipe=default(InventoryGui.RecipeDataPair);
            Clear();
            foreach (var item in Player.m_localPlayer.GetInventory().GetAllItemsInGridOrder())
            {
                if (!SalvageInterop.ListedCell(Player.m_localPlayer,item)) continue;
                AddRow(item,SalvageActions.WhyNot(item),null);
            }
            // Gear in the nearby chests Burrow lets this player craft from, nearest chest first (Salvage / Nearby Chests).
            foreach (var pair in SalvageChests.Items())
                if (!chestOf.ContainsKey(pair.Value))
                    AddRow(pair.Value,SalvageRules.Blocker(Player.m_localPlayer,pair.Value,pair.Key.GetInventory()),pair.Key);
            if (layout != null) { layout.View=KitTileView.List;layout.Arrange(rows.Select(r=>r.Value.transform as RectTransform).ToList()); }
            int restore=rows.FindIndex(r=>r.Key==selected);
            Choose(restore>=0?restore:Mathf.Clamp(selectedIndex,0,rows.Count-1),center);
        }
        static void AddRow(ItemDrop.ItemData item,string blocked,Container chest)
        {
            if (item?.m_shared == null) return;
            // Gear with a recipe that cannot be salvaged now is listed dimmed with the reason, so it is never silently missing.
            if (blocked!=null && (blocked==SalvageWords.Off || blocked==SalvageWords.Cannot || blocked==SalvageWords.NoRecipe)) return;
            var row=Object.Instantiate(gui.m_recipeElementPrefab,gui.m_recipeListRoot);
            row.name="Kindle_SalvageRow";row.SetActive(true);
            var icon=row.transform.Find("icon")?.GetComponent<Image>();
            if (icon != null) { icon.sprite=item.GetIcon();icon.color=Color.white;SalvageRarity.Apply(icon.gameObject,item); }
            var name=row.transform.Find("name")?.GetComponent<TMP_Text>();
            if (name != null) { name.text=ItemNames.DisplayName(item)+(item.m_stack>1?" ×"+item.m_stack:"")+ChestLabel(chest);name.color=blocked==null?Color.white:BlockedName; }
            var quality=row.transform.Find("QualityLevel")?.GetComponent<TMP_Text>();
            if (quality != null) { quality.gameObject.SetActive(item?.m_shared != null && item.m_shared.m_maxQuality>1);quality.text=item.m_quality.ToString(); }
            var bar=row.transform.Find("Durability")?.GetComponent<GuiBar>();
            if (bar != null) { bool worn=item?.m_shared != null && item.m_shared.m_useDurability && item.m_durability<item.GetMaxDurability();bar.gameObject.SetActive(worn);if(worn)bar.SetValue(item.GetDurabilityPercentage()); }
            var button=row.GetComponent<Button>();
            int index=rows.Count;
            if (button != null) { button.onClick=new Button.ButtonClickedEvent();button.onClick.AddListener(()=>Choose(index,false)); }
            string tip=ItemDrop.ItemData.GetTooltip(item,item.m_quality,false,Game.m_worldLevel);
            if (chest != null) tip="<color="+ChestColour+">"+SalvageWords.FromChest+"</color>\n\n"+tip;
            KitTooltip.Set(row,ItemNames.DisplayName(item),blocked==null?tip:"<color=#FF6A5A>"+blocked+"</color>\n\n"+tip);
            rows.Add(new KeyValuePair<ItemDrop.ItemData,GameObject>(item,row));
            if (chest != null) chestOf[item]=chest;
        }
        /// <summary>"From a chest", small and in the Reach container color, after the name of a chest row; empty otherwise.</summary>
        static string ChestLabel(Container chest) => chest==null?"":"  <size=80%><color="+ChestColour+">"+SalvageWords.FromChest+"</color></size>";
        static void Clear()
        {
            foreach (var row in rows) if (row.Value != null) { row.Value.SetActive(false);Object.Destroy(row.Value); }
            rows.Clear();chestOf.Clear();
        }
        static void Choose(int index,bool center)
        {
            selected=null;drawGeneration++;
            for(int i=0;i<rows.Count;i++)
            {
                var marker=rows[i].Value.transform.Find("selected");if(marker!=null)marker.gameObject.SetActive(i==index);
                if(i==index){selected=rows[i].Key;selectedIndex=i;}
            }
            if(center && selected!=null && gui.m_recipeEnsureVisible!=null)gui.m_recipeEnsureVisible.CenterOnItem(rows[index].Value.transform as RectTransform);
            if(gui.m_uiGroups!=null && gui.m_uiGroups.Length>3)gui.SetActiveGroup(gui.m_uiGroups[3]);
            if(GamepadRumble.instance!=null)GamepadRumble.instance.PlayGlobalSelectVibration();
        }
        public static bool Gamepad(InventoryGui instance)
        {
            if(!Active)return true;
            int step=ZInput.GetButtonDown("JoyLStickDown")||ZInput.GetButtonDown("JoyDPadDown")?1:ZInput.GetButtonDown("JoyLStickUp")||ZInput.GetButtonDown("JoyDPadUp")?-1:0;
            if(step!=0 && rows.Count>0)Choose(Mathf.Clamp(selectedIndex+step,0,rows.Count-1),true);
            return false;
        }
        public static bool Craft(InventoryGui instance)
        {
            if(!Active)return true;
            if(instance.m_craftTimer>=0f || selected==null)return false;
            var actor=Player.m_localPlayer;
            Action done=()=> { if(instance!=null && actor==Player.m_localPlayer && Ready) { instance.m_craftItemEffects.Create(actor.transform.position,Quaternion.identity);instance.UpdateCraftingPanel(); } };
            if(chestOf.TryGetValue(selected,out var chest))SalvageChests.Confirm(actor,chest,selected,done);
            else SalvageActions.Confirm(actor,selected,done);
            return false;
        }
        // Shift, Ctrl and Alt count only when both the game's input and Unity's old Input say held: either can keep a
        // modifier let go in another window (Alt+Tab), as fixed in OpenKeep 4.0.0 / ValheimModLibs Hotkey.KeyHeld (9da28eb5).
        public static void Hotkey(InventoryGui instance)
        {
            if(!Ready || !SalvageSettings.Enabled.Value || !InventoryGui.IsVisible() || UnifiedPopup.IsVisible() || instance.m_dragItem!=null || !MeadworksInput.Allows(InputContext.Window))return;
            var shortcut=Plugin.Settings.Shortcut(KindleSettings.Salvage.SalvageKey);
            if(!HotkeyRules.Pressed(MeadworksInput.Chord(shortcut),MeadworksInput.Typing,
                key=>Enum.TryParse(key,out KeyCode code)&&Input.GetKeyDown(code),
                key=>Enum.TryParse(key,out KeyCode code)&&(HotkeyRules.ReadsThroughGame(key)?ZInput.GetKey(code,false)&&Input.GetKey(code):Input.GetKey(code))))return;
            var grid=instance.m_playerGrid;
            var pointer=ZInput.pointerPosition;
            var item=grid!=null?grid.GetItem(new Vector2i((int)pointer.x,(int)pointer.y)):null;
            if(item==null && grid!=null && ZInput.IsGamepadActive())item=grid.GetGamepadSelectedItem();
            if(item!=null)SalvageActions.Confirm(Player.m_localPlayer,item,()=>{if(instance!=null && Ready)instance.UpdateCraftingPanel();});
        }
        /// <summary>
        /// Salvage tab: the game's recipe update is skipped. With no recipe selected it would hide the name, description and
        /// requirement rows every frame for <see cref="Fill"/> to show them again. Only its tail runs here: no craft is in
        /// progress while the tab is open (Select and Fill require that), so the progress bar stays hidden and Craft shown.
        /// </summary>
        public static bool BeforeRecipe(InventoryGui instance)
        {
            if(!Active || instance!=gui || instance.m_craftTimer>=0f)return true;
            if(instance.m_craftProgressPanel!=null && instance.m_craftProgressPanel.gameObject.activeSelf)instance.m_craftProgressPanel.gameObject.SetActive(false);
            if(!instance.m_craftButton.gameObject.activeSelf)instance.m_craftButton.gameObject.SetActive(true);
            return false;
        }
        readonly struct DrawKey : IEquatable<DrawKey>
        {
            readonly ItemDrop.ItemData item;readonly CraftingStation station;readonly int stack,quality,changes,second,generation;
            public DrawKey(ItemDrop.ItemData item,CraftingStation station,int stack,int quality,int changes,int second,int generation)
            {this.item=item;this.station=station;this.stack=stack;this.quality=quality;this.changes=changes;this.second=second;this.generation=generation;}
            public bool Equals(DrawKey other)=>ReferenceEquals(item,other.item) && ReferenceEquals(station,other.station) && stack==other.stack
                && quality==other.quality && changes==other.changes && second==other.second && generation==other.generation;
            public override bool Equals(object obj)=>obj is DrawKey other && Equals(other);
            public override int GetHashCode()=>HashCode.Combine(stack,quality,changes,second,generation);
        }
        /// <summary>
        /// Draws the chosen item, its returns and the Salvage button's state only when one of them can have changed:
        /// another item or stack, an inventory change, another station, a settings change or selection, or the next
        /// second (the returns page and the station range). Otherwise the labels, item tooltip and row tooltips stay drawn.
        /// </summary>
        public static void Fill(InventoryGui instance,Player player)
        {
            if(!Active || player==null || instance==null || instance.m_craftTimer>=0f)return;
            var item=selected;
            Container chest=null;
            bool fromChest=item!=null && chestOf.TryGetValue(item,out chest);
            bool present=item!=null && (fromChest?chest && chest.GetInventory().ContainsItem(item):player.GetInventory().ContainsItem(item));
            var key=new DrawKey(present?item:null,player.GetCurrentCraftingStation(),present?item.m_stack:0,present?item.m_quality:0,
                KindleBatch.InventoryChanges,(int)Time.fixedTime,drawGeneration);
            // Inscribe's UpdateRecipe postfix still runs while the game's update is skipped and, with no recipe selected, clears the
            // rarity backdrop on the detail icon every frame. Re-applying only the backdrop each frame (cheap; Fill runs last)
            // keeps it steady; before, it showed for one frame per redraw and flickered once a second.
            if(key.Equals(drawn)){if(present)SalvageRarity.Apply(instance.m_recipeIcon.gameObject,item);return;}
            drawn=key;
            Header(instance,player);
            var caption=instance.m_craftButton.GetComponentInChildren<TMP_Text>();if(caption!=null)caption.text=SalvageWords.Salvage;
            if(!present)
            {
                instance.m_recipeIcon.enabled=false;instance.m_recipeName.enabled=false;
                foreach(var slot in instance.m_recipeRequirementList)InventoryGui.HideRequirement(slot.transform);
                instance.m_recipeDecription.enabled=true;instance.m_recipeDecription.text=SalvageWords.Empty;instance.m_craftButton.interactable=false;
                KitTooltip.Set(instance.m_craftButton.gameObject,SalvageWords.Salvage,"");return;
            }
            instance.m_recipeIcon.enabled=true;instance.m_recipeIcon.sprite=item.GetIcon();SalvageRarity.Apply(instance.m_recipeIcon.gameObject,item);
            instance.m_recipeName.enabled=true;instance.m_recipeName.text=ItemNames.DisplayName(item)+(item.m_stack>1?" ×"+item.m_stack:"")+ChestLabel(fromChest?chest:null);
            instance.m_recipeDecription.enabled=true;instance.m_recipeDecription.text=KitLocalize.Text(ItemDrop.ItemData.GetTooltip(item,item.m_quality,false,Game.m_worldLevel));
            var returns=SalvageActions.Returns(item);var slots=instance.m_recipeRequirementList;
            int start=slots.Length>0 && returns.Count>slots.Length?(int)Time.fixedTime%Mathf.CeilToInt(returns.Count/(float)slots.Length)*slots.Length:0;
            int shown=0;
            for(int i=start;i<returns.Count && shown<slots.Length;i++,shown++)
            {
                var root=slots[shown].transform;var entry=returns[i];var icon=root.Find("res_icon").GetComponent<Image>();var name=root.Find("res_name").GetComponent<TMP_Text>();var amount=root.Find("res_amount").GetComponent<TMP_Text>();
                icon.gameObject.SetActive(true);name.gameObject.SetActive(true);amount.gameObject.SetActive(true);icon.sprite=entry.Item.m_itemData.GetIcon();icon.color=Color.white;
                name.text=KitLocalize.Text(entry.Name);amount.text=entry.IsCertain?entry.Amount.ToString():SalvageActions.Percent(entry.Chance);amount.color=Color.white;KitTooltip.Set(root.gameObject,name.text,"");
            }
            for(;shown<slots.Length;shown++)InventoryGui.HideRequirement(slots[shown].transform);
            string blocker=SalvageRules.Blocker(player,item,fromChest?chest.GetInventory():null);if(blocker==null && returns.Count==0)blocker=SalvageWords.Nothing;
            // A chest item has no room check: what does not fit drops at the player's feet.
            if(blocker==null && !fromChest && !SalvageInventory.Fits(player.GetInventory(),item,returns))blocker=SalvageWords.NoFit;
            if(blocker==null && fromChest && SalvageChests.Busy)blocker=SalvageWords.ChestWait;
            instance.m_craftButton.interactable=blocker==null;KitTooltip.Set(instance.m_craftButton.gameObject,SalvageWords.Salvage,blocker??"");
        }
        /// <summary>What the skipped game update keeps right on the Salvage tab: the station header, and no leftover craft-recipe parts.</summary>
        static void Header(InventoryGui instance,Player player)
        {
            var station=player.GetCurrentCraftingStation();
            if(station!=null)
            {
                instance.m_craftingStationName.text=Localization.instance.Localize(station.m_name);
                instance.m_craftingStationIcon.gameObject.SetActive(true);instance.m_craftingStationIcon.sprite=station.m_icon;
                instance.m_craftingStationLevel.text=station.GetLevel().ToString();instance.m_craftingStationLevelRoot.gameObject.SetActive(true);
            }
            else
            {
                instance.m_craftingStationName.text=Localization.instance.Localize("$hud_crafting");
                instance.m_craftingStationIcon.gameObject.SetActive(false);instance.m_craftingStationLevelRoot.gameObject.SetActive(false);
            }
            if(instance.m_qualityPanel!=null)instance.m_qualityPanel.gameObject.SetActive(false);
            instance.m_minStationLevelIcon.gameObject.SetActive(false);instance.m_variantButton.gameObject.SetActive(false);instance.m_itemCraftType.gameObject.SetActive(false);
        }
        public static void Shutdown()
        {
            bool active=Active;initialized=false;Deselect();
            if(Plugin.Settings!=null) Plugin.Settings.Changed-=Changed;
            configurationDirty=false;
            patches?.UnpatchSelf();patches=null;
            layout?.Forget();layout=null;tab?.Dispose();tab=null;
            SalvageYaml.Shutdown();SalvageRarity.Shutdown();SalvageChests.Shutdown();selected=null;warning=null;warned.Clear();
            if(active && gui!=null && Player.m_localPlayer!=null)gui.OnTabCraftPressed();
            gui=null;
        }
    }
}
