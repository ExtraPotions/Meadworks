// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Regenerates a dungeon chest's loot on its own slower timer, so a crypt is worth walking back into. The chest
    /// refills only when it has been emptied, only inside a generated room, and only once the timer has passed since
    /// it was last filled - a chest a player is still working through is never topped up under them.
    ///
    /// OWNER ONLY, at the moment the chest loads: the same point the game itself fills a fresh chest, so a returning
    /// player finds it already stocked rather than watching items appear. The fill time lives in the chest's own ZDO,
    /// which the game replicates and saves, so the clock survives a restart and cannot be reset by another machine.
    /// </summary>
    [HarmonyPatch(typeof(Container), "Awake")]
    public static class DungeonLootPatch
    {
        // Every chest, cart and ship hold that loads comes through here: with dungeon loot off, the first test ends it.
        private static void Postfix(Container __instance)
        {
            if (RuleState.Active.Respawn.DungeonLoot)
            {
                Guard.Run("Container.Awake loot regen", static container => Consider(container), __instance);
            }
        }

        private static void Consider(Container container)
        {
            RespawnRules rules = RuleState.Active.Respawn;
            if (!rules.DungeonLoot || container == null || !Eligible(container))
            {
                return;
            }
            ZDO zdo = container.m_nview.GetZDO();
            if (!Due(zdo, rules.DungeonLootDays))
            {
                return;
            }
            Refill(container, zdo);
        }

        private static bool Eligible(Container container)
        {
            return container.m_nview != null && container.m_nview.IsValid() && container.m_nview.IsOwner()
                && container.m_defaultItems != null && container.m_defaultItems.m_drops.Count > 0
                && container.GetInventory() != null && SpawnerRespawn.InDungeon(container) && Empty(container);
        }

        // Container.Awake does not read the stored items - the game first loads them on its next CheckForChanges - so
        // here every chest's inventory still reads empty, a full one included. Load them now with the same call
        // CheckForChanges makes (which then finds the revision unchanged and skips), and judge what is really inside.
        // A chest the game has just filled for the first time is already saved and loaded, and holds its loot.
        private static bool Empty(Container container)
        {
            container.Load();
            return container.GetInventory().NrOfItems() == 0;
        }

        /// <summary>True once the configured world days have passed since the last fill. An unstamped chest is stamped now.</summary>
        private static bool Due(ZDO zdo, float days)
        {
            long ticks = zdo.GetLong(TraitKeys.LootFilledAt, 0L);
            DateTime now = ZNet.instance.GetTime();
            if (ticks == 0L)
            {
                zdo.Set(TraitKeys.LootFilledAt, now.Ticks); // first sight: start its clock, do not refill
                return false;
            }
            return (now - new DateTime(ticks)).TotalSeconds >= days * NetTime.DaySeconds();
        }

        // Adds the chest's own drop table again, exactly as the game does when it first fills it, then restamps the
        // clock and saves so the new contents and the new time replicate together.
        private static void Refill(Container container, ZDO zdo)
        {
            foreach (ItemDrop.ItemData item in container.m_defaultItems.GetDropListItems())
            {
                container.GetInventory().AddItem(item);
            }
            zdo.Set(TraitKeys.LootFilledAt, ZNet.instance.GetTime().Ticks);
            container.Save();
            if (Log.Diagnostics)
            {
                Log.Diag($"{container.m_name}: dungeon loot regenerated");
            }
        }
    }
}
