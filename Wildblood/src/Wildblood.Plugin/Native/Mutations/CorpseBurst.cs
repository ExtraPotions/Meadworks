// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// A Bloated corpse ends in its own blast, not in the game's corpse timer. Left alone, the ragdoll races the blast or
    /// outlives it - its timer is 2 seconds for most creatures and up to 10 for the biggest - and then vanishes in the
    /// game's corpse smoke as it drops its loot: a white cloud left behind after the explosion. So the corpse rider moves
    /// the corpse's own removal to just past the fuse (<see cref="Hold"/>), and the blast bursts it (<see cref="Burst"/>)
    /// through that same removal - the loot saved on the corpse at death drops exactly as it would have - with only the
    /// smoke left out, because the blast is its end.
    /// <para>
    /// The ragdoll is a networked, persistent object, and only the machine that owns it can remove it for everyone:
    /// normally the creature's owner, which made it and sends the blast, but ownership can move. So the blast names the
    /// corpse, every machine is told, and only its owner acts; the removal then replicates. Should the blast never come
    /// (its sender left mid-fuse), the moved timer still removes the corpse the game's way, loot and smoke, a moment after
    /// the fuse - a corpse is never left lying and its loot is never lost.
    /// </para>
    /// </summary>
    public static class CorpseBurst
    {
        /// <summary>The game's corpse removal on <see cref="Ragdoll"/>, which its own Awake schedules by this name.</summary>
        private const string Removal = "DestroyNow";

        /// <summary>How long past the fuse the corpse's own timer waits for the blast before removing it the game's way.</summary>
        private const float Grace = 1f;

        /// <summary>Moves the corpse's own removal to just past the fuse, so the blast - not its timer - ends it.</summary>
        public static void Hold(Ragdoll corpse, float fuse)
        {
            corpse.CancelInvoke(Removal);
            corpse.InvokeRepeating(Removal, Mathf.Max(fuse, 0f) + Grace, 1f);
        }

        /// <summary>The corpse's network id, the same on every machine; None when there is no corpse to burst.</summary>
        public static ZDOID IdOf(Ragdoll? corpse)
        {
            ZNetView? nview = corpse != null ? corpse.GetComponent<ZNetView>() : null;
            return nview != null && nview.IsValid() ? nview.GetZDO().m_uid : ZDOID.None;
        }

        /// <summary>
        /// Runs on every machine at the blast; only the corpse's owner acts. The game's own removal drops the loot and
        /// removes the corpse everywhere; emptying its remove effect first leaves out the smoke, which only the owner makes.
        /// </summary>
        public static void Burst(ZDOID id)
        {
            GameObject? go = id != ZDOID.None && ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
            Ragdoll? corpse = go != null ? go.GetComponent<Ragdoll>() : null;
            ZNetView? nview = go != null ? go.GetComponent<ZNetView>() : null;
            if (corpse == null || nview == null || !nview.IsOwner())
            {
                return; // gone already, not a corpse, or another machine's to remove
            }
            corpse.m_removeEffect = new EffectList();
            corpse.Invoke(Removal, 0f); // next frame, through the very method its timer calls
        }
    }
}
