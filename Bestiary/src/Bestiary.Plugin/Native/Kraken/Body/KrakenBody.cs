// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// Draws the kraken on every machine, from its phase in the ZDO and the attacks its owner orders: the head (placed
    /// by <see cref="HeadMotion"/>) and the six long tentacles (each a <see cref="TentacleMotion"/>). Swimming, the
    /// tentacles trail from the head. Holding a ship, everything is placed in the ship's own space: the tentacles rise
    /// round the hull and strike across the deck, or grip the rail while the head is up. The blows land here, on each
    /// machine at its own moment (<see cref="KrakenImpacts"/>).
    /// </summary>
    public class KrakenBody : MonoBehaviour
    {
        public const string ModelName = "KrakenModel";
        public const string HeadName = "head";
        public const string TentaclePrefix = "tentacle_";
        public const int Tentacles = ShipHull.Anchors;
        private const float LetGo = 1.2f;       // seconds its tentacles take to leave a ship it lets go of

        private readonly TentacleMotion?[] _tentacles = new TentacleMotion?[Tentacles];
        private readonly ShipScene _scene = new ShipScene();
        private HeadMotion? _head;
        private Transform? _model;
        private KrakenPhase _phase = (KrakenPhase)(-1);
        private bool _letGo;                    // leaving a ship it held, rather than one it only chased
        private float _since, _time;

        public Character Character { get; private set; } = null!;
        public KrakenState State { get; private set; } = null!;
        public HeadRig? Rig { get; private set; }
        public ShipScene? Scene => _scene.Ship != null ? _scene : null;
        /// <summary>
        /// Always 1: the kraken is drawn at its own size whatever scale the creature is given (a size mutation from Elite
        /// Creatures Reborn, a level's growth), since its fight is measured against the ship it holds.
        /// </summary>
        public float Scale => 1f;

        private float Age => Time.time - _since;

        private void Awake()
        {
            Character = GetComponent<Character>();
            State = new KrakenState(GetComponent<ZNetView>());
            Transform? model = transform.Find(ModelName);
            _model = model;
            Transform? head = model != null ? model.Find(HeadName) : null;
            Rig = head != null ? new HeadRig(head) : null;
            _head = Rig != null ? new HeadMotion(Rig) : null;
            for (int i = 0; i < Tentacles && model != null; i++)
            {
                Transform? arm = model.Find(TentaclePrefix + i);
                TentacleRig? rig = arm != null ? TentacleRig.Find(arm) : null;
                _tentacles[i] = rig != null ? new TentacleMotion(i, rig) : null;
            }
        }

        private void LateUpdate()
        {
            if (_head == null || Character == null || Character.IsDead())
            {
                return;
            }
            float dt = Time.deltaTime;
            _time += dt;
            Unscale();
            Track(State.Phase);
            bool held = State.Grips || (_letGo && Age < LetGo);
            Ship? ship = held ? KrakenShips.Find(State.Ship) : null;
            if (ship != null)
            {
                _scene.Track(ship, dt);
                AtShip(_head, dt);
            }
            else
            {
                _scene.Forget();
                Swimming(_head, dt);
            }
        }

        // The model back to its own size under a scaled creature.
        private void Unscale()
        {
            Vector3 scale = transform.lossyScale;
            if (_model != null && (scale - Vector3.one).sqrMagnitude > 1e-6f)
            {
                _model.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            }
        }

        /// <summary>The owner ordered tentacle <paramref name="i"/> to strike towards a point in the ship's space.</summary>
        public void Slam(int i, Vector3 target, StrikeKind kind)
        {
            TentacleMotion? arm = i >= 0 && i < Tentacles ? _tentacles[i] : null;
            if (arm == null || !Ready())
            {
                return;
            }
            arm.Begin(new TentacleStrike(i, target, kind, _scene.Deck(i, target), arm.Shown));
            if (kind == StrikeKind.Grab)
            {
                KrakenEffects.Warning(_scene.World(_scene.Anchor(i)));
            }
        }

        /// <summary>The owner ordered the head to bite or squirt at a point in the ship's space.</summary>
        public void Attack(HeadAction.Kind kind, Vector3 target)
        {
            if (_head != null && Ready())
            {
                _head.Begin(new HeadAction(kind, target));
            }
        }

        /// <summary>The kraken staggered: whatever was about to land is pulled back.</summary>
        public void Flinch()
        {
            _head?.Flinch();
            foreach (TentacleMotion? arm in _tentacles)
            {
                arm?.Flinch();
            }
        }

        // An order can arrive before this machine has placed the ship this frame.
        private bool Ready()
        {
            Ship? ship = KrakenShips.Find(State.Ship);
            if (ship != null && State.Grips && _scene.Ship != ship)
            {
                _scene.Track(ship, 0f);
            }
            return _scene.Ship != null && _scene.Ship == ship;
        }

        private void Track(KrakenPhase phase)
        {
            if (phase == _phase)
            {
                return;
            }
            bool seen = _phase >= 0;
            _letGo = phase == KrakenPhase.Leave && KrakenState.IsGrip(_phase);
            _phase = phase;
            _since = Time.time;
            _head!.Where = Placing(phase);
            Ship? ship = KrakenShips.Find(State.Ship);
            for (int i = 0; i < Tentacles; i++)
            {
                float? grip = phase == KrakenPhase.Head ? KrakenTargets.GripAlong(ship, State.Side, State.Along, i) : null;
                _tentacles[i]?.Set(ModeFor(phase, grip != null), grip);
            }
            if (seen)
            {
                KrakenImpacts.Entered(this, phase);
            }
        }

        private void AtShip(HeadMotion head, float dt)
        {
            float scale = Scale;
            if (_phase == KrakenPhase.Leave)
            {
                head.Swimming(transform, scale, Age, _time, dt);
            }
            else
            {
                KrakenImpacts.Fired(this, head.AtShip(_scene, State.Side, State.Along, scale, _time, dt));
            }
            for (int i = 0; i < Tentacles; i++)
            {
                TentacleMotion? arm = _tentacles[i];
                if (arm != null && _phase == KrakenPhase.Dive)
                {
                    arm.Trailing(Trail(i, scale), _time, dt);
                }
                else if (arm != null)
                {
                    KrakenImpacts.Landed(this, arm, arm.AtShip(_scene, scale, _time, dt));
                }
            }
        }

        private void Swimming(HeadMotion head, float dt)
        {
            float scale = Scale;
            head.Swimming(transform, scale, Age, _time, dt);
            for (int i = 0; i < Tentacles; i++)
            {
                _tentacles[i]?.Trailing(Trail(i, scale), _time, dt);
            }
        }

        // From the head's joint for tentacle i, backwards from where the head faces, fanned out by the joint's side.
        private TentacleFrame Trail(int i, float scale)
        {
            Transform joint = Rig!.Joint(i);
            Transform head = Rig.Root;
            Vector3 back = Vector3.ProjectOnPlane(-head.forward, Vector3.up).normalized;
            float fan = Mathf.Clamp(head.InverseTransformPoint(joint.position).x * 0.35f, -0.8f, 0.8f);
            return new TentacleFrame(joint.position, back + Vector3.Cross(Vector3.up, back) * fan, Vector3.up, scale);
        }

        private static HeadMotion.Place Placing(KrakenPhase phase)
        {
            switch (phase)
            {
                case KrakenPhase.Lurk: return HeadMotion.Place.Lurk;
                case KrakenPhase.Hunt: return HeadMotion.Place.Hunt;
                case KrakenPhase.Leave: return HeadMotion.Place.Sink;
                case KrakenPhase.Head: return HeadMotion.Place.Beside;
                default: return HeadMotion.Place.Under;
            }
        }

        private static TentacleMotion.Mode ModeFor(KrakenPhase phase, bool grips)
        {
            switch (phase)
            {
                case KrakenPhase.Tentacles: return TentacleMotion.Mode.Up;
                case KrakenPhase.Head: return grips ? TentacleMotion.Mode.Grip : TentacleMotion.Mode.Down;
                case KrakenPhase.Leave: return TentacleMotion.Mode.Down;
                default: return TentacleMotion.Mode.Trail;
            }
        }
    }
}
