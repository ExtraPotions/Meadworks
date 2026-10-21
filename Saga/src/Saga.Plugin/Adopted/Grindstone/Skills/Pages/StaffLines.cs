// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Skills/Pages/StaffLines.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Lines for what a magic skill's staves do better at the caster's level, read from the game's item database as the
    /// page opens, for the staves the player has had in their inventory (Player.IsKnownMaterial). The game scales two
    /// things of a staff with the level, both through values its prefabs hold, so the numbers are the game's own:
    /// <list type="bullet">
    /// <item>A summon: a SpawnAbility the staff's attack releases, directly or from its projectile's hit
    /// (<see cref="SummonLine"/>).</item>
    /// <item>A shield: an SE_Shield that is the staff's attack status effect or the one its area applies, when it has an
    /// absorb per skill level (SE_Shield.SetLevel: base + per level × level + the world level's share).</item>
    /// </list>
    /// A staff built any other way adds nothing.
    /// </summary>
    internal static class StaffLines
    {
        /// <summary>A line per growing summon and shield of the skill's known staves; false when there is none.</summary>
        public static bool Write(SkillPage page)
        {
            HashSet<Object> seen = new HashSet<Object>();
            int before = page.Entries.Count;
            foreach (ItemDrop.ItemData staff in KnownStaves(page))
                Staff(page, staff, seen);
            return page.Entries.Count > before;
        }

        private static void Staff(SkillPage page, ItemDrop.ItemData staff, HashSet<Object> seen)
        {
            string name = WeaponLines.Name(staff.m_shared.m_name);
            List<GameObject> casts = Casts(staff);
            foreach (GameObject cast in casts)
            {
                SpawnAbility summon = cast.GetComponentInChildren<SpawnAbility>(true);
                if (summon != null && seen.Add(summon))
                    SummonLine.Write(page, name, summon);
            }
            SE_Shield shield = Shield(staff, casts);
            if (shield != null && seen.Add(shield))
                ShieldLine(page, name, shield);
        }

        /// <summary>Every item of the page's skill the player has had in their inventory.</summary>
        private static IEnumerable<ItemDrop.ItemData> KnownStaves(SkillPage page)
        {
            if (ObjectDB.instance == null)
                yield break;
            foreach (GameObject prefab in ObjectDB.instance.m_items)
            {
                ItemDrop item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (item == null || item.m_itemData.m_shared.m_skillType != page.Type)
                    continue;
                if (page.Player.IsKnownMaterial(item.m_itemData.m_shared.m_name))
                    yield return item.m_itemData;
            }
        }

        /// <summary>What the staff's attacks release (projectile, spawn on hit, spawn on trigger), then what those projectiles spawn on a hit.</summary>
        private static List<GameObject> Casts(ItemDrop.ItemData staff)
        {
            List<GameObject> casts = new List<GameObject>();
            foreach (Attack attack in new[] { staff.m_shared.m_attack, staff.m_shared.m_secondaryAttack })
            {
                if (attack == null)
                    continue;
                Add(casts, attack.m_attackProjectile);
                Add(casts, attack.m_spawnOnHit);
                Add(casts, attack.m_spawnOnTrigger);
            }
            for (int i = 0; i < casts.Count; i++)
            {
                Projectile projectile = casts[i].GetComponent<Projectile>();
                if (projectile != null)
                    Add(casts, projectile.m_spawnOnHit);
            }
            return casts;
        }

        private static void Add(List<GameObject> casts, GameObject cast)
        {
            if (cast != null && !casts.Contains(cast))
                casts.Add(cast);
        }

        /// <summary>The growing shield the staff casts: its own attack status effect, or the one an area it releases applies.</summary>
        private static SE_Shield Shield(ItemDrop.ItemData staff, List<GameObject> casts)
        {
            SE_Shield own = Growing(staff.m_shared.m_attackStatusEffect);
            if (own != null)
                return own;
            foreach (GameObject cast in casts)
            {
                Aoe area = cast.GetComponentInChildren<Aoe>(true);
                if (area == null)
                    continue;
                SE_Shield shield = Growing(Effect(area.m_statusEffectIfPlayer));
                if (shield == null)
                    shield = Growing(Effect(area.m_statusEffect));
                if (shield != null)
                    return shield;
            }
            return null;
        }

        private static StatusEffect Effect(string name) =>
            string.IsNullOrEmpty(name) || ObjectDB.instance == null ? null : ObjectDB.instance.GetStatusEffect(name.GetStableHashCode());

        private static SE_Shield Growing(StatusEffect effect) =>
            effect is SE_Shield shield && shield.m_absorbDamagePerSkillLevel > 0f ? shield : null;

        /// <summary>"Shield absorbs 180 damage": what the shield soaks up when cast at the player's level, added up as SE_Shield.SetLevel does.</summary>
        private static void ShieldLine(SkillPage page, string staff, SE_Shield shield)
        {
            float fromLevel = shield.m_absorbDamagePerSkillLevel * page.Level;
            float absorb = shield.m_absorbDamage + fromLevel;
            if (Game.m_worldLevel > 0)
                absorb += shield.m_absorbDamageWorldLevel * Game.m_worldLevel;
            page.Line(SagaText.Format("gs_27c154b6f82fa559", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", absorb)), "Shield",
                SagaText.Format("gs_4cfab0c290a35a48", staff, string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", shield.m_absorbDamage), SkillPage.Number(shield.m_absorbDamagePerSkillLevel), string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", fromLevel)));
        }
    }
}
