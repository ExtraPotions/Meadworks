using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Burrow.Core;
using ExtraPotions.Burrow.Embedded.PatchGuard;

namespace Burrow.Stacks
{
    /// <summary>
    /// PackPanel's Key Stack (or the MeadWorks inventory owner's, read through <c>meadworks.inventory.keyring</c>): while PackPanel is installed, every prefab in its <c>3. Key Ring / Key Items</c> stacks to
    /// at least its <c>Key Stack</c>. PackPanel leaves the writing to this module when both are installed, so one mod
    /// writes every stack size; it is applied as the key's starting value (<see cref="StackValues"/>), so an
    /// Burrow.Stacks.yml entry or a per item entry for a key still wins, and it holds with this module off too, so no
    /// switch cuts a stack of keys at the next load. A change of either entry, or a server's values arriving, applies
    /// the values again.
    /// </summary>
    public static class PackPanelKeys
    {
        private static bool watching;
        private static float nextCheck;
        private static int seenStack = -1;
        private static IReadOnlyCollection<string> seenItems;
        private static readonly HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static int Over(string prefab, int stack)
        {
            if (!PackPanelLink.TryKeyRing(out int size, out IReadOnlyCollection<string> items))
                return stack;
            Remember(size, items);
            return keys.Contains(prefab) ? Math.Max(stack, size) : stack;
        }

        /// <summary>Called when the item database is ready (every plugin is loaded by then): follows the key ring once.</summary>
        public static void Watch()
        {
            if (watching || !PackPanelLink.Present)
                return;
            watching = true;
            // PackPanel (third party) raises SettingChanged; the MeadWorks owner's key ring is a capability, read by Tick.
            EventHandler apply = Guard.Wrap("packpanel key stack", (_, _) => StackValues.ApplyAll());
            ConfigEntry<int> size = PackPanelLink.PackPanelEntry<int>(PackPanelLink.KeySection, "Key Stack");
            ConfigEntry<string> items = PackPanelLink.PackPanelEntry<string>(PackPanelLink.KeySection, "Key Items");
            if (size != null)
                size.SettingChanged += apply;
            if (items != null)
                items.SettingChanged += apply;
        }

        /// <summary>
        /// Once a second while watching: a changed key ring (the owner's Key Stack or Key Items) applies the values again.
        /// shortcut: polled at 1 Hz because a capability has no change event; a change applies within a second.
        /// </summary>
        public static void Tick(float now)
        {
            if (!watching || now < nextCheck)
                return;
            nextCheck = now + 1f;
            if (!PackPanelLink.TryKeyRing(out int size, out IReadOnlyCollection<string> items))
                return;
            if (Remember(size, items))
                StackValues.ApplyAll();
        }

        /// <summary>Keeps the key names; true when the ring differs from the last one seen (not on the first sighting).</summary>
        private static bool Remember(int size, IReadOnlyCollection<string> items)
        {
            if (ReferenceEquals(items, seenItems) && size == seenStack)
                return false;
            bool same = size == seenStack && seenItems != null && items.Count == keys.Count;
            if (same)
                foreach (string name in items)
                    if (!keys.Contains(name)) { same = false; break; }
            bool first = seenItems == null;
            seenStack = size;
            seenItems = items;
            if (same)
                return false;
            keys.Clear();
            foreach (string name in items)
                if (!string.IsNullOrWhiteSpace(name))
                    keys.Add(name.Trim());
            return !first;
        }
    }
}
