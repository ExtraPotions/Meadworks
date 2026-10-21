// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Perks/CleanFell.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Clean fell: the stump comes out with the tree. On the tree's owner, right after TreeBase.SpawnLog instantiated the
    /// log and the stump (<see cref="Felling"/>), the feller's level share of Clean Fell At 100 is the chance that the
    /// stump is destroyed the way chopping it would destroy it: Destructible.Destroy plays its m_destroyedEffect, calls
    /// m_onDestroyed, where DropOnDestroyed spawns its drops, and removes it with ZNetScene.Destroy. Every stump in the
    /// game drops two rolls of one item: Wood; Frostwood for the snow pines; Blackwood for the Ashlands trees. None has an
    /// m_spawnWhenDestroyed.
    /// <para>Destroying the stump in the frame it was spawned is safe. This machine created its ZDO (ZNetView.Awake,
    /// CreateNewZDO) and owns it; Awake has run on every component, so DropOnDestroyed has hooked m_onDestroyed; and
    /// Destroy does not wait for Start (only Destructible.Damage ignores the first frame). The game does the same when
    /// TreeBase.Awake destroys a tree whose stored health is spent. The ZDO is destroyed before ZDOMan ever sent it, so
    /// other machines never see the stump: they get the destroyed ZDO's ID, which they do not know, and drop the
    /// fragment RPC (ZRoutedRpc finds no such ZDO). The owner renders no fragments either, since the stump was never
    /// drawn (Destructible.CreateFragments takes visible renderers only). Everybody near hears the destroyed effect
    /// (sfx_wood_break on every stump, a prefab with a ZNetView) and sees the wood, which are networked objects.</para>
    /// <see cref="Replanting"/> takes the stump out the same way (<see cref="RemoveStump"/>).
    /// </summary>
    public static class CleanFell
    {
        /// <summary>Called by <see cref="Felling"/> on the tree's owner, after Timber and before Replanting.</summary>
        public static void OnFelled(FellContext fell)
        {
            if (fell.Woodcutter == null || fell.Stub == null)
                return;
            if (Random.value < WoodSkill.Share(FellPerkSettings.CleanFellAt100.Value, fell.Woodcutter.Level))
                RemoveStump(fell);
        }

        /// <summary>
        /// Takes the fell's stump out as if it had been chopped by the felling hit (its point, direction and attacker);
        /// does nothing when there is no stump or it is already gone.
        /// </summary>
        public static void RemoveStump(FellContext fell)
        {
            Destructible stub = fell.Stub;
            ZNetView nview = stub != null ? stub.m_nview : null;
            if (nview == null || !nview.IsValid() || !nview.IsOwner() || stub.m_destroyed)
                return;
            stub.Destroy(fell.Hit);
        }
    }
}
