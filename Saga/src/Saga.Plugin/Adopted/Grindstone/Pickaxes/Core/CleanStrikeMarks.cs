// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/CleanStrikeMarks.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using ExtraPotions.Saga.Skills;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Tells a rock's owner that a miner's next hit on a chunk is a clean strike, so the break of that chunk can give the
    /// clean strike's extra roll there.
    /// <list type="bullet">
    /// <item><see cref="Send"/> runs on the miner's client, before the clean strike's hit is sent: an RPC on the rock's
    /// ZNetView to its owner (<see cref="Keys.RpcCleanStrike"/>: int area, ZDOID the miner's player). Both RPCs go the
    /// same route from the same sender, so the mark arrives first; when the miner owns the rock it is handled at once.</item>
    /// <item>The owner keeps each mark in memory for <see cref="Lifetime"/> seconds (a clean strike may need more hits
    /// to break its chunk). Marks are lost if the rock changes owner in between.</item>
    /// <item><see cref="Take"/> runs on the owner when a chunk breaks: true once per mark of that miner on that chunk.</item>
    /// </list>
    /// The RPC is registered on a MineRock5's view when the first mark arrives for it (<see cref="LazyRpcs"/>), beside the
    /// game's own RPC_Damage, not on every rock that wakes.
    /// </summary>
    public static class CleanStrikeMarks
    {
        /// <summary>Seconds the owner keeps a mark.</summary>
        public const float Lifetime = 30f;

        private const int MaxMarks = 512;

        private static readonly List<Mark> Marks = new List<Mark>();

        /// <summary>Registers the mark RPC on a rock's view, when the first mark arrives for it (<see cref="LazyRpcs"/>).</summary>
        public static void RegisterOn(ZNetView view)
        {
            MineRock5 chunks = view.GetComponent<MineRock5>();
            if (chunks != null) {
                view.Register<int, ZDOID>(Keys.RpcCleanStrike, (sender, area, miner) => Receive(chunks, sender, area, miner));
                NativeRpcCleanup.Track(view, Keys.RpcCleanStrike);
            }
        }

        /// <summary>On the miner's client: marks the chunk <paramref name="area"/> of <paramref name="rock"/> for the local player.</summary>
        public static void Send(Rock rock, int area)
        {
            Player player = Player.m_localPlayer;
            if (player != null && rock != null && rock.IsValid && area >= 0)
                rock.View.InvokeRPC(Keys.RpcCleanStrike, area, player.GetZDOID());
        }

        /// <summary>On the rock's owner: takes the mark of this miner on this chunk; true when there was one.</summary>
        public static bool Take(ZDOID rock, int area, ZDOID miner)
        {
            Prune();
            int index = Marks.FindIndex(mark => mark.Rock == rock && mark.Area == area && mark.Miner == miner);
            if (index < 0)
                return false;
            Marks.RemoveAt(index);
            return true;
        }

        /// <summary>On the rock's owner: takes the breaking miner's mark on the broken chunk; false for a single piece.</summary>
        public static bool Take(RockBreak broken) =>
            broken != null && broken.Miner != null && broken.Chunk >= 0 && Take(broken.RockId, broken.Chunk, broken.Miner.Attacker);

        private static void Receive(MineRock5 chunks, long sender, int area, ZDOID miner) =>
            HookGuard.Run("clean strike mark", () => Keep(chunks, sender, area, miner));

        private static void Keep(MineRock5 chunks, long sender, int area, ZDOID miner)
        {
            ZNetView view = chunks != null ? chunks.m_nview : null;
            bool ownsRock = view != null && view.IsValid() && view.IsOwner();
            ZDO minerZdo = null;
            if (!miner.IsNone() && ZDOMan.instance != null) minerZdo = ZDOMan.instance.GetZDO(miner);
            bool minerPresent = minerZdo != null && minerZdo.IsValid();
            bool senderOwns = false;
            if (minerPresent) senderOwns = minerZdo.GetOwner() == sender;
            if (!CleanStrikeRules.Keep(PickSkill.Active, ownsRock, minerPresent, senderOwns, area) || view == null)
                return;
            Prune();
            if (Marks.Count >= MaxMarks)
                Marks.RemoveAt(0);
            Marks.Add(new Mark(view.GetZDO().m_uid, area, miner, Time.time));
        }

        private static void Prune()
        {
            float oldest = Time.time - Lifetime;
            Marks.RemoveAll(mark => mark.Made < oldest);
        }

        private readonly struct Mark
        {
            public Mark(ZDOID rock, int area, ZDOID miner, float time)
            {
                Rock = rock;
                Area = area;
                Miner = miner;
                Made = time;
            }

            public ZDOID Rock { get; }
            public int Area { get; }
            public ZDOID Miner { get; }
            public float Made { get; }
        }
    }
}
