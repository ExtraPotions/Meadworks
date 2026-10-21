// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The player on this machine caught by the kraken's grab: held at the grabbing tentacle's tip as it hoists them up
    /// and out over the water, then flung: let go with the tip's swing (out from the ship and up), to land in the sea.
    /// Only the machine that owns the player moves them (as the game moves every player), and everyone else sees them
    /// fly by the game's own sync. A grab that loses its tentacle or its player lets go at once.
    /// </summary>
    public class KrakenHeld : MonoBehaviour
    {
        private const int Grip = 13;             // the pose point the player hangs from: near the tip, where it is strong enough
        private const float Throw = 3f;          // metres a second out from the ship: a short toss, into the sea a few metres off
        private const float Lift = 3f;           // and up

        private Player _player = null!;
        private TentacleMotion _arm = null!;
        private Vector3 _out;
        private float _release;

        /// <summary>Holds <paramref name="player"/> on <paramref name="arm"/> until <paramref name="release"/> (Time.time), then throws them along <paramref name="outward"/>.</summary>
        public static void Grab(Player player, TentacleMotion arm, float release, Vector3 outward)
        {
            KrakenHeld held = player.GetComponent<KrakenHeld>();
            if (held == null)
            {
                held = player.gameObject.AddComponent<KrakenHeld>();
            }
            held._player = player;
            held._arm = arm;
            held._release = release;
            outward.y = 0f;
            held._out = outward.sqrMagnitude > 1e-4f ? outward.normalized : -player.transform.forward;
        }

        private void FixedUpdate()
        {
            if (_player == null || _arm == null || _player.IsDead())
            {
                Destroy(this);
                return;
            }
            Rigidbody body = _player.m_body;
            if (Time.time < _release)
            {
                Vector3 at = _arm.Shown[Grip] - Vector3.up * 1f;
                _player.transform.position = at;
                body.position = at;
                body.linearVelocity = Vector3.zero;
                return;
            }
            body.linearVelocity = _out * Throw + Vector3.up * Lift;
            Destroy(this);
        }
    }
}
