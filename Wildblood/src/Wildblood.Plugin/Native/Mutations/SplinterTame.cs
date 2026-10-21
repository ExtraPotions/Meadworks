// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// What a tamed Splintering creature hands down to its copies, so a tame that splits stays a tame twice over instead
    /// of turning into two wild creatures at its owner's feet. Read from the dying parent's ZDO in
    /// <see cref="Patches.DeathPatch"/>'s prefix (the postfix sees a reset ZDO) and applied by <see cref="Splitter"/> to
    /// each copy on the machine that instantiates it, which owns it - so every write here is an owner write the ZDO
    /// replicates to every client. Besides the tame itself it carries the player-given name with its author (the game's
    /// name filter keys off the author, so it is kept rather than re-stamped with whoever owned the corpse) and the
    /// player the parent was following, so both copies fall in behind the same player. A wild parent has none: null.
    /// </summary>
    public sealed class SplinterTame
    {
        public string Name = "";
        public string NameAuthor = "";
        public string Follow = "";

        /// <summary>Null for a wild parent, so its copies stay wild.</summary>
        public static SplinterTame? Read(Character parent, ZDO zdo)
        {
            if (!parent.IsTamed())
            {
                return null;
            }
            return new SplinterTame
            {
                Name = zdo.GetString(ZDOVars.s_tamedName),
                NameAuthor = zdo.GetString(ZDOVars.s_tamedNameAuthor),
                Follow = zdo.GetString(ZDOVars.s_follow),
            };
        }

        /// <summary>
        /// Tames a freshly instantiated copy through the game's own path - the one Tameable.Tame takes when a creature
        /// finishes eating its way to tame: MonsterAI.MakeTame, which sets the tamed flag through Character.SetTamed's
        /// owner RPC (it lands locally here, the owner, and writes ZDOVars.s_tamed) and calms the AI and drops its
        /// target. No tamed effect or "tamed" message: the creature was already tame. The name and follow keys are the
        /// ones Tameable itself writes; Tameable.UpdateSavedFollowTarget then re-issues the follow command on its own.
        /// </summary>
        public void Apply(GameObject copy, ZDO zdo)
        {
            MonsterAI ai = copy.GetComponent<MonsterAI>();
            if (ai != null)
            {
                ai.MakeTame();
            }
            else
            {
                copy.GetComponent<Character>().SetTamed(true);
            }
            if (Name.Length > 0)
            {
                zdo.Set(ZDOVars.s_tamedName, Name);
                zdo.Set(ZDOVars.s_tamedNameAuthor, NameAuthor);
            }
            if (Follow.Length > 0)
            {
                zdo.Set(ZDOVars.s_follow, Follow);
            }
        }
    }
}
