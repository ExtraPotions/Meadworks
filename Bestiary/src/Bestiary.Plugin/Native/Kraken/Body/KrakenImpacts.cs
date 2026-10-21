// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// What happens on one machine when a blow lands there or the kraken changes what it is doing: the effects, whether
    /// the player on this machine is caught (<see cref="KrakenStrikes"/>), the ship rocking (where it is sailed) and, on
    /// the kraken's owner only, a smash's damage to the ship.
    /// </summary>
    public static class KrakenImpacts
    {
        private const float SlamRock = 0.35f;
        private const float SmashRock = 0.8f;

        /// <summary>A tentacle's blow landed; the arm's shown pose is the one it lies in.</summary>
        public static void Landed(KrakenBody body, TentacleMotion arm, TentacleStrike? strike)
        {
            Ship? ship = body.Scene?.Ship;
            if (strike == null || ship == null)
            {
                return;
            }
            bool smash = strike.Kind == StrikeKind.Smash;
            Vector3 at = arm.Shown[TentacleSpec.Bones * 2 / 3];
            KrakenEffects.Landing(at, smash);
            Player? caught = KrakenStrikes.Slam(body.Character, arm.Shown, body.Scale, strike.Kind);
            if (caught != null && strike.Kind == StrikeKind.Grab)
            {
                KrakenHeld.Grab(caught, arm, Time.time + strike.Release - strike.Age, -strike.Frame(body.Scene!, body.Scale).Out);
            }
            ShipHold.Rock(ship, at, smash ? SmashRock : SlamRock);
            if (smash && body.State.IsOwner)
            {
                ShipHold.Damage(ship, body.Character, at);
            }
        }

        /// <summary>The head's attack reached its moment: the beak snaps, or the ink flies from its mouth.</summary>
        public static void Fired(KrakenBody body, HeadAction? action)
        {
            if (action == null || body.Rig == null || body.Scene == null)
            {
                return;
            }
            if (action.What == HeadAction.Kind.Bite)
            {
                KrakenEffects.Biting(body.Rig.Mouth.position);
                KrakenStrikes.Bite(body.Character, body.Rig.Mouth.position, body.Scale);
            }
            else
            {
                InkJet.Launch(body.Character, body.Rig.Mouth.position, body.Scene.World(action.Target));
            }
        }

        /// <summary>The kraken began something new, as this machine sees it.</summary>
        public static void Entered(KrakenBody body, KrakenPhase phase)
        {
            Ship? ship = KrakenShips.Find(body.State.Ship);
            switch (phase)
            {
                case KrakenPhase.Hunt:
                    KrakenEffects.Roaring(body.transform.position);
                    break;
                case KrakenPhase.Dive:
                    KrakenEffects.Surfacing(Surface(body.transform.position));
                    break;
                case KrakenPhase.Tentacles when ship != null:
                    Splashes(ship);
                    break;
                case KrakenPhase.Head when ship != null:
                    Vector3 head = KrakenTargets.HeadSpot(ship, body.State.Side, body.State.Along, 0f);
                    KrakenEffects.Surfacing(head);
                    KrakenEffects.Roaring(head);
                    break;
                case KrakenPhase.Leave:
                    KrakenEffects.Diving(Surface(body.transform.position));
                    break;
            }
        }

        private static void Splashes(Ship ship)
        {
            ShipHull hull = ShipHull.Of(ship);
            for (int i = 0; i < ShipHull.Anchors; i++)
            {
                KrakenEffects.Emerging(Surface(ship.transform.TransformPoint(hull.Anchor(i))));
            }
        }

        private static Vector3 Surface(Vector3 at) => new Vector3(at.x, KrakenShips.Water(at), at.z);
    }
}
