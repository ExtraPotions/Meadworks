// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Aspects;
using ExtraPotions.Wildblood.Adopted.Patches;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// A Cloning creature's decoy (<see cref="CloneSpawner"/>): it fights like its creature but its blows do no harm
    /// (<see cref="CloneHits"/>), and it is hollow the way a Phantom copy is - no drops, no body, no death that counts
    /// (<see cref="PhantomBody"/>); having none of its mutations' powers, it never splits, explodes, devours or steals.
    /// It lasts only while its creature hides behind it. Its owner checks every frame and takes it away in a puff the
    /// moment that ends: the creature showed itself (a landed blow, or its decoy's time ran out), died, or is gone. Killed
    /// first, it goes in the same puff, and the creature's owner, finding it gone, shows the creature. The puff is sent
    /// over its own network view before it goes, so every machine that holds it draws one. Attached on every machine:
    /// each needs the puff's handler and the hollowing, and only the live owner's checks act.
    /// </summary>
    public sealed class CloneDecoy : MonoBehaviour
    {
        /// <summary>To every machine holding the decoy: it is going, here.</summary>
        public const string PuffRpc = "ecr_clone_puff";

        /// <summary>Seconds past its creature's `decoy life` before it goes on its own, should no owner end the trick.</summary>
        private const float LifeSlack = 5f;

        private Character _character = null!;
        private EliteController _controller = null!;
        private bool _gone;
        private System.Action? _step;

        private void Start() => Guard.Run("CloneDecoy.Start", Setup);

        private void Setup()
        {
            _character = GetComponent<Character>();
            _controller = GetComponent<EliteController>();
            if (_character == null || _controller == null || _controller.View == null || !_controller.View.IsValid())
            {
                enabled = false;
                return;
            }
            PhantomBody.Hollow(_character);
            NativeRpcOwnership.View(_controller.View, NativeRpcOwner.CreatureDecoy, () => _controller.View.Register<Vector3>(PuffRpc, OnPuff), PuffRpc);
            _character.m_onDeath += OnDied; // the game calls it on the owner only, while the ZDO still answers
            CloneBodies.Ignore(Real(), _character);
        }

        private void Update() => Guard.Run("CloneDecoy.Update", _step ??= Step);

        private void Step()
        {
            if (!_gone && _controller.OwnsCurrentView() && !_character.IsDead() && (!_controller.Ready || Abandoned()))
            {
                Leave();
            }
        }

        /// <summary>Its creature no longer hides behind it - showed itself, died or is gone - or its time ran out long ago.</summary>
        private bool Abandoned()
        {
            ZDO own = _controller.View.GetZDO();
            ZDO? real = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(CloneStore.RealOf(own)) : null;
            if (real == null || CloneStore.Decoy(real) != own.m_uid)
            {
                return true;
            }
            float life = _controller.Rules.PowerOf(Mutation.Cloning, Fields.DecoyLife);
            return life > 0f && CloneStore.SecondsSinceMark(real) > life + LifeSlack;
        }

        /// <summary>Owner: the puff, then gone - removed outright, not killed, so nothing counts it as a kill.</summary>
        private void Leave()
        {
            _gone = true;
            Puff();
            ZNetScene.instance.Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_character is not null) _character.m_onDeath -= OnDied;
        }

        private void OnDied() => SafeCall.Run("CloneDecoy.Died", () =>
        {
            if (!_gone)
            {
                _gone = true;
                Puff();
            }
        });

        private void Puff()
        {
            if (NativeRpcOwnership.OwnsView(_controller.View, NativeRpcOwner.CreatureDecoy))
            {
                _controller.View.InvokeRPC(ZRoutedRpc.Everybody, PuffRpc, _character.GetCenterPoint());
            }
        }

        private void OnPuff(long sender, Vector3 at) => Guard.Run("CloneDecoy.Puff", () =>
        {
            if (!BlinkEffects.Headless())
            {
                CloneEffects.Vanish(_controller.Rules.PrefabOf(Mutation.Cloning, Fields.VanishEffect), at,
                    BlinkEffects.Radius(_character));
            }
        });

        private Character? Real()
        {
            ZDOID id = CloneStore.RealOf(_controller.View.GetZDO());
            GameObject? go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
            return go != null ? go.GetComponent<Character>() : null;
        }
    }
}
