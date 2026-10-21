// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The game's own break-in, aimed at the raider's target rather than at random (features/raids.md section 4, "Whatever
    /// is in the way"). A monster given a static target walks to its nearest point and strikes it once it is in reach and
    /// in plain view, its blows damaging whatever piece they land on (<c>MonsterAI.UpdateAI</c>, <c>Attack</c>). This
    /// picks which piece that is, on the raider's slow tick: the target itself while the way to it is open; when it is in
    /// reach but a piece stands in the line between, or the game finds no path to it, the nearest player-built piece on
    /// the line from the raider's eyes to the target - the wall, door or gate between - if the raider can see it; failing
    /// that, a piece within 10 m it can see, exactly as the game's own break-in picks (<c>FindRandomStaticTarget</c>).
    /// The same aim serves a player shut in where the raider cannot reach (<see cref="Toward"/>). The piece it breaks is
    /// kept while it stands and is in view; a broken piece reads as null, and the next tick aims again. A path test is the
    /// one search it adds, every 1.5 seconds, the same the game makes for its priority targets.
    /// </summary>
    internal sealed class BreakIn
    {
        /// <summary>The game's own break-in range: a monster that cannot reach its target hits a piece this close.</summary>
        private const float RandomRange = 10f;

        /// <summary>Metres past its weapon's reach that still count as at the target.</summary>
        private const float ReachSlack = 1f;

        /// <summary>The reach of a raider holding no weapon the AI could pick.</summary>
        private const float BareReach = 2f;

        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static int _pieceMask;

        /// <summary>The piece it is breaking through, kept while it stands and is in view, so the aim does not hop between
        /// two pieces of one wall as the raider shuffles.</summary>
        private StaticTarget? _breaking;

        private StaticTarget? _goal;

        /// <summary>The piece to strike or walk to now, on the way to <paramref name="goal"/>.</summary>
        public StaticTarget Aim(MonsterAI ai, Character body, StaticTarget goal)
        {
            if (_goal != goal)
            {
                _goal = goal;
                _breaking = null; // a new target: whatever stood in the way of the last one is no reason to stay
            }
            Vector3 at = body.transform.position;
            Vector3 point = goal.FindClosestPoint(at);
            float reach = Reach(body);
            if ((point - at).sqrMagnitude <= reach * reach)
            {
                StaticTarget? blocker = Blocker(body, goal.GetCenter(), goal);
                return blocker == null ? Open(goal) : Breaking(ai, blocker) ?? goal; // strike it, or what is between
            }
            if (ai.HavePath(point))
            {
                return Open(goal); // a way round, a door left open: the game's pathfinding walks it
            }
            return Breaking(ai, Blocker(body, goal.GetCenter(), goal)) ?? goal;
        }

        /// <summary>The piece to break on the way to a creature it cannot reach - a player shut in - or null.</summary>
        public StaticTarget? Toward(MonsterAI ai, Character body, Character prey)
        {
            _goal = null; // the building target waits: the next aim at it starts afresh
            return Breaking(ai, Blocker(body, prey.GetCenterPoint(), null));
        }

        private StaticTarget Open(StaticTarget goal)
        {
            _breaking = null;
            return goal;
        }

        // Something stands between: the piece it is already breaking, while that stands and is in view; else the piece on
        // the line, when the game's own sight test would let it strike it; else one it can see within 10 m, as the game's
        // break-in picks.
        private StaticTarget? Breaking(MonsterAI ai, StaticTarget? blocker)
        {
            if (_breaking == null || Seen(ai, _breaking) == null)
            {
                _breaking = Seen(ai, blocker) ?? ai.FindRandomStaticTarget(RandomRange);
            }
            return _breaking;
        }

        // The game strikes a static target only in plain view (BaseAI.CanSeeTarget), so a piece it cannot see is no aim.
        private static StaticTarget? Seen(MonsterAI ai, StaticTarget? piece) =>
            piece != null && ai.CanSeeTarget(piece) ? piece : null;

        // The nearest piece a player built on the line from the eyes to the target's middle, the target itself aside.
        private static StaticTarget? Blocker(Character body, Vector3 to, StaticTarget? goal)
        {
            Vector3 eye = body.m_eye != null ? body.m_eye.position : body.GetCenterPoint();
            Vector3 line = to - eye;
            float length = line.magnitude;
            if (length < 0.01f)
            {
                return null;
            }
            int count = Physics.RaycastNonAlloc(eye, line / length, Hits, length, PieceMask());
            StaticTarget? best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].distance < bestDistance && Breakable(Hits[i].collider, goal) is Piece piece)
                {
                    best = piece;
                    bestDistance = Hits[i].distance;
                }
            }
            return best;
        }

        private static Piece? Breakable(Collider collider, StaticTarget? goal)
        {
            Piece? piece = collider != null ? collider.GetComponentInParent<Piece>() : null;
            if (piece == null || piece == goal || !piece.IsPlacedByPlayer())
            {
                return null;
            }
            return piece.GetComponent<WearNTear>() != null ? piece : null;
        }

        private static float Reach(Character body)
        {
            ItemDrop.ItemData? weapon = body is Humanoid humanoid ? humanoid.GetCurrentWeapon() : null;
            return (weapon != null ? weapon.m_shared.m_aiAttackRange : BareReach) + ReachSlack;
        }

        private static int PieceMask()
        {
            if (_pieceMask == 0)
            {
                _pieceMask = LayerMask.GetMask("piece");
            }
            return _pieceMask;
        }
    }
}
