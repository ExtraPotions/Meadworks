using Burrow.Core;

namespace Burrow.Stow
{
    /// <summary>The "$burrow_stow..." words of the module. <see cref="Register"/> hands them to the game's localization;
    /// <see cref="Format"/> localizes a word with placeholders and fills them.</summary>
    public static class StowWords
    {
        public const string QuickStack = "$burrow_stow_quickstack";
        public const string StoreAll = "$burrow_stow_storeall";
        public const string TopUp = "$burrow_stow_topup";
        public const string Sort = "$burrow_stow_sort";
        public const string Trash = "$burrow_stow_trash";
        public const string Edit = "$burrow_stow_edit";
        public const string Off = "$burrow_stow_off";
        public const string Moved = "$burrow_stow_moved";
        public const string MovedTo = "$burrow_stow_moved_to";
        public const string Nothing = "$burrow_stow_nothing";
        public const string NoContainer = "$burrow_stow_nocontainer";
        public const string NearbyOff = "$burrow_stow_nearby_off";
        public const string NoRoute = "$burrow_stow_noroute";
        public const string Routed = "$burrow_stow_routed";
        public const string RoutedMore = "$burrow_stow_routed_more";
        public const string StoredOne = "$burrow_stow_storedone";
        public const string ToppedUp = "$burrow_stow_toppedup";
        public const string ToppedUpFrom = "$burrow_stow_toppedup_from";
        public const string Sorted = "$burrow_stow_sorted";
        public const string FavouriteOn = "$burrow_stow_favorite_on";
        public const string FavouriteOff = "$burrow_stow_favorite_off";
        public const string SlotOn = "$burrow_stow_slot_on";
        public const string SlotOff = "$burrow_stow_slot_off";
        public const string JunkOn = "$burrow_stow_junk_on";
        public const string JunkOff = "$burrow_stow_junk_off";
        public const string FeedSourceOn = "$burrow_stow_feedsource_on";
        public const string FeedSourceOff = "$burrow_stow_feedsource_off";
        public const string FeedSourcePrivate = "$burrow_stow_feedsource_private";
        public const string FavouriteKept = "$burrow_stow_favorite_kept";
        public const string EquippedKept = "$burrow_stow_equipped_kept";
        public const string TrashAsk = "$burrow_stow_trash_ask";
        public const string DestroyJunkAsk = "$burrow_stow_destroy_junk_ask";
        public const string Trashed = "$burrow_stow_trashed";
        public const string NoJunk = "$burrow_stow_nojunk";
        public const string DragHint = "$burrow_stow_drag_hint";
        public const string Found = "$burrow_stow_found";
        public const string NotFound = "$burrow_stow_notfound";
        public const string NoCycle = "$burrow_stow_nocycle";
        public const string Salvaged = "$burrow_stow_salvaged";
        public const string TrashModeOn = "$burrow_stow_trash_mode";

        public static void Register()
        {
            Language.Add("burrow_stow_quickstack", "Quick stack");
            Language.Add("burrow_stow_storeall", "Store all");
            Language.Add("burrow_stow_topup", "Top up");
            Language.Add("burrow_stow_sort", "Sort");
            Language.Add("burrow_stow_trash", "Trash");
            Language.Add("burrow_stow_edit", "Edit stow rules");
            Language.Add("burrow_stow_off", "Burrow stow is disabled");
            Language.Add("burrow_stow_moved", "Moved {0} stacks");
            Language.Add("burrow_stow_moved_to", "Moved {0} stacks to {1}");
            Language.Add("burrow_stow_nothing", "Nothing to move");
            Language.Add("burrow_stow_nocontainer", "No container is open");
            Language.Add("burrow_stow_nearby_off", "Quick stacking to nearby containers is disabled");
            Language.Add("burrow_stow_noroute", "No nearby container takes {0}");
            Language.Add("burrow_stow_routed", "Sent {0} to {1}");
            Language.Add("burrow_stow_routed_more", "Sent {0} to {1} and {2} more");
            Language.Add("burrow_stow_storedone", "Stored one {0}");
            Language.Add("burrow_stow_toppedup", "Topped up {0} items");
            Language.Add("burrow_stow_toppedup_from", "Topped up {0} items from {1}");
            Language.Add("burrow_stow_sorted", "Sorted {0} stacks");
            RegisterMarks();
            RegisterTrash();
        }

        private static void RegisterMarks()
        {
            Language.Add("burrow_stow_favorite_on", "{0} is now a favorite");
            Language.Add("burrow_stow_favorite_off", "{0} is no longer a favorite");
            Language.Add("burrow_stow_slot_on", "Slot {0} is now a favorite");
            Language.Add("burrow_stow_slot_off", "Slot {0} is no longer a favorite");
            Language.Add("burrow_stow_junk_on", "{0} is marked as junk");
            Language.Add("burrow_stow_junk_off", "{0} is no longer marked as junk");
            Language.Add("burrow_stow_feedsource_on", "{0} is now a feed source");
            Language.Add("burrow_stow_feedsource_off", "{0} is no longer a feed source");
            Language.Add("burrow_stow_feedsource_private", "Personal Chests cannot be feed sources");
            Language.Add("burrow_stow_favorite_kept", "{0} is a favorite and stays");
            Language.Add("burrow_stow_equipped_kept", "{0} is equipped and stays");
            Language.Add("burrow_stow_found", "{0} in {1} nearby containers");
            Language.Add("burrow_stow_notfound", "No nearby container holds {0}");
            Language.Add("burrow_stow_nocycle", "No other container within reach");
        }

        private static void RegisterTrash()
        {
            Language.Add("burrow_stow_trash_ask", "Destroy {0}?");
            Language.Add("burrow_stow_destroy_junk_ask", "Destroy {0} junk stacks?");
            Language.Add("burrow_stow_trashed", "Destroyed {0}");
            Language.Add("burrow_stow_nojunk", "Nothing in the inventory is marked as junk");
            Language.Add("burrow_stow_drag_hint", "Drag a stack onto the trash can to destroy it, or Shift + click the can to click stacks away");
            Language.Add("burrow_stow_salvaged", "Salvaged {0}");
            Language.Add("burrow_stow_trash_mode", "Trash mode: click a stack to destroy it, let go of Shift to stop");
        }

        /// <summary>Localizes a word and fills its {0}, {1} placeholders.</summary>
        public static string Format(string word, params object[] args)
        {
            string text = Language.Localize(word);
            try
            {
                return string.Format(text, args);
            }
            catch (System.FormatException)
            {
                return text;
            }
        }
    }
}
