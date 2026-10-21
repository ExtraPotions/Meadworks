using System.Collections.Generic;
using Mantle.Core;

namespace Mantle.Ring
{
    /// <summary>
    /// Which keys this character has ever carried, so a key it never had before is announced once (<see cref="KeyRingNotice"/>),
    /// the first time it arrives however it came: picked up, bought, crafted or taken from a chest (the user's request,
    /// 2026-09-28). The set of key prefab names is saved with the character (custom data <c>Mantle.keysSeen</c>, comma
    /// separated), so taking keys back from a grave, or finding a key again after using the last one, announces nothing.
    /// A character without the record starts with every key it carries then, silently. The set is also kept in memory for
    /// the character being played, so a new body after death (whose data may predate the last save) keeps it. Read in
    /// the local player's frame while the ring is active, over the whole inventory: a stack above Key Stack spills into
    /// the grid.
    /// </summary>
    public static class KeyRingNews
    {
        public const string Key = "Mantle.keysSeen";
        private static readonly HashSet<string> seen = new HashSet<string>();
        private static PlayerProfile seenProfile;
        private static Player seenFor;

        public static void Tick(Player player)
        {
            if (!InventoryState.IsLocal(player) || !KeyRing.Active)
                return;
            if (player != seenFor)
                Follow(player);
            bool added = false;
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (!KeyRing.IsKey(item) || !seen.Add(ItemNames.PrefabName(item)))
                    continue;
                KeyRingNotice.Add(item);
                added = true;
            }
            if (added)
                Save(player);
        }

        /// <summary>A new body: the same character keeps the set (merged with the body's record), another starts from its own.</summary>
        private static void Follow(Player player)
        {
            PlayerProfile profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (profile != seenProfile)
            {
                seenProfile = profile;
                seen.Clear();
                KeyRingNotice.Clear();
            }
            seenFor = player;
            if (player.m_customData.TryGetValue(Key, out string record))
                Merge(record);
            else if (seen.Count == 0)
                Seed(player);
            Save(player);
        }

        private static void Merge(string record)
        {
            foreach (string prefab in record.Split(','))
            {
                if (prefab.Length > 0)
                    seen.Add(prefab);
            }
        }

        /// <summary>A character's first time: every key it carries counts as known, nothing is announced.</summary>
        private static void Seed(Player player)
        {
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (KeyRing.IsKey(item))
                    seen.Add(ItemNames.PrefabName(item));
            }
        }

        private static void Save(Player player) => player.m_customData[Key] = string.Join(",", seen);
    }
}
