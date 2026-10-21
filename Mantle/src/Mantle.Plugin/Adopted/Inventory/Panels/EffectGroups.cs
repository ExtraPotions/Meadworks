using Mantle.Core;

namespace Mantle.Panels
{
    /// <summary>
    /// The stats panel's groups for Epic Loot's effects. Epic Loot gives no category, so the group comes from words in
    /// the effect's type name, checked in this order (the first match wins): skills, defence, offence, movement, health
    /// and stamina, and everything else, which also takes the types other mods add. "AddFrostResistancePercentage" is
    /// defence before offence sees "Frost"; "ModifyJumpStaminaUse" is movement before stamina.
    /// </summary>
    public static class EffectGroups
    {
        public enum Group
        {
            Offence,
            Defence,
            Resources,
            Movement,
            Skills,
            Other,
        }

        /// <summary>The order the groups are listed in.</summary>
        public static readonly Group[] Order = { Group.Offence, Group.Defence, Group.Resources, Group.Movement, Group.Skills, Group.Other };

        private static readonly Group[] Checked = { Group.Skills, Group.Defence, Group.Offence, Group.Movement, Group.Resources };

        private static readonly string[][] Keywords =
        {
            new[] { "Skill", "Learner" },
            new[] { "Resist", "Armor", "Armour", "Block", "Parry", "Avoid", "Reflect", "DamageTaken", "Feint", "Ward" },
            new[] { "Damage", "Attack", "Backstab", "Crit", "Stagger", "Projectile", "Shot", "Lightning", "Steal", "Hunter",
                    "Execut", "Bow", "Knockback", "Pierce", "Slash", "Blunt", "Fire", "Frost", "Poison", "Spirit", "Throw",
                    "Explosi", "Bleed", "Weapon" },
            new[] { "Movement", "Speed", "Jump", "Swim", "Sneak", "RunStamina", "Sprint", "Fall", "Dodge", "Glide" },
            new[] { "Health", "Stamina", "Eitr", "Regen", "Heal", "Food" },
        };

        public static Group Of(string type)
        {
            for (int i = 0; i < Checked.Length; i++)
                foreach (string word in Keywords[i])
                    if (type.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return Checked[i];
            return Group.Other;
        }

        /// <summary>The heading over a group: Epic Loot's name, then the group's.</summary>
        public static string Title(Group group) => Words.StatEpicLoot + " · " + Word(group);

        private static string Word(Group group)
        {
            switch (group)
            {
                case Group.Offence: return Words.StatOffence;
                case Group.Defence: return Words.StatDefence;
                case Group.Resources: return Words.StatResources;
                case Group.Movement: return Words.StatMovement;
                case Group.Skills: return Words.StatSkills;
                default: return Words.StatOther;
            }
        }
    }
}
