// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.Adopted.Visuals;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The warning that rides a Bloated creature's corpse for the whole fuse. Spawned on every client from the death
    /// broadcast (see <see cref="EliteRpc"/>), which names the corpse by id - the ragdoll that very death made (see
    /// <see cref="DeathRagdoll"/>) - so each client finds its own copy of that one corpse and never a neighbour's lying
    /// nearby. It follows the body wherever it slides or rolls, so the thing that looks like it is about to explode is
    /// the thing that is. Ragdoll physics is not synchronised, so each client's corpse settles a little differently -
    /// that divergence is only cosmetic here. It also keeps the corpse from vanishing on its own timer before the blast
    /// (see <see cref="CorpseBurst"/>). When the fuse ends, ONLY the owner's rider broadcasts the blast at its corpse's
    /// resting place, naming the same corpse for the blast to burst, so the blast everyone sees and the damage the owner
    /// deals are the same spot. A death that left no ragdoll, or a corpse that never reaches this client, leaves the
    /// warning and the blast at the place of death - better a blast in the right area than none.
    /// </summary>
    public sealed class BloatedCorpse : MonoBehaviour
    {
        /// <summary>Radius the swelling-corpse warning is scaled to - a body-sized tell, not the full blast footprint.</summary>
        private const float WarningRadius = 2f;

        /// <summary>Seconds between looks for the named corpse on a client it has not reached yet.</summary>
        private const float LookEvery = 0.25f;

        private float _fuse;
        private bool _isOwner;
        private BlastSpec _blast;
        private ZDOID _corpseId;
        private Ragdoll? _corpse;
        private bool _found;
        private float _searchTimer;

        public static void Spawn(Vector3 deathPos, ZDOID corpseId, float delay, string warningEffect, BlastSpec blast,
            bool isOwner)
        {
            GameObject holder = new GameObject("ecr_bloated_corpse");
            holder.transform.position = deathPos;
            BloatedCorpse corpse = holder.AddComponent<BloatedCorpse>();
            corpse._fuse = delay;
            corpse._isOwner = isOwner;
            corpse._blast = blast;
            corpse._corpseId = corpseId;
            corpse._found = corpseId == ZDOID.None; // no corpse to look for: the warning stays at the place of death
            if (!Machine.Headless) // a dedicated server still keeps the fuse and holds the corpse, but draws nothing
            {
                GameObject? warning = EffectResolver.Resolve(warningEffect, EffectResolver.Warning, "Bloated warning effect");
                LingeringVisual.Hold(holder, warning, WarningRadius); // full strength until the fuse ends and takes it away
            }
        }

        private void Update() => Guard.Run("BloatedCorpse.Update", static self => self.Step(), this);

        private void Step()
        {
            RideCorpse();
            _fuse -= Time.deltaTime;
            if (_fuse > 0f)
            {
                return;
            }
            EndFuse();
            Destroy(gameObject);
        }

        // Follow the local copy of the named corpse once found - its bones, since a ragdoll's root never moves. Until then
        // keep looking on a throttle (the ragdoll replicates a frame or two after the death broadcast on a remote client),
        // staying at the death spot. Found once, never sought again: if the corpse goes, the rider stays where it was.
        private void RideCorpse()
        {
            if (_found)
            {
                if (_corpse != null)
                {
                    transform.position = _corpse.GetAverageBodyPosition();
                }
                return;
            }
            _searchTimer -= Time.deltaTime;
            if (_searchTimer > 0f)
            {
                return;
            }
            _searchTimer = LookEvery;
            Latch(NamedCorpse());
        }

        private void Latch(Ragdoll? corpse)
        {
            if (corpse == null)
            {
                return;
            }
            _corpse = corpse;
            _found = true;
            CorpseBurst.Hold(corpse, _fuse); // the blast, not the corpse's own timer, ends it
            transform.position = corpse.GetAverageBodyPosition();
        }

        // This client's copy of the named corpse, once the network has made it here; null until then.
        private Ragdoll? NamedCorpse()
        {
            GameObject? go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(_corpseId) : null;
            return go != null ? go.GetComponent<Ragdoll>() : null;
        }

        // The blast position is the OWNER's corpse, sent when the fuse ENDS (not at death): where its body was last seen,
        // or the death spot if none was found. The blast names the same corpse the death did, and no other. Non-owners
        // simply stop showing the warning; they draw the blast when the owner's broadcast arrives.
        private void EndFuse()
        {
            if (!_isOwner)
            {
                return;
            }
            EliteRpc.FireBlast(transform.position, _corpseId, _blast);
        }
    }
}
