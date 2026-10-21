// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// One Nightfall tornado, drawn locally on each client. It rises where the owner put it and whirls into existence
    /// over the wave's `form time`: a low, thin whirl in a skirt of kicked-up dust that grows to its full height and
    /// width, its wind rising with it; as it finishes forming it throws out a ring of dust. Then
    /// it glides after the place its view gives it each frame (<see cref="TornadoView"/>), laid on the ground of this
    /// machine's own world, its top trailing behind as it moves and a cold light in its foot flaring with the lightning
    /// inside it. When it breaks up it stops feeding, widens and fades with its wind over <see cref="FadeTime"/>
    /// seconds, then goes back to <see cref="TornadoPool"/> to be raised again. It lives apart from the boss, so a boss
    /// that dies or unloads leaves its tornadoes to break up on their own rather than vanish; one its view stops
    /// following breaks up by itself. Each raising is a new lease, so a view can only steer the tornado it raised.
    /// </summary>
    internal sealed class TornadoVisual : MonoBehaviour
    {
        private const float FadeTime = 1.5f;

        /// <summary>How quickly the drawn tornado closes on where it should be, and on the ground under it.</summary>
        private const float Smoothing = 8f;
        private const float GroundSmoothing = 10f;

        /// <summary>Metres off at which it jumps rather than glides.</summary>
        private const float Snap = 8f;

        /// <summary>How far its top trails per metre a second it moves, at most, and how fast that settles.</summary>
        private const float TrailPerSpeed = 0.3f;
        private const float MaxTrail = 2.5f;
        private const float TrailSmoothing = 3f;

        /// <summary>The wind's loudness as it starts to form, and at its full voice.</summary>
        private const float FirstWind = 0.3f;
        private const float FullWind = 1f;

        private const float Glow = 0.6f;
        private const float Flare = 4f;
        private const float MinFlash = 0.8f;
        private const float MaxFlash = 3f;

        /// <summary>Seconds without word from its view after which it breaks up by itself.</summary>
        private const float Orphaned = 1f;

        private const int RingOfDust = 24;

        private TornadoSwirl _swirl = null!;
        private TornadoCone[] _cones = System.Array.Empty<TornadoCone>();
        private TornadoShape _shape;
        private Light _light = null!;
        private AudioSource _wind = null!;
        private Vector3 _target;
        private Vector3 _trail;
        private float _groundY;
        private float _form;
        private float _age;
        private float _fadeAt = -1f;
        private float _followed;
        private float _density;
        private float _flash;
        private float _flashAge = 1f;
        private float _nextFlash;
        private bool _formed;

        /// <summary>Changes each time it is raised or put away, so a view steers only the tornado it raised.</summary>
        public int Lease { get; private set; }

        /// <summary>Where its funnel touches the ground, as this client draws it - and judges its own player by.</summary>
        public Vector3 Foot => transform.position;

        public void Build(TornadoSwirl swirl, TornadoCone[] cones, Light light, AudioSource wind)
        {
            _swirl = swirl;
            _cones = cones;
            _light = light;
            _wind = wind;
        }

        /// <summary>Raised at <paramref name="at"/>, <paramref name="age"/> seconds into its life.</summary>
        public void Begin(Vector3 at, TornadoShape shape, float form, float age)
        {
            Lease++;
            _form = form;
            _age = age;
            _formed = age >= form;
            _fadeAt = -1f;
            _followed = Time.time;
            _trail = Vector3.zero;
            _nextFlash = Random.Range(MinFlash, MaxFlash);
            _density = StormEffects.Density();
            _groundY = TornadoTargets.Ground(at, at.y);
            transform.position = new Vector3(at.x, _groundY, at.z);
            _target = transform.position;
            gameObject.SetActive(true);
            _shape = shape;
            _swirl.Begin(shape, _density, Grow());
            StartWind();
        }

        /// <summary>From its view each frame: where it should be now and how old it is, on the shared clock.</summary>
        public void Follow(int lease, Vector3 target, float age)
        {
            if (lease != Lease || _fadeAt >= 0f)
            {
                return;
            }
            _target = target;
            _age = age;
            _followed = Time.time;
        }

        /// <summary>Its time is up, or its boss has fallen: it breaks up. Asking twice changes nothing.</summary>
        public void Dissipate(int lease)
        {
            if (lease == Lease)
            {
                Fade();
            }
        }

        private void Fade()
        {
            if (_fadeAt < 0f)
            {
                _fadeAt = Time.time;
                _swirl.Stop();
            }
        }

        private void LateUpdate() => Guard.Run("TornadoVisual.LateUpdate", static self => self.Step(), this);

        private void Step()
        {
            float dt = Time.deltaTime;
            if (Time.time - _followed > Orphaned)
            {
                Fade(); // its view is gone
            }
            Move(dt);
            float grow = Grow();
            float fade = _fadeAt < 0f ? 1f : 1f - Mathf.Clamp01((Time.time - _fadeAt) / FadeTime);
            if (!_formed && grow >= 1f)
            {
                Formed();
            }
            Flicker(dt, grow * fade);
            _swirl.Drive(grow, fade, _flash, _trail, dt);
            DriveCones(grow, fade, dt);
            _wind.volume = Mathf.Lerp(FirstWind, FullWind, grow) * fade;
            if (fade <= 0f)
            {
                TornadoPool.Return(this);
            }
        }

        // The funnel itself, bent with the particles round it and softened toward this machine's camera.
        private void DriveCones(float grow, float fade, float dt)
        {
            Camera camera = Utils.GetMainCamera();
            Vector3 eye = camera != null ? transform.InverseTransformPoint(camera.transform.position) : Vector3.forward;
            foreach (TornadoCone cone in _cones)
            {
                cone.Drive(_shape, grow, fade, _flash, _trail, _swirl.Time, dt, eye);
            }
        }

        // Glides toward its place (or jumps, when far off), on this machine's ground under it.
        private void Move(float dt)
        {
            Vector3 at = transform.position;
            Vector3 goal = new Vector3(_target.x, at.y, _target.z);
            Vector3 next = (goal - at).sqrMagnitude > Snap * Snap
                ? goal
                : Vector3.Lerp(at, goal, 1f - Mathf.Exp(-Smoothing * dt));
            float ground = TornadoTargets.Ground(next, _groundY);
            _groundY = Mathf.Abs(ground - _groundY) > Snap
                ? ground
                : Mathf.Lerp(_groundY, ground, 1f - Mathf.Exp(-GroundSmoothing * dt));
            next.y = _groundY;
            Trail(dt > 0f ? (next - at) / dt : Vector3.zero, dt);
            transform.position = next;
        }

        private void Trail(Vector3 velocity, float dt)
        {
            velocity.y = 0f;
            Vector3 behind = Vector3.ClampMagnitude(-velocity * TrailPerSpeed, MaxTrail);
            _trail = Vector3.Lerp(_trail, behind, 1f - Mathf.Exp(-TrailSmoothing * dt));
        }

        /// <summary>0 as it rises to 1 once formed, easing in and out.</summary>
        private float Grow()
        {
            float t = _form > 0f ? Mathf.Clamp01(_age / _form) : 1f;
            return t * t * (3f - 2f * t);
        }

        private void Formed()
        {
            _formed = true;
            _swirl.Burst(RingOfDust);
        }

        // Lightning inside it now and then (a sharp flash, a beat of dark, a weaker one), over a faint steady glow.
        private void Flicker(float dt, float strength)
        {
            _nextFlash -= dt;
            if (_nextFlash <= 0f)
            {
                _nextFlash = Random.Range(MinFlash, MaxFlash);
                _flashAge = 0f;
            }
            _flashAge += dt;
            float strike = _flashAge < 0.07f ? 1f : _flashAge < 0.13f ? 0f : _flashAge < 0.2f ? 0.6f : 0f;
            _flash = _density > 0f ? strike * strength : 0f;
            _light.intensity = (Glow * strength + Flare * _flash) * _density;
        }

        // From a random place in the loop, so two tornadoes rising together never gust in step.
        private void StartWind()
        {
            if (_wind.clip == null)
            {
                return;
            }
            _wind.volume = 0f;
            _wind.time = Random.Range(0f, _wind.clip.length);
            _wind.Play();
        }

        /// <summary>Put away by <see cref="TornadoPool"/>: particles, wind and light gone, switched off.</summary>
        public void Rest()
        {
            Lease++;
            _swirl.Clear();
            _wind.Stop();
            _light.intensity = 0f;
            gameObject.SetActive(false);
        }
    }
}
