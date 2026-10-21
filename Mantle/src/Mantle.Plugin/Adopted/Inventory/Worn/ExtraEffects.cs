using System.Collections.Generic;
using Mantle.Core;

namespace Mantle.Worn
{
    /// <summary>
    /// The status effects of the extra utilities: each one's equip effect, and its set effect once the set is complete
    /// (the set count includes them, see <see cref="UtilityEffectPatches"/>). Kept apart from the game's own list of
    /// equipment effects, which the game rebuilds from its fields and would remove ours from; an effect the game also
    /// grants is never removed here. Synced after every game refresh of the equipment effects.
    /// </summary>
    public static class ExtraEffects
    {
        private static readonly HashSet<StatusEffect> added = new HashSet<StatusEffect>();

        public static void Reset() => added.Clear();

        /// <summary>
        /// Every frame: puts back the effect of a worn extra utility or belt that the game took away without refreshing the
        /// equipment (a respawn clears the new character's status effects after it was dressed), so the HUD icon and the
        /// +150 of a kept Megingjord are not lost until the next equipment change. Nothing to do while nothing extra is worn.
        /// </summary>
        public static void Keep(Humanoid humanoid)
        {
            if (ExtraUtilities.Worn.Count == 0 && added.Count == 0)
                return;
            Sync(humanoid);
        }

        public static void Sync(Humanoid humanoid)
        {
            if (!InventoryState.IsLocal(humanoid))
                return;
            HashSet<StatusEffect> wanted = Wanted(humanoid);
            SEMan seman = humanoid.GetSEMan();
            foreach (StatusEffect effect in added)
            {
                if (!wanted.Contains(effect) && !humanoid.m_equipmentStatusEffects.Contains(effect))
                    seman.RemoveStatusEffect(effect.NameHash());
            }
            foreach (StatusEffect effect in wanted)
            {
                if (!seman.HaveStatusEffect(effect.NameHash()))
                    seman.AddStatusEffect(effect, resetTime: false);
            }
            added.Clear();
            added.UnionWith(wanted);
        }

        private static HashSet<StatusEffect> Wanted(Humanoid humanoid)
        {
            HashSet<StatusEffect> wanted = new HashSet<StatusEffect>();
            foreach (ItemDrop.ItemData item in ExtraUtilities.Worn)
            {
                if (!item.m_equipped)
                    continue;   // the game's UnequipItem refreshes the effects before it lets the item go from the list
                if (item.m_shared.m_equipStatusEffect != null)
                    wanted.Add(item.m_shared.m_equipStatusEffect);
                if (humanoid.HaveSetEffect(item))
                    wanted.Add(item.m_shared.m_setStatusEffect);
            }
            return wanted;
        }
    }
}
