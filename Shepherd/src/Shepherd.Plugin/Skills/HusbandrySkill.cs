// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Husbandry;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Skills
{
    /// <summary>
    /// Registers the Husbandry skill through the CS1 skill registry (S06a). The definition comes from Crucible's
    /// GrindstoneSkills map, so the skill is <c>meadworks_husbandry</c>, imports a GrindstoneSkills
    /// <c>grindstone_husbandry</c> level once per character, and publishes <c>meadworks_husbandry_level</c> on the
    /// player ZDO. The pinned GrindstoneSkills Husbandry icon is adopted under its GPL notice (Q5),
    /// with the vanilla boar trophy as fallback; never decoded on a headless process.
    /// </summary>
    public static class HusbandrySkill
    {
        private static Sprite? embedded;
        private static bool triedEmbedded;
        public static SkillDefinition Definition() =>
            GrindstoneSkillsMap.Definition(HusbandryIdentity.ImportFrom, ShepherdCore.ProductId, HusbandryIdentity.Name, HusbandryIdentity.Description);

        public static MeadworksSkill Register()
        {
            var definition = Definition();
            if (definition.Identity != HusbandryIdentity.Identity || definition.PublishKey != HusbandryIdentity.PublishKey)
                throw new InvalidOperationException("Crucible mapped Husbandry to " + definition.Identity + " / " + definition.PublishKey
                    + "; Shepherd expects " + HusbandryIdentity.Identity + " / " + HusbandryIdentity.PublishKey + ".");
            return MeadworksSkills.Register(definition, Icon);
        }

        private static Sprite? Icon()
        {
            if (MeadworksHeadless.IsHeadless) return null;
            if (!triedEmbedded)
            {
                triedEmbedded = true;
                embedded = Adopted.Husbandry.EmbeddedSprite.Load(typeof(HusbandrySkill).Assembly, "Shepherd.skill_husbandry.png", "shepherd_husbandry");
            }
            return embedded != null ? embedded : TrophyIcon();
        }
        public static void ShutdownIcon()
        {
            if (embedded != null)
            {
                var texture = embedded.texture;
                UnityEngine.Object.Destroy(embedded);
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
            embedded = null;
            triedEmbedded = false;
        }

        private static Sprite? TrophyIcon()
        {
            var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("TrophyBoar") : null;
            var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            return drop != null ? drop.m_itemData.GetIcon() : null;
        }
    }
}
