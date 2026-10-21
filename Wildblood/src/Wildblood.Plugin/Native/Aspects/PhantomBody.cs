// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// What makes a Phantom copy hollow, applied on every machine the moment it resolves as one: it drops nothing, it
    /// leaves no body - its death spawns none of the boss's death effects, so it simply vanishes where it falls - and its
    /// death never counts as the boss's. The boss-defeated key is what the game (and progression mods) read as "this
    /// boss is beaten", so a copy must not carry it. Each is a field on this one instance; the prefab is untouched.
    /// </summary>
    internal static class PhantomBody
    {
        public static void Hollow(Character copy)
        {
            copy.m_defeatSetGlobalKey = "";
            copy.m_dreamCinematic = "";
            copy.m_deathEffects = new EffectList();
            CharacterDrop drop = copy.GetComponent<CharacterDrop>();
            if (drop != null)
            {
                drop.SetDropsEnabled(false);
            }
        }
    }
}
