// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Blinking: every `every` seconds of combat it vanishes and reappears `distance` metres behind its target, and the
    /// destination is marked `tell time` seconds before it arrives, so a player paying attention can turn in time.
    /// Decided on the owner, which holds the AI and the position: it keeps the combat clock (<see cref="BlinkClock"/>),
    /// picks the spot (<see cref="BlinkSpot"/>), sends the tell, checks again when the tell runs out, then sends the
    /// blink and moves the creature (<see cref="BlinkMove"/>). Drawn on every machine that holds the creature, from one
    /// RPC on its own ZNetView: the tell, the two puffs, and the veil that hides the body until the jump lands there
    /// (<see cref="BlinkVeil"/>). Attached everywhere; a hand-over mid-tell drops the pending blink with the old owner
    /// (the marker fizzles), and the new owner reads the last tell's time from the ZDO, so it never blinks twice.
    /// </summary>
    public sealed class BlinkBehaviour : MonoBehaviour
    {
        /// <summary>The one message, to every client holding the creature: a tell or the blink, and where.</summary>
        public const string Rpc = "ecr_blink";

        private const int PhaseTell = 0;
        private const int PhaseBlink = 1;

        /// <summary>Seconds to wait when the moment is wrong: mid-swing, staggered, or its target unsensed.</summary>
        private const float BusyRetry = 0.5f;

        /// <summary>Seconds to wait when no spot behind the target will do: time for the fight to move.</summary>
        private const float NoSpotRetry = 3f;

        /// <summary>A target this much beyond `distance` from the spot when the tell ends has got away.</summary>
        private const float LeaveSlack = 6f;

        private Character _character = null!;
        private EliteController _controller = null!;
        private BaseAI _ai = null!;
        private BlinkClock _clock = null!;
        private readonly BlinkVeil _veil = new BlinkVeil();
        private float _distance = 4f;
        private float _tellTime = 0.5f;
        private string _blinkEffect = "";
        private string _tellEffect = "";
        private string _tellSound = "";
        private Character? _target;
        private Vector3 _dest;
        private float _arriveAt;

        /// <summary>True while the body is hidden mid-blink, for anything that should hide with it.</summary>
        public bool Veiled => isActiveAndEnabled && _veil.Active;

        private void Start() => Guard.Run("BlinkBehaviour.Start", Setup);

        private void Setup()
        {
            _character = GetComponent<Character>();
            _controller = GetComponent<EliteController>();
            if (_character == null || _controller == null || _controller.View == null || !_controller.View.IsValid())
            {
                enabled = false;
                return;
            }
            _ai = _character.GetBaseAI();
            Read(_controller.Rules);
            if (!NativeRpcOwnership.View(_controller.View, NativeRpcOwner.CreatureBlink, () => _controller.View.Register<int, Vector3>(Rpc, OnMessage), Rpc))
            {
                enabled = false;
                return;
            }
            PlateVeils.Join(_character, this); // its nameplate hides while the body is veiled mid-blink
        }

        private void OnDisable()
        {
            _target = null;
            _clock?.Reset();
            _veil.Lift();
        }

        private void OnDestroy()
        {
            OnDisable();
            PlateVeils.Leave(_character, this);
        }

        private void Read(BiomeRules rules)
        {
            _clock = new BlinkClock(rules.PowerOf(Mutation.Blinking, Fields.Every)); // none of these is star-enhanced
            _distance = Mathf.Max(1f, rules.PowerOf(Mutation.Blinking, Fields.Distance));
            _tellTime = Mathf.Max(0f, rules.PowerOf(Mutation.Blinking, Fields.TellTime));
            _blinkEffect = rules.PrefabOf(Mutation.Blinking, Fields.BlinkEffect);
            _tellEffect = rules.PrefabOf(Mutation.Blinking, Fields.TellEffect);
            _tellSound = rules.PrefabOf(Mutation.Blinking, Fields.TellSound);
        }

        private void Update() => Guard.Run("BlinkBehaviour.Update", static self => self.Step(), this);

        private void Step()
        {
            _veil.Tick(_controller.View, transform, Time.deltaTime); // every machine: unveil once the jump lands here
            if (!NativeRpcOwnership.OwnsView(_controller.View, NativeRpcOwner.CreatureBlink))
            {
                Drop();
                return;
            }
            if (_ai == null || _character.IsDead() || !_controller.IsOwner())
            {
                Drop(); // owner-only from here: the AI and the position belong to the owner
                return;
            }
            if (_target != null)
            {
                Follow();
            }
            else if (_clock.Tick(Quarry() != null, _controller.View.GetZDO(), Time.deltaTime))
            {
                Begin();
            }
        }

        /// <summary>Not the owner (any more), or dead: a pending tell is dropped and the fight forgotten.</summary>
        private void Drop()
        {
            _target = null;
            _clock.Reset();
        }

        /// <summary>
        /// What it is fighting, or null: a live enemy its AI holds as its target while alerted. A tamed creature's AI
        /// only ever targets enemies, and a player is never a tamed creature's quarry, so it blinks behind what it
        /// fights for its owner and never behind a friend. An animal's AI never holds a target, so it never blinks.
        /// </summary>
        private Character? Quarry()
        {
            Character target = _ai.GetTargetCreature();
            if (target == null || target == _character || target.IsDead() || !_ai.IsAlerted())
            {
                return null;
            }
            if (_character.IsTamed() && target.IsPlayer())
            {
                return null;
            }
            return BaseAI.IsEnemy(_character, target) ? target : null;
        }

        /// <summary>A blink is due: pick the spot and send the tell, or put it off a few seconds.</summary>
        private void Begin()
        {
            Character? target = Quarry();
            if (target == null || !CanBlink(target))
            {
                _clock.Retry(BusyRetry);
                return;
            }
            if (!BlinkSpot.Find(_character, _ai, target, _distance, out Vector3 dest))
            {
                _clock.Retry(NoSpotRetry);
                return;
            }
            _clock.Spent(_controller.View.GetZDO());
            _target = target;
            _dest = dest;
            _arriveAt = Time.time + _tellTime;
            Send(PhaseTell, dest);
        }

        /// <summary>Not mid-swing, staggered, ridden or latched on, and able to see or hear its target now.</summary>
        private bool CanBlink(Character target) =>
            !_character.InAttack() && !_character.IsStaggering() && !_character.HaveRider() && !_character.IsAttached()
            && _ai.CanSenseTarget(target);

        /// <summary>During the tell: call it off if the target is lost; blink the moment the tell runs out.</summary>
        private void Follow()
        {
            Character target = _target!;
            if (!StillOn(target))
            {
                _target = null; // died, got away, or the AI let it go: the marker fizzles and nothing moves
                return;
            }
            if (Time.time < _arriveAt)
            {
                return;
            }
            _target = null;
            if (BlinkSpot.IsClear(_character, _dest)) // someone may have stepped onto the spot during the tell
            {
                Send(PhaseBlink, _dest); // drawn here at once, while it still stands where it vanishes
                BlinkMove.Jump(_character, _dest, target.transform.position);
            }
        }

        private bool StillOn(Character target) =>
            Quarry() == target && Vector3.Distance(target.transform.position, _dest) <= _distance + LeaveSlack;

        /// <summary>
        /// To every client holding the creature: <c>Everybody</c> through its own ZNetView is delivered here at once and
        /// relayed by the server to every player, and any machine that does not hold the creature drops it unread - the
        /// same path as the Warding tell.
        /// </summary>
        private void Send(int phase, Vector3 dest) =>
            _controller.View.InvokeRPC(ZRoutedRpc.Everybody, Rpc, phase, dest);

        private void OnMessage(long sender, int phase, Vector3 dest) =>
            Guard.Run("BlinkBehaviour.Draw", () => Draw(phase, dest));

        /// <summary>Every machine: the tell at the spot, or the blink - two puffs, then the veil.</summary>
        private void Draw(int phase, Vector3 dest)
        {
            if (!isActiveAndEnabled || _controller == null || !_controller.Ready
                || !_controller.Traits.Has(Mutation.Blinking) || _character == null || BlinkEffects.Headless())
            {
                return;
            }
            float radius = BlinkEffects.Radius(_character);
            if (phase == PhaseTell)
            {
                BlinkEffects.Tell(_tellEffect, _tellSound, dest, radius);
                return;
            }
            BlinkEffects.Blink(_blinkEffect, transform.position, dest, radius);
            _veil.Drop(gameObject, dest);
        }
    }
}
