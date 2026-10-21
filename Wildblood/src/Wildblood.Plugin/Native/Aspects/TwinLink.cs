// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Twin's shared health pool. Attached to both twins on every machine; it acts only while this machine owns its twin.
    /// Each frame it compares the twin's health with what it last saw and sends any loss - a hit, a burn tick, lava - to
    /// the partner's owner, which takes the same amount off. A share it receives moves its baseline by exactly what it
    /// removed, so it is never sent back. Healing is not shared: both regenerate at the game's same rate, so they stay
    /// level. A twin's death is sent separately (<see cref="SendFall"/>), from the death patch, because by the next
    /// frame the dead twin's ZDO is gone and this could not see its last loss.
    /// </summary>
    public sealed class TwinLink : MonoBehaviour
    {
        private Character _character = null!;
        private EliteController _controller = null!;
        private bool _tracking;
        private float _last;

        private void Start()
        {
            _character = GetComponent<Character>();
            _controller = GetComponent<EliteController>();
        }

        private void Update() => Guard.Run("TwinLink.Update", static self => self.Step(), this);

        private void Step()
        {
            if (_character == null || _controller == null || !_controller.IsOwner() || _character.IsDead())
            {
                _tracking = false; // a new owner starts from the health it finds, not a stale baseline
                return;
            }
            float health = _character.GetHealth();
            if (_tracking && health < _last - 0.01f)
            {
                Send(_controller.View.GetZDO(), _last - health, fatal: false);
            }
            _tracking = true;
            _last = health;
        }

        /// <summary>Owner side, from a partner's share: take the same loss, or fall when the partner fell.</summary>
        public void Receive(float loss, bool fatal)
        {
            if (_character == null || _controller == null || !_controller.IsOwner() || _character.IsDead())
            {
                return;
            }
            float before = _character.GetHealth();
            float after = fatal ? 0f : Mathf.Max(0f, before - loss);
            _character.SetHealth(after);
            _last = _tracking ? _last - (before - after) : after;
            _tracking = true;
        }

        /// <summary>From a twin's death, while its ZDO is still readable: its partner falls with it.</summary>
        public static void SendFall(ZDO zdo) => Send(zdo, 0f, fatal: true);

        private static void Send(ZDO zdo, float loss, bool fatal)
        {
            ZDOID partner = AspectStore.GetTwin(zdo);
            GameObject? go = partner != ZDOID.None && ZNetScene.instance != null ? ZNetScene.instance.FindInstance(partner) : null;
            ZNetView? view = go != null ? go.GetComponent<ZNetView>() : null;
            if (view != null && NativeRpcOwnership.OwnsView(view, NativeRpcOwner.CreatureBoss))
            {
                view.InvokeRPC(CreatureRpc.TwinShare, loss, fatal); // to the partner's owner; delivered locally if that is us
            }
        }
    }
}
