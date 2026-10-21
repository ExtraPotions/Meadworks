// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the localization keys Threshold uses (tokens meadworks_threshold_<key>, design section 11).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExtraPotions.Threshold.Access;
using ExtraPotions.Threshold.Conversion;
using ExtraPotions.Threshold.Picker;
using ExtraPotions.Threshold.Routing;

namespace ExtraPotions.Threshold.Localization
{
    public readonly struct TextKey
    {
        public TextKey(string key, int arguments)
        {
            Key = key;
            Arguments = arguments;
        }

        public string Key { get; }

        /// <summary>Number of <c>{0}</c>-style placeholders, numbered from 0.</summary>
        public int Arguments { get; }

        public override string ToString() => Key;
    }

    /// <summary>Every key of assets/localization/*.json; the tests hold the catalog to this table.</summary>
    public static class ThresholdTextKeys
    {
        public const string OffNoCrucible = "off_no_crucible";
        public const string OffNoThreshold = "off_no_threshold";
        public const string PortalEditNoReply = "portal_edit_no_reply";

        /// <summary>{0} installed source names.</summary>
        public const string StepAside = "step_aside";

        /// <summary>TeleportEverything installed: Teleport Listed Items and Bring Tames are left to it (T19).</summary>
        public const string TeleportEverythingStepAside = "te_step_aside";

        /// <summary>{0} number of settings, {1} source name.</summary>
        public const string ImportDone = "import_done";
        public const string CheatIgnoreTeleportRestrictions = "cheat_ignore_teleport_restrictions";

        /// <summary>Walking in opens the map picker (U1).</summary>
        public const string HoverWalkIn = "hover_walk_in";

        /// <summary>{0} key.</summary>
        public const string HoverSetTag = "hover_set_tag";

        /// <summary>{0} key, {1} access mode name.</summary>
        public const string HoverCycleMode = "hover_cycle_mode";

        /// <summary>{0} hub name.</summary>
        public const string HoverHub = "hover_hub";

        public const string PickerTeleport = "picker_teleport", PickerCancel = "picker_cancel", PickerUnnamed = "picker_unnamed", HoverTarget = "hover_target";
        public const string PickerTitle = "picker_title";
        public const string PickerSearch = "picker_search";
        public const string PickerTravel = "picker_travel";

        /// <summary>{0} hub name. The map's Return To Hub button (follow-up of 2026-10-08).</summary>
        public const string PickerReturnToHub = "picker_return_to_hub";

        /// <summary>Hub pin action: choose this hub as the player's home hub (Threshold follow-up 2).</summary>
        public const string PickerSetHome = "picker_set_home";

        /// <summary>Hub pin action on the current home hub: forget it (no Return To Hub button until a home hub is set again).</summary>
        public const string PickerClearHome = "picker_clear_home";

        /// <summary>Marker on the home hub's pin and row.</summary>
        public const string PickerHomeHub = "picker_home_hub";
        public const string PickerSetHub = "picker_set_hub";
        public const string PickerClearHub = "picker_clear_hub";
        public const string PickerHubName = "picker_hub_name";
        public const string PickerClose = "picker_close";
        public const string PickerUntagged = "picker_untagged";

        /// <summary>{0} meters.</summary>
        public const string PickerDistance = "picker_distance";
        public const string PickerCarryWarning = "picker_carry_warning";
        public const string PickerEmpty = "picker_empty";

        /// <summary>{0} category name, {1} portal name (PotalMap's "Category - Name").</summary>
        public const string PickerFavouriteLabel = "picker_favorite_label";
        public const string PickerToggleFavourite = "picker_toggle_favorite";

        /// <summary>The F8 MeadWorks menu entry that turns Show Portal Pins off (shown while on) or on (shown while off).</summary>
        public const string MenuPortalPinsOn = "menu_portal_pins_on";
        public const string MenuPortalPinsOff = "menu_portal_pins_off";
        public const string MapYouAreHere = "map_you_are_here", PickerMapPinsHint = "picker_map_pins_hint";

        /// <summary>{0} amount, {1} item name.</summary>
        public const string FuelNotEnough = "fuel_not_enough";
        public const string CarryBlocked = "carry_blocked";

        /// <summary>Bring Carts: the pulled cart's load may not pass this portal (the trip is refused), or the cart stays because
        /// another player has it.</summary>
        public const string CartBlocked = "cart_blocked";
        public const string CartStays = "cart_stays";

        /// <summary>Sea gate picker (T14): title, empty list, the sail-on button, the crew's line ({0} helmsman's name) and the
        /// helmsman's name when unknown.</summary>
        public const string SgPickerTitle = "sg_picker_title";
        public const string SgPickerEmpty = "sg_picker_empty";
        public const string SgSailOn = "sg_sail_on";
        public const string SgCrewWait = "sg_crew_wait";
        public const string SgHelmsman = "sg_helmsman";

