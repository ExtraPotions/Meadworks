// Adapted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Look/SkinArt.cs, GamePanelTheme.cs, TimberBackground.cs, Panels/TabButtons.cs,
// Panels/StatRows.cs, Panels/GearStats.cs, Panels/SheetScroll.cs, Panels/Divider.cs, Panels/BlockedCell.cs and ValheimModLibs/PlateColumn/TipTemplate.cs,
// SkinPalette.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: theme switches, art slicing, sprite-name rules and colour
// constants collected in one Unity-free place; resource prefix and marker names generalised from PackPanel to Crucible.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>The kit's two looks, in PackPanel's <c>InventoryTheme</c> order.</summary>
    public enum KitThemeKind { Brown, Timber }

    /// <summary>
    /// Whether the kit also re-dresses the game's own wood panels (crafting, character dialogs, settings): PackPanel
    /// always does with Timber on; in Meadworks the product that owns the inventory (Mantle) asks for it.
    /// </summary>
    public enum KitGamePanels { Auto, Always, Never }

    /// <summary>MeadWorks styling restyles native and MeadWorks UI only; other mods' UI is foreign and left alone.</summary>
    public enum KitUiOwner { Native, Meadworks, Foreign }

    /// <summary>Ownership facts about one node on an image's parent chain.</summary>
    public readonly struct KitUiNode {
        public KitUiNode(string? name,bool valheim,bool meadworks,bool foreign,bool foreignContainer){Name=name;Valheim=valheim;Meadworks=meadworks;Foreign=foreign;ForeignContainer=foreignContainer;}
        public string? Name { get; }
        /// <summary>Has a component from the game's own assembly_valheim.</summary>
        public bool Valheim { get; }
        /// <summary>Has a component from a MeadWorks assembly.</summary>
        public bool Meadworks { get; }
        /// <summary>Has a component from any other mod.</summary>
        public bool Foreign { get; }
        /// <summary>A root another framework made for its mods' UI (Jotunn's CustomGUIFront/Back).</summary>
        public bool ForeignContainer { get; }
    }

    public static class KitThemeRules {
        /// <summary>The child under a themed image holding its Timber mesh.</summary>
        public const string WoodMarker="Crucible_timberwood";
        /// <summary>A decorative rim, never itself a panel to theme.</summary>
        public const string FrameName="Crucible_frame";

        /// <summary>Timber shows only while the kit look is on (PackPanel: master switch and Brown Style both on).</summary>
        public static bool Timber(bool lookOn,KitThemeKind theme)=>lookOn&&theme==KitThemeKind.Timber;

        public static string PanelArt(bool timber)=>timber?"timber_background":"panel";

        /// <summary>The Timber button when it decoded, else the brown one.</summary>
        public static string ButtonArt(bool timber,bool timberButtonAvailable)=>timber&&timberButtonAvailable?"timber_button":"button";

        /// <summary>One of the game's wood panel sprites; selection masks and the little projecting tabs are not panels.</summary>
        public static bool IsWoodSprite(string? name)=>name!=null
            &&name.StartsWith("woodpanel_",StringComparison.Ordinal)
            &&!name.EndsWith("_mask",StringComparison.Ordinal)
            &&!name.StartsWith("woodpanel_flik",StringComparison.Ordinal);

        /// <summary>An image to dress in Timber: already themed, or a sliced/tiled filled wood panel; never the rim.</summary>
        public static bool IsGamePanel(bool hasWoodMarker,bool slicedOrTiled,bool fillCenter,string? spriteName,string? imageName){
            bool panel=hasWoodMarker||(slicedOrTiled&&fillCenter&&IsWoodSprite(spriteName));
            return panel&&imageName!=FrameName;
        }

        /// <summary>
        /// Who owns an image, read from its own node up to the scene root. A node's facts decide in this order: a
        /// third-party container (Jotunn's CustomGUI roots) is foreign; a MeadWorks name or component is MeadWorks; a
        /// Valheim (assembly_valheim) component is native; any other mod's component is foreign. A chain with no deciding
        /// node is foreign, so images other mods build from plain Unity components are never restyled.
        /// </summary>
        public static KitUiOwner UiOwner(IEnumerable<KitUiNode> fromImageUp){
            foreach(var node in fromImageUp){
                if(node.ForeignContainer)return KitUiOwner.Foreign;
                if(node.Meadworks||IsMeadworksName(node.Name))return KitUiOwner.Meadworks;
                if(node.Valheim)return KitUiOwner.Native;
                if(node.Foreign)return KitUiOwner.Foreign;
            }
            return KitUiOwner.Foreign;
        }

        public static bool IsMeadworksName(string? name)=>name!=null&&(name.StartsWith("Crucible_",StringComparison.Ordinal)||name.StartsWith("Meadworks_",StringComparison.Ordinal));

        public static bool ThemeGamePanels(KitGamePanels setting,bool requested)=>setting==KitGamePanels.Always||setting==KitGamePanels.Auto&&requested;
    }

    /// <summary>A scene load or theme change looks at every image again, a slice per frame rather than all at once.</summary>
    public static class KitScan {
        public const int PerFrame=500;
        public static int End(int scanned,int total)=>Math.Min(total,scanned+PerFrame);
    }

    /// <summary>How one embedded image becomes a sprite: its area in the texture, scale and 9-slice border.</summary>
    public readonly struct KitSlice {
        public KitRect Area { get; }
        public float PixelsPerUnit { get; }
        public float Border { get; }
        public KitSlice(KitRect area,float pixelsPerUnit,float border){Area=area;PixelsPerUnit=pixelsPerUnit;Border=border;}
    }

    /// <summary>
    /// The images the kit embeds once (in Crucible.UI, not in every product) and how each is sliced. The two Timber
    /// images are MeadWorks' own, built from CC0 textures; the finished images are CC BY-NC-SA 4.0, not PackPanel's AI images; the brown frames, ring art and slot icons
    /// are the outputs PackPanel ships of its own drawing scripts. Item icons (backpacks, tackleboxes) stay with Mantle.
    /// </summary>
    public static class KitArtCatalog {
        public const string ResourcePrefix="Crucible.UI.assets.";
        public const string SpritePrefix="Crucible_";
        const float PanelPixelsPerUnit=200f;
        const float PanelBorder=32f;
        const float ButtonBorder=12f;

        public static readonly IReadOnlyList<string> SlotIcons=new ReadOnlyCollection<string>(new[]{
            "ammo","back","backpack","chest","food","head","key","legs","mead","purse","tackle","tacklebox","trinket","utility"});

        public static readonly IReadOnlyList<string> UiArt=BuildArt();

        static IReadOnlyList<string> BuildArt(){
            var names=new List<string>{"timber_background","timber_button","panel","cell","button","button_hover","button_pressed","ring_panel","ring_line","map_frame_large","compass_strip","heading_plate","compass_plank","compass_inset","iron_rivet","window_wood","window_frame","window_inset","button_wood","button_wood_hover","button_wood_pressed","button_wood_disabled","side_plate","dial_frame","empty_boar"};
            foreach(var icon in SlotIcons)names.Add("icon_"+icon);
            return names.AsReadOnly();
        }

        public static string Resource(string name)=>ResourcePrefix+name+".png";
        public static string SpriteName(string name)=>SpritePrefix+name;

        /// <summary>The scale and border PackPanel's <c>SkinArt</c> reads each image with; unknown names are unsliced.</summary>
        public static void Spec(string name,out float pixelsPerUnit,out float border){
            switch(name){
                case "panel":pixelsPerUnit=PanelPixelsPerUnit;border=PanelBorder;return;
                case "timber_button":pixelsPerUnit=100f;border=-5f;return;
                case "map_frame_large":pixelsPerUnit=200f;border=56f;return;
                case "button":case "button_hover":case "button_pressed":pixelsPerUnit=100f;border=ButtonBorder;return;
                default:pixelsPerUnit=100f;border=0f;return;
            }
        }

        public static KitSlice Slice(string name,int width,int height){Spec(name,out var ppu,out var border);return Slice(name,width,height,ppu,border);}

        /// <summary>
        /// PackPanel's <c>Resliced</c>: a negative border keeps 13% corners and expresses their thickness in UI units; the
        /// Timber button drops its transparent source margins and keeps corners a quarter of its height, 5 units thick.
        /// </summary>
        public static KitSlice Slice(string name,int width,int height,float pixelsPerUnit,float border){
            if(width<=0||height<=0)throw new ArgumentException("An image has a positive size.",nameof(width));
            var whole=new KitRect(0f,0f,width,height);
            if(border<0f){
                float units=-border;
                border=KitMath.CeilToInt(KitMath.Min(width,height)*0.13f);
                pixelsPerUnit=border*100f/units;
            }
            if(name=="timber_button"){
                whole=new KitRect(width*(57f/1983f),height*(137f/793f),width*(1870f/1983f),height*(551f/793f));
                border=whole.Height*0.25f;
                pixelsPerUnit=border*100f/5f;
            }
            return new KitSlice(whole,pixelsPerUnit,border);
        }
    }

    /// <summary>Every fixed colour of the kit, as PackPanel 0.12.0 and PlateColumn draw them.</summary>
    public static class KitPalette {
        /// <summary>Timber wallpaper tint on a themed panel.</summary>
        public static readonly KitColor PanelTint=new KitColor(0.92f,0.92f,0.92f,1f);
        public static readonly KitColor TabLitText=new KitColor(1f,0.86f,0.55f,1f);
        public static readonly KitColor TabLit=new KitColor(1f,1f,1f,1f);
        public static readonly KitColor TabDim=new KitColor(0.5f,0.5f,0.5f,1f);
        public static readonly KitColor TabDimText=new KitColor(0.78f,0.74f,0.66f,0.85f);
        public static readonly KitColor SheetLabel=new KitColor(0.96f,0.89f,0.74f,1f);
        public static readonly KitColor SheetValue=new KitColor(1f,0.76f,0.42f,1f);
        public static readonly KitColor SheetHeading=new KitColor(0.79f,0.64f,0.42f,1f);
        /// <summary>The stat sheet's dark rounded inset (PackPanel <c>GearStats</c>).</summary>
        public static readonly KitColor SheetInset=new KitColor(0.05f,0.035f,0.02f,0.45f);
        public static readonly KitColor ScrollTrack=new KitColor(0f,0f,0f,0.25f);
        public static readonly KitColor ScrollHandle=new KitColor(0.55f,0.4f,0.24f,0.9f);
        public static readonly KitColor Divider=new KitColor(0.55f,0.4f,0.24f,0.85f);
        /// <summary>The thin gold border PlateColumn adds to the game's item tooltip.</summary>
        public static readonly KitColor TipBorder=new KitColor(0.85f,0.66f,0.36f,0.9f);
        /// <summary>PlateColumn's light parchment caption colour, #D8C3A0.</summary>
        public static readonly KitColor SkinLabel=KitColor.FromBytes(0xD8,0xC3,0xA0);
        /// <summary>A blocked cell's grey cross (PackPanel <c>BlockedCell</c>).</summary>
        public static readonly KitColor BlockedCross=new KitColor(0.62f,0.6f,0.56f,0.9f);
    }
}
