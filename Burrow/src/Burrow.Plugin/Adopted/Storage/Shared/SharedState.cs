using Burrow.Core;

namespace Burrow.Shared
{
    /// <summary>
    /// What the local client is doing with a shared chest: the container it has open in the panel without owning
    /// it (the viewer state of SPEC 9.1), the mode, and the name of the player using a chest (ZDO string
    /// <c>Burrow.user</c>, written by the owning client in <see cref="UserNamePatch"/>).
    /// </summary>
    public static class SharedState
    {
        public const string UserKey = "Burrow.user";

        /// <summary>The container the local player views without owning it, or null.</summary>
        public static Container ViewedContainer { get; private set; }

        /// <summary>The effective mode: Full while Multi-User Chests is on, Off while MultiUserChest is loaded, else Shared Chests.</summary>
        public static SharedMode Mode => Burrow.Integration.MultiUserChestYield.Mode;

        public static bool FullMode => Mode == SharedMode.Full;

        /// <summary>The local player has this container open in the panel and does not own it (View or Full).</summary>
        public static bool IsViewing(Container container)
        {
            if (container == null || ViewedContainer != container)
                return false;
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || gui.m_currentContainer != container)
                return false;
            ZNetView view = container.m_nview;
            return view != null && view.IsValid() && !view.IsOwner();
        }

        internal static void BeginView(Container container)
        {
            ViewedContainer = container;
            Plugin.Log.LogDebug($"Burrow: viewing {ContainerScan.PrefabName(container)} used by {UserName(container)}");
        }

        internal static void EndView()
        {
            if (ViewedContainer != null)
                Plugin.Log.LogDebug($"Burrow: no longer viewing {ContainerScan.PrefabName(ViewedContainer)}");
            ViewedContainer = null;
        }

        /// <summary>The name written by the client using the container, or the "another player" word.</summary>
        public static string UserName(Container container)
        {
            ZNetView view = container != null ? container.m_nview : null;
            string name = view != null && view.IsValid() ? view.GetZDO().GetString(UserKey, "") : "";
            return string.IsNullOrEmpty(name) ? Language.Localize(SharedWords.Someone) : name;
        }

        /// <summary>The panel title of a viewed container: "&lt;chest name&gt; (in use by &lt;player&gt;)".</summary>
        public static string Title(Container container)
        {
            Inventory inventory = container != null ? container.GetInventory() : null;
            string name = inventory != null ? Language.Localize(inventory.GetName()) : "";
            return name + " (" + Language.Localize(SharedWords.InUse) + " " + UserName(container) + ")";
        }
    }
}
