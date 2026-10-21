using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mantle.Accessories;
using Mantle.Backpacks;
using Mantle.Crafting;
using Mantle.Layout;
using Mantle.Look;
using Mantle.Panels;
using Mantle.Ring;
using Mantle.Slots;
using Mantle.Tackle;
using ExtraPotions.Mantle.Embedded.SyncedConfig;

namespace Mantle.Core
{
    /// <summary>
    /// Binds and wires everything of the player's own inventory: a bigger grid, labelled slots beside it (worn gear, a
    /// backpack, up to five worn utilities, food, mead, ammo and a coin purse), a key ring, a tacklebox, the backpacks and
    /// the look.
    /// The slots are cells of the game's own inventory (<see cref="InventoryLayout"/>); the game's grid draws them and
    /// <see cref="SlotElements"/> moves them into the slot panel. A changed layout setting (or FeastMaster's food count)
    /// re-lays the inventory out on the next frame.
    /// </summary>
    public static class InventoryModule
    {
        private static bool pending;
        private static readonly List<Action> releases = new List<Action>();
        internal static bool LayoutPending => pending;

        /// <summary>Lays the inventory out again on the next frame (the accessory registry changed which items belong in which slot).</summary>
        public static void RequestLayout() => pending = true;

        public static void Initialize(SyncedConfiguration synced)
        {
            InventorySettings.Bind(synced);
            Words.Register();
            BackpackWords.Register();
            TackleboxWords.Register();
            AccessoryWords.Register();
            Consume.ConsumeWords.Register();
            Panels.StatTipWords.Register();
            BackpacksFile.Initialize(synced);
            TackleboxesFile.Initialize(synced);
            AccessoriesFile.Initialize(synced);
            CraftRecipes.Watch();
            WatchLayoutKeys();
            KeyStacks.Watch();
            FoodCount.Changed += FoodCountChanged;
            EliteEquipmentLink.BootsChanged += BootsChanged;
            Subscribe(InventorySettings.BrownStyle, (sender, args) => PanelSize.Refresh());
            Subscribe(InventorySettings.Enabled, (sender, args) => PanelSize.Refresh());   // the look goes with it
            Subscribe(InventorySettings.SlotLabels, (sender, args) => SlotElements.Invalidate());
            Subscribe(InventorySettings.SlotIcons, (sender, args) => SlotElements.Invalidate());
        }

        /// <summary>Called every frame of the local player (<see cref="PlayerTick"/>): binds a late ready character or applies a layout change waiting from the settings.</summary>
        public static void ApplyPending(Player player)
        {
            if (player == null || player != Player.m_localPlayer)
                return;
            if (!Plugin.WorldReady)
            {
                // Forget only runtime binding; saved records still determine rollback after readiness.
                InventoryState.Set(null, null);
                pending = false;
                return;
            }
            if (InventoryState.Player != player)
            {
                InventoryState.Set(null, null);
                pending = false;
            }
            if (player.IsDead() || player.IsTeleporting())
                return;
            FoodCount.Tick();
            bool needsBinding = InventoryState.Player != player || InventoryState.Layout == null;
            if (!pending && !needsBinding)
                return;
            // Wanted includes disabled modules with a saved layout record to undo.
            // Apply is the ordinary settled-player migration, never the temporary load-width path.
            if (LayoutApply.Wanted(player) && !CountRefusal.Refused(player))   // the user, 2026-10-12: a lower count never drops items
                LayoutApply.Apply(player, dropOverflow: true);
            pending = false;
        }

        /// <summary>EliteEquipment's Separate Boots changed: the Feet slot comes or goes.</summary>
        private static void BootsChanged() => pending |= InventorySettings.EquipmentSlots.Value;

        private static void FoodCountChanged() => pending |= InventorySettings.FoodSlotsFollowEating.Value && !SlotCounts.Shared;

        public static void Shutdown()
        {
            FoodCount.Changed -= FoodCountChanged;
            EliteEquipmentLink.BootsChanged -= BootsChanged;
            foreach (Action release in releases)
                release();
            releases.Clear();
            CraftRecipes.Unwatch();
            KeyStacks.Unwatch();
            AccessoriesFile.Reset();
            FoodCount.Shutdown();
            Consume.ConsumeBar.Shutdown();
        }

        private static void WatchLayoutKeys()
        {
            Watch(InventorySettings.Enabled);
            Watch(InventorySettings.InventoryWidth);
            Watch(InventorySettings.InventoryRows);
            Watch(InventorySettings.EquipmentSlots);
            Watch(InventorySettings.SlotsPerGroup);
            Watch(InventorySettings.UtilitySlots);
            Watch(InventorySettings.TrinketSlot);
            Watch(InventorySettings.BackpackSlot);
            Watch(InventorySettings.RingSlots);
            Watch(InventorySettings.BeltSlot);
            Watch(InventorySettings.FoodSlots);
            Watch(InventorySettings.FoodSlotsFollowEating);
            Watch(InventorySettings.MeadSlots);
            Watch(InventorySettings.AmmoSlots);
            Watch(InventorySettings.CoinPurse);
            Watch(KeyRingSettings.KeyRing);
            Watch(KeyRingSettings.KeyItems);
            Watch(TackleboxSettings.Enabled);
            Watch(TackleboxSettings.TackleItems);
        }

        private static void Watch<T>(ConfigEntry<T> entry) => Subscribe(entry, (sender, args) => pending = true);

        /// <summary>A setting handler that <see cref="Shutdown"/> takes off again, so a reload leaves none behind.</summary>
        private static void Subscribe<T>(ConfigEntry<T> entry, EventHandler handler)
        {
            entry.SettingChanged += handler;
            releases.Add(() => entry.SettingChanged -= handler);
        }
    }
}