        /// <summary>The sea gate words of the modules adapted from Wayfare (T14): the Wayfare key without its <c>wf_</c>
        /// prefix, with its placeholder count ({0} found, {1} limit, in meters).</summary>
        private static readonly TextKey[] SeaGateWords =
        {
            new TextKey("sg_pillar", 0), new TextKey("sg_pillar_desc", 0), new TextKey("sg_default_name", 0), new TextKey("sg_unpaired", 0),
            new TextKey("sg_hover_rename", 0), new TextKey("sg_hover_sailin", 0), new TextKey("sg_rename", 0), new TextKey("sg_pair_ok", 0),
            new TextKey("sg_pair_far", 2), new TextKey("sg_pair_close", 2), new TextKey("sg_pair_height", 2), new TextKey("sg_pair_nowater", 0),
            new TextKey("sg_pair_shallow", 2), new TextKey("sg_pair_deep", 2), new TextKey("sg_paired", 0), new TextKey("sg_fix_hint", 0),
            new TextKey("sg_denied_gone", 0), new TextKey("sg_denied_cargo", 0), new TextKey("sg_denied_notready", 0),
            new TextKey("sg_denied_nohelm", 0), new TextKey("sg_fallback", 0), new TextKey("sg_denied_closed", 0),
            new TextKey("sg_denied_private", 0), new TextKey("sg_denied_admin", 0), new TextKey("sg_notowner", 0),
            new TextKey(SgPickerTitle, 0), new TextKey(SgPickerEmpty, 0), new TextKey(SgSailOn, 0), new TextKey(SgCrewWait, 1), new TextKey(SgHelmsman, 0),
        };

        private static readonly TextKey[] Fixed =
        {
            new TextKey(OffNoCrucible, 0), new TextKey(OffNoThreshold, 0),
            new TextKey(PortalEditNoReply, 0),
            new TextKey(StepAside, 1), new TextKey(TeleportEverythingStepAside, 0), new TextKey(ImportDone, 2), new TextKey(CheatIgnoreTeleportRestrictions, 0),
            new TextKey(HoverWalkIn, 0), new TextKey(HoverSetTag, 1), new TextKey(HoverCycleMode, 2), new TextKey(HoverHub, 1),
            new TextKey(PickerTeleport, 0), new TextKey(PickerCancel, 0), new TextKey(PickerUnnamed, 0), new TextKey(HoverTarget, 0),
            new TextKey(PickerTitle, 0), new TextKey(PickerSearch, 0), new TextKey(PickerTravel, 0), new TextKey(PickerReturnToHub, 1), new TextKey(PickerSetHome, 0), new TextKey(PickerClearHome, 0), new TextKey(PickerHomeHub, 0),
            new TextKey(PickerSetHub, 0),
            new TextKey(PickerClearHub, 0), new TextKey(PickerHubName, 0), new TextKey(PickerClose, 0), new TextKey(PickerUntagged, 0),
            new TextKey(PickerDistance, 1), new TextKey(PickerCarryWarning, 0), new TextKey(PickerEmpty, 0),
            new TextKey(PickerFavouriteLabel, 2), new TextKey(PickerToggleFavourite, 0),
            new TextKey(MenuPortalPinsOn, 0), new TextKey(MenuPortalPinsOff, 0),
            new TextKey(MapYouAreHere, 0), new TextKey(PickerMapPinsHint, 0),
            new TextKey(FuelNotEnough, 2), new TextKey(CarryBlocked, 0), new TextKey(CartBlocked, 0), new TextKey(CartStays, 0),
            new TextKey("picker_search_help", 0),
            new TextKey("picker_destination_help", 0),
            new TextKey("picker_category_help", 0),
            new TextKey("picker_favorite_help", 0),
            new TextKey("picker_home_help", 0),
            new TextKey("picker_source_hub_help", 0),
            new TextKey("picker_return_help", 0),
            new TextKey("picker_close_help", 0),
        };

        public static string Category(PickerCategory category) => "category_" + Snake(category.ToString());

        public static string Mode(AccessMode mode) => "mode_" + Snake(mode.ToString());

        public static string Refusal(HubRefusal refusal) => "refusal_" + Snake(refusal.ToString());

        public static string Refusal(RouteRefusal refusal) => "refusal_" + Snake(refusal.ToString());

        /// <summary>Portal edit refusals (T12b); the hub reasons share their keys with <see cref="HubRefusal"/>.</summary>
        public static string Refusal(EditRefusal refusal) => "refusal_" + Snake(refusal.ToString());

        public static string Note(ImportNote note) => "import_note_" + Snake(note.ToString());

        /// <summary>Argument count of an enum-derived key (only the hub limit carries one: {0} limit).</summary>
        private static int ArgumentsOf(string key) => key == "refusal_limit_reached" ? 1 : 0;

        public static IReadOnlyList<TextKey> All { get; } = Array.AsReadOnly(Fixed.Concat(SeaGateWords)
            .Concat(Enum.GetValues(typeof(PickerCategory)).Cast<PickerCategory>().Select(Category)
                .Concat(Enum.GetValues(typeof(AccessMode)).Cast<AccessMode>().Select(Mode))
                .Concat(Enum.GetValues(typeof(HubRefusal)).Cast<HubRefusal>().Where(r => r != HubRefusal.None).Select(Refusal))
                .Concat(Enum.GetValues(typeof(RouteRefusal)).Cast<RouteRefusal>().Where(r => r != RouteRefusal.None).Select(Refusal))
                .Concat(Enum.GetValues(typeof(EditRefusal)).Cast<EditRefusal>()
                    .Where(r => r != EditRefusal.None && !Enum.IsDefined(typeof(HubRefusal), r.ToString())).Select(Refusal))
                .Concat(Enum.GetValues(typeof(ImportNote)).Cast<ImportNote>().Select(Note))
                .Select(k => new TextKey(k, ArgumentsOf(k))))
            .ToArray());

        private static string Snake(string name)
        {
            var text = new StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && i > 0) text.Append('_');
                text.Append(char.ToLowerInvariant(name[i]));
            }
            return text.ToString();
        }
    }
}
