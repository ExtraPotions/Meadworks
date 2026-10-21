// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGatePairing.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>Pairing two pillars into a gate and taking a gate apart. The new pillar writes its own link on the
    /// placer's machine and asks the other pillar's owner for the other half (<see cref="SeaGateFields.PairRpc"/>);
    /// a pillar destroyed for real asks its partner's owner to let go (<see cref="SeaGateFields.UnpairRpc"/>). Every
    /// write happens on the owner of the ZDO it changes. A lost race or a missed message leaves a one-sided link,
    /// which reads as unpaired (<see cref="SeaGateFields.IsMutual"/>), never as a gate.</summary>
    public static class SeaGatePairing
    {
        private const float RequesterWaitSeconds = 6f;
        private const float RequesterPollSeconds = 0.25f;
        private const float RetrySeconds = 3f;
        private const float AnnounceDistance = 40f;

        private static float nextRetry;

        private static bool Active => SeaGateConfig.On;

        /// <summary>Placer's machine, a fresh pillar (owner, no id yet): stamp an id and pair with the best candidate.</summary>
        public static void PairNew(SeaGatePillar pillar)
        {
            if (!Active || !IsOwned(pillar) || pillar.Id != 0L)
                return;
            pillar.Zdo.Set(SeaGateFields.IdKey, SeaGateFields.NewId());
            TryPair(pillar);
            SeaGateRegistry.Invalidate();
        }

        /// <summary>Every few seconds on every machine: each unpaired pillar this machine owns tries again, so lowering
        /// the seabed, moving a pillar or removing a stray one opens the gate without rebuilding. A pillar placed while
        /// sea gates were off gets its id here.</summary>
        internal static void Tick()
        {
            if (!Active || Time.time < nextRetry)
                return;
            nextRetry = Time.time + RetrySeconds;
            IReadOnlyList<SeaGatePillar> pillars = SeaGateRegistry.Pillars;
            for (int i = 0; i < pillars.Count; i++)
            {
                SeaGatePillar pillar = pillars[i];
                if (!IsOwned(pillar) || SeaGatePairRules.HasLivePartner(pillar))
                    continue;
                if (pillar.Id == 0L)
                    PairNew(pillar);
                else if (TryPair(pillar))
                    SeaGateRegistry.Invalidate();
            }
        }

        /// <summary>Owner of a pillar with an id: when the rules allow, write this half of the link and ask the other
        /// pillar's owner for the other half (again on each retry until it answers); a player standing near is told
        /// the gate opens, once.</summary>
        private static bool TryPair(SeaGatePillar pillar)
        {
            PairCheck check = Check(pillar.transform.position, pillar.Id);
            if (!check.Ok || check.Candidate == null || !check.Candidate.IsValid)
                return false;
            bool fresh = SeaGateFields.GetPartner(pillar.Zdo) != check.Candidate.Id;
            Link(pillar.Zdo, check.Candidate.Id, check.Sides);
            check.Candidate.View.InvokeRPC(SeaGateFields.PairRpc, pillar.Id, check.Sides);
            if (fresh)
                Announce(pillar.transform.position);
            return true;
        }

        private static void Announce(Vector3 position)
        {
            Player player = Player.m_localPlayer;
            if (player != null && Vector3.Distance(player.transform.position, position) <= AnnounceDistance)
                player.Message(MessageHud.MessageType.Center, SeaGateWords.Paired);
        }

        /// <summary>Owner side of <see cref="SeaGateFields.PairRpc"/> on the existing pillar. The request can arrive
        /// before the new pillar's ZDO has reached this machine, so an unknown requester is waited for a few seconds.</summary>
        public static void OnPair(SeaGatePillar pillar, long sender, long partnerId, int sides)
        {
            if (!IsOwned(pillar) || pillar.Id == 0L || partnerId == 0L || partnerId == pillar.Id)
                return;
            if (sides == 0 || (sides & ~SeaGateFields.BothSides) != 0)
                return;
            if (!Decide(pillar, partnerId, sides))
                pillar.StartCoroutine(AwaitRequester(pillar, partnerId, sides));
        }

        /// <summary>Accepts or silently rejects a pair request once the requester is loaded here and names this pillar;
        /// false while it is not here yet.</summary>
        private static bool Decide(SeaGatePillar pillar, long partnerId, int sides)
        {
            if (!IsOwned(pillar))
                return true;
            SeaGatePillar requester = SeaGateRegistry.FindLoaded(partnerId);
            if (requester == null || SeaGateFields.GetPartner(requester.Zdo) != pillar.Id)
                return false;
            if (!SeaGatePairRules.HasLivePartner(pillar) && SeaGatePairRules.InWidth(pillar.transform.position, requester.transform.position))
                Link(pillar.Zdo, partnerId, sides);
            return true;
        }

        private static IEnumerator AwaitRequester(SeaGatePillar pillar, long partnerId, int sides)
        {
            float until = Time.time + RequesterWaitSeconds;
            while (Time.time < until)
            {
                yield return new WaitForSeconds(RequesterPollSeconds);
                if (pillar == null || Decide(pillar, partnerId, sides))
                    yield break;
            }
        }

        /// <summary>Writes the other half of the link.</summary>
        private static void Link(ZDO zdo, long partnerId, int sides)
        {
            zdo.Set(SeaGateFields.PartnerKey, partnerId);
            zdo.Set(SeaGateFields.SidesKey, sides);
            SeaGateRegistry.Invalidate();
        }

        /// <summary>Owner side of <see cref="SeaGateFields.UnpairRpc"/>: the partner is going away.</summary>
        public static void OnUnpair(SeaGatePillar pillar, long sender, long partnerId)
        {
            if (!IsOwned(pillar) || partnerId == 0L || SeaGateFields.GetPartner(pillar.Zdo) != partnerId)
                return;
            ZDO zdo = pillar.Zdo;
            zdo.Set(SeaGateFields.PartnerKey, 0L);
            SeaGateRegistry.Invalidate();
        }

        /// <summary>The verdict for a pillar at <paramref name="position"/> (the placement preview uses this too).</summary>
        public static PairCheck Check(Vector3 position, long selfId) => SeaGatePairRules.Check(position, selfId, null);

        /// <summary>A pillar is being destroyed for real on its owner: tell its loaded partner to let go. A partner not
        /// loaded here keeps a one-sided link, which already reads as unpaired.</summary>
        internal static void OnDestroyed(ZDO zdo)
        {
            long myId = SeaGateFields.GetId(zdo);
            SeaGatePillar partner = SeaGateRegistry.FindLoaded(SeaGateFields.GetPartner(zdo));
            if (myId == 0L || partner == null || partner.View == null)
                return;
            partner.View.InvokeRPC(SeaGateFields.UnpairRpc, myId);
            SeaGateRegistry.Invalidate();
        }

        private static bool IsOwned(SeaGatePillar pillar) => pillar != null && pillar.IsValid && pillar.View.IsOwner();
    }

    /// <summary>Unpairs on real destruction only. Every real removal of a networked object goes through
    /// <c>ZNetScene.Destroy</c> on the owner, which deletes the ZDO: hammer removal (<c>WearNTear.Remove</c> ->
    /// <c>RPC_Remove</c> -> <c>WearNTear.Destroy</c>, or <c>Player.RemovePiece</c>'s own call for a piece without
    /// <c>WearNTear</c>) and breaking (<c>WearNTear.ApplyDamage</c> -> <c>Destroy</c>). A zone unloading
    /// (<c>ZNetScene.RemoveObjects</c>) and a ZDO destroyed elsewhere (<c>OnZDODestroyed</c>) call
    /// <c>Object.Destroy</c> directly and never come here, though both run the pillar's OnDestroy.</summary>
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Destroy), new[] { typeof(GameObject) })]
    public static class PillarDestroyPatch
    {
        [HarmonyPrefix]
        public static void Prefix(GameObject go)
        {
            if (go == null || !SeaGateConfig.On)
                return;
            ZNetView view = go.GetComponent<ZNetView>();
            ZDO zdo = view != null ? view.GetZDO() : null;
            if (!SeaGateFields.IsPillar(zdo) || !zdo.IsOwner())
                return;
            SeaGatePairing.OnDestroyed(zdo);
        }
    }
}
