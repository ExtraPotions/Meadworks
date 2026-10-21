// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: EliteCreaturesReborn 3.22's Binding mutation, built from its described behavior only.
using ExtraPotions.Wildblood.Adopted.Patches;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Binding: a hit from it that lands roots the struck player for `root time` seconds - they cannot move until the roots
    /// let go - at most once every `cooldown` seconds, so a pack of them cannot hold a player forever. Decided on the
    /// player's own machine after the game resolved the hit (a step of <see cref="HitPatch"/>): a dodged, parried or
    /// harmless hit roots no one, nor does a tamed one. The root is a plain game status effect made at runtime ("Rooted"),
    /// so the HUD shows it with its timer and the game removes it; the game never saves status effects, so a character
    /// saved rooted loads free.
    /// </summary>
    internal static class BindingRoot
    {
        public const string StatusKey = "wildblood_rooted";

        private static readonly int StatusHash = StatusKey.GetStableHashCode();
        private static SE_Stats? _template;
        private static float _nextAt;

        /// <summary>True while <paramref name="player"/> is rooted.</summary>
        public static bool Rooted(Player player) => player.GetSEMan().HaveStatusEffect(StatusHash);

        public static void TryRoot(Struck struck, bool dodged)
        {
            HitData? hit = struck.Hit;
            EliteController? elite = struck.AttackerElite;
            if (hit == null || dodged || !(struck.Victim is Player player) || !struck.Owned || player.IsDead()
                || elite == null || !elite.Ready || !elite.Traits.Has(Mutation.Binding) || struck.Attacker == null
                || struck.Attacker.IsTamed() || ThievingParryPatch.Parried(hit) || hit.GetTotalDamage() <= 0f
                || Time.time < _nextAt)
            {
                return;
            }
            float seconds = Mathf.Clamp(Enhance.Magnitude(elite.Rules, elite.Traits, Mutation.Binding, Fields.RootTime), 0f, 10f);
            if (seconds <= 0f)
            {
                return;
            }
            _nextAt = Time.time + Mathf.Max(seconds, elite.Rules.PowerOf(Mutation.Binding, Fields.Cooldown));
            StatusEffect? status = player.GetSEMan().AddStatusEffect(Template(), resetTime: true);
            if (status != null)
            {
                status.m_ttl = seconds;
            }
        }

        private static SE_Stats Template()
        {
            if (_template == null)
            {
                _template = ScriptableObject.CreateInstance<SE_Stats>();
                _template.name = StatusKey;
                _template.m_name = "Rooted";
                _template.m_tooltip = "Binding roots hold your feet: you cannot move until they let go.";
                _template.m_speedModifier = -1f;
                _template.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            if (_template.m_icon == null)
            {
                _template.m_icon = Icon(); // asked again until the object database can answer
            }
            return _template;
        }

        private static Sprite? Icon()
        {
            ObjectDB db = ObjectDB.instance;
            StatusEffect? tared = db != null ? db.GetStatusEffect("Tared".GetStableHashCode()) : null;
            if (tared != null && tared.m_icon != null)
            {
                return tared.m_icon;
            }
            GameObject? root = db != null ? db.GetItemPrefab("Root") : null;
            ItemDrop? item = root != null ? root.GetComponent<ItemDrop>() : null;
            Sprite[]? icons = item != null ? item.m_itemData.m_shared.m_icons : null;
            return icons != null && icons.Length > 0 ? icons[0] : null;
        }
    }
}
