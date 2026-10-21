using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Burrow.Embedded.SyncedConfig;

namespace Burrow.Core
{
    /// <summary>
    /// Section "0. Containers": the switches every module applies when it looks at containers. All synced and
    /// covered by the lock; read at use time.
    /// </summary>
    public static class CoreSettings
    {
        public const string Section = "0. Containers";

        public static ConfigEntry<bool> Ships { get; private set; }
        public static ConfigEntry<bool> Carts { get; private set; }
        public static ConfigEntry<bool> PlayerChests { get; private set; }
        public static ConfigEntry<bool> HonourWards { get; private set; }
        public static ConfigEntry<SharedMode> SharedChests { get; private set; }

        public static void Initialize(SyncedConfiguration synced)
        {
            BindSwitches(synced);
            SharedChests = synced.Bind(Section, "Shared Chests", SharedMode.Off,
                "Used only when Multi-User Chests is off (it is on by default, which lets every player use a chest at once). What happens with a chest another player has open. Off: it is skipped, as the game itself refuses to open it. "
                + "View: interacting with it shows its contents read-only, titled '(in use by <player>)', and the panel turns live when the other player closes it. "
                + "Full: as View, and every change (click move, drag and drop, take all, stack all, Burrow's own actions) is sent as a request to the player's client that owns the chest, which applies it or refuses it; nothing is applied speculatively.",
                tags: new object[] { new ConfigDependency(() => Burrow.Integration.MultiUserChestYield.Enabled == null || !Burrow.Integration.MultiUserChestYield.Enabled.Value, () => "Multi-User Chests is on: every player can use a chest at once (the same as Full).") });
        }

        private static void BindSwitches(SyncedConfiguration synced)
        {
            Ships = synced.Bind(Section, "Ships", true,
                "Ship storage counts as a container: crafting, station feeding, quick stacking and every other module may use it.");
            Carts = synced.Bind(Section, "Carts", true,
                "Cart storage counts as a container.");
            PlayerChests = synced.Bind(Section, "Player Chests", false,
                "The Personal Chest (piece_chest_private) counts as a container, for its owner only.");
            HonourWards = synced.Bind(Section, "Honor Wards", true,
                "Containers inside a ward the player may not use are skipped (the game's PrivateArea.CheckAccess, for containers that check wards).");
        }
    }
}
