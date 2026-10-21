// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Adapters;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The kraken's orders to every machine: a tentacle slam or smash, a bite, an ink squirt, a flinch. The owner decides
    /// and sends one small RPC to everybody (itself included); each machine plays the attack on its own
    /// <see cref="KrakenBody"/> from the moment it arrives and judges its own player when it lands. Targets travel in the
    /// held ship's own space, so each machine aims at the same spot on the ship as it sees it.
    /// </summary>
    public class KrakenAttacks : MonoBehaviour
    {
        public const string SlamRpc = "ecp_kraken_slam";
        public const string BiteRpc = "ecp_kraken_bite";
        public const string InkRpc = "ecp_kraken_ink";
        public const string FlinchRpc = "ecp_kraken_flinch";
        public const string StruckRpc = "ecp_kraken_struck";

        private ZNetView _nview = null!;
        private KrakenBody _body = null!;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _body = GetComponent<KrakenBody>();
            if (_nview == null || !_nview.IsValid())
            {
                return;
            }
            _nview.Register<int, Vector3, int>(SlamRpc, (sender, i, target, kind) => Guard.Run("Kraken slam", () => _body.Slam(i, target, (StrikeKind)kind)));
            _nview.Register<Vector3>(BiteRpc, (sender, target) => Guard.Run("Kraken bite", () => _body.Attack(HeadAction.Kind.Bite, target)));
            _nview.Register<Vector3>(InkRpc, (sender, target) => Guard.Run("Kraken ink", () => _body.Attack(HeadAction.Kind.Ink, target)));
            _nview.Register(FlinchRpc, sender => Guard.Run("Kraken flinch", _body.Flinch));
            _nview.Register<Vector3, int>(StruckRpc, (sender, at, what) => Guard.Run("Kraken struck", () => KrakenEffects.Struck(at, what)));
        }

        /// <summary>OWNER: tentacle <paramref name="i"/> strikes towards a point in the ship's space: a slam, a smash (it hits the ship too) or a grab.</summary>
        public void Slam(int i, Vector3 target, StrikeKind kind) => Send(SlamRpc, i, target, (int)kind);

        /// <summary>OWNER: the head bites at a point in the ship's space.</summary>
        public void Bite(Vector3 target) => Send(BiteRpc, target);

        /// <summary>OWNER: the head squirts ink at a point in the ship's space.</summary>
        public void Ink(Vector3 target) => Send(InkRpc, target);

        /// <summary>OWNER: it staggered; whatever was coming is pulled back.</summary>
        public void Flinch() => Send(FlinchRpc);

        /// <summary>
        /// ANY MACHINE: one of its blows landed on the player here (each machine judges its own player), so every machine
        /// plays the hit at <paramref name="at"/>: 0 a tentacle, 1 the beak, 2 the ink.
        /// </summary>
        public void Struck(Vector3 at, int what)
        {
            if (_nview != null && _nview.IsValid())
            {
                _nview.InvokeRPC(ZNetView.Everybody, StruckRpc, at, what);
            }
        }

        private void Send(string rpc, params object[] parameters)
        {
            if (_nview != null && _nview.IsValid() && _nview.IsOwner())
            {
                _nview.InvokeRPC(ZNetView.Everybody, rpc, parameters);
            }
        }
    }
}
