// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Bestiary.Fixes
{
    /// <summary>What a pending crypt chest does on this frame.</summary>
    public enum CryptSwapStep
    {
        /// <summary>The view, the owner, or the prefab is not ready. Leave the swapper enabled.</summary>
        Retry = 0,

        /// <summary>This machine owns the chest and the mimic prefab exists.</summary>
        Swap = 1,

        /// <summary>The swap is given up. Clear the pending key and fill the chest.</summary>
        Fill = 2,
    }

    /// <summary>
    /// A claimed crypt chest stays empty when the swapper gives up on the first frame. Retry until this machine
    /// owns the chest and the prefab exists. Fill when mimics are off, or when the prefab build has settled and
    /// the prefab is still missing.
    /// </summary>
    public static class CryptSwapRules
    {
        public static CryptSwapStep Next(bool viewValid, bool owner, bool prefabReady, bool buildSettled, bool mimicsOn)
        {
            if (!viewValid || !owner)
            {
                return CryptSwapStep.Retry;
            }
            if (!mimicsOn)
            {
                return CryptSwapStep.Fill;
            }
            if (prefabReady)
            {
                return CryptSwapStep.Swap;
            }
            return buildSettled ? CryptSwapStep.Fill : CryptSwapStep.Retry;
        }
    }

    /// <summary>
    /// Who may take or leave a bone ballista. The owner checks the sender's own player id and the use position.
    /// A client starts holding only after it asked.
    /// </summary>
    public static class BallistaGrant
    {
        public const float UseRange = 3.5f;

        /// <summary>Local forward past this is the side the player cannot use (the existing behind check).</summary>
        public const float Behind = 0f;

        public static bool InPlace(float distance, float localForward) =>
            distance <= UseRange && localForward <= Behind;

        public static bool AcceptRequest(long senderPlayerId, long claimedPlayerId, float distance, float localForward, bool heldBySomeoneElse) =>
            senderPlayerId != 0L
            && senderPlayerId == claimedPlayerId
            && InPlace(distance, localForward)
            && !heldBySomeoneElse;

        public static bool AcceptRelease(long senderPlayerId, long claimedPlayerId, long holderId) =>
            senderPlayerId != 0L && senderPlayerId == claimedPlayerId && holderId == claimedPlayerId;

        public static bool AcceptGrant(bool thisClientAsked) => thisClientAsked;
    }

    /// <summary>
    /// One greataxe's attack is not every greataxe's attack. Live numbers are copied onto each item.
    /// Swing timers and the chain step stay on that item's own attack.
    /// </summary>
    public static class SharedAttackRules
    {
        public const float SpinAngle = 360f;
        public const int SpinChain = 1;

        public static bool ShareAttackObjects => false;

        private static readonly HashSet<string> Runtime = new HashSet<string>(StringComparer.Ordinal)
        {
            "m_time",
            "m_currentAttackCainLevel",
            "m_nextAttackChainLevel",
            "m_character",
            "m_baseAI",
            "m_body",
            "m_zanim",
            "m_animEvent",
            "m_weapon",
            "m_visEquipment",
            "m_lastUsedAmmo",
            "m_attackDrawPercentage",
            "m_wasInAttack",
            "m_abortAttack",
            "m_attackTowardsCameraDir",
            "m_projectileAttackStarted",
            "m_projectileFireTimer",
            "m_projectileBurstsFired",
            "m_ammoItem",
            "m_attackDone",
            "m_isAttached",
            "m_attachTarget",
            "m_attachOffset",
            "m_attachDistance",
            "m_attachHitPoint",
            "m_detachTimer",
        };

        public static bool IsRuntime(string field) => Runtime.Contains(field);

        /// <summary>Combo step 2 is the spin. Every other step uses the axe's configured arc.</summary>
        public static float SwingAngle(int chainLevel, float configuredAngle) =>
            chainLevel == SpinChain ? SpinAngle : configuredAngle;
    }

    /// <summary>
    /// The active-boss count is the server's. A client owner may ask for one decrement.
    /// It does not send its own count.
    /// </summary>
    public static class BossCountRules
    {
        public static bool MayWrite(bool isServer) => isServer;

        public static float Decrement(float serverCount)
        {
            float next = serverCount - 1f;
            return next < 0f ? 0f : next;
        }

        public static bool Accept(bool isServer, bool senderOwns, bool alreadyReleased, bool counted, bool isKraken) =>
            isServer && senderOwns && !alreadyReleased && counted && isKraken;
    }
}
