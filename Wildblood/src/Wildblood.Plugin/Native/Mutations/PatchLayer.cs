// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Flame visuals adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Config;
using ExtraPotions.Wildblood.Visuals;
using System.Collections.Generic;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Every patch of one trail kind this machine has laid, from every creature: the patches themselves (where, how
    /// wide, until when, how strong), their drawing (<see cref="PatchDecals"/>, one per decal effect in use) and what
    /// they do to this machine's player (<see cref="PatchFooting"/>), looked at ten times a second. A patch is this
    /// machine's own from the moment it is laid, so it lasts its full life and fades out normally after the creature
    /// that laid it dies or leaves; and since a player is only ever slowed by patches their own machine has drawn,
    /// nothing slows them unseen. One per kind, made the first time a trail is drawn in a world, and gone with the
    /// world's scene (taking its patches with it). Never on a dedicated server, which draws nothing and has no player.
    /// </summary>
    internal sealed class PatchLayer : MonoBehaviour
    {
        /// <summary>The most patches of one kind this machine keeps; beyond it the oldest goes first.</summary>
        private const int MaxPatches = 1000;

        private const float CheckEvery = 0.1f;

        private static readonly PatchLayer?[] Layers = new PatchLayer?[TrailKind.All.Length];

        private PatchFlames? _flames;

        private readonly List<GroundPatch> _patches = new List<GroundPatch>();
        private readonly Dictionary<string, PatchDecals> _decals = new Dictionary<string, PatchDecals>();
        private TrailKind _kind = null!;
        private PatchFooting _footing = null!;
        private Predicate<GroundPatch> _expired = null!;
        private Action _tick = null!;
        private float _now;
        private float _nextCheck;

        /// <summary>This machine's layer for <paramref name="kind"/>, made on first use in a world.</summary>
        public static PatchLayer For(TrailKind kind)
        {
            PatchLayer? layer = Layers[kind.Index];
            if (layer != null)
            {
                return layer;
            }
            layer = new GameObject($"ecr_{kind.Name}_patches").AddComponent<PatchLayer>();
            layer.Init(kind);
            Layers[kind.Index] = layer;
            return layer;
        }

        private void Init(TrailKind kind)
        {
            _kind = kind;
            _footing = new PatchFooting(kind);
            _expired = patch => patch.Until <= _now;
            _tick = Tick;
        }

        /// <summary>
        /// Lays one patch for drop <paramref name="id"/>, dropped at <paramref name="at"/>: on the ground below it,
        /// drawn, and felt by this machine's player for the <paramref name="remaining"/> seconds it has left.
        /// </summary>
        public void Lay(long id, Vector3 at, TrailSpec spec, float remaining)
        {
            GroundProbe.Find(at, out Vector3 point, out Vector3 normal);
            if (_patches.Count >= MaxPatches)
            {
                _patches.RemoveAt(0);
            }
            _patches.Add(new GroundPatch
            {
                Point = point, RadiusSq = spec.Radius * spec.Radius, Until = Time.time + remaining, Slow = spec.Slow,
                Grip = spec.Grip, Burn = spec.Burn,
            });
            if (_kind.Burns && _flames == null) _flames = PatchFlames.Build(transform);
            PatchDecals decals = Decals(spec.Effect);
            decals.Draw(point, normal, spec.Radius * 2f, remaining, _kind.Colour, id);
            if (_kind.Burns) decals.Show(FlameVisualPolicy.DrawDecals(_flames != null, Configuration.EffectDensity.Value));
        }

        private PatchDecals Decals(string effect)
        {
            if (!_decals.TryGetValue(effect, out PatchDecals decals))
            {
                decals = PatchDecals.Build(DecalSource.Find(effect, _kind), transform, _kind);
                _decals[effect] = decals;
            }
            return decals;
        }

        private void Update() => Guard.Run("PatchLayer.Update", _tick);

        private void Tick()
        {
            if (Time.time < _nextCheck)
            {
                return;
            }
            _nextCheck = Time.time + CheckEvery;
            float elapsed = Mathf.Min(Time.time - _now, 1f);
            _now = Time.time;
            _patches.RemoveAll(_expired);
            _footing.Tick(Player.m_localPlayer, _patches);
            _flames?.Tick(_patches, elapsed);
            if (_kind.Burns)
            {
                bool show = FlameVisualPolicy.DrawDecals(_flames != null, Configuration.EffectDensity.Value);
                foreach (PatchDecals decals in _decals.Values) decals.Show(show);
            }
        }

        private void OnDestroy() => _footing?.Drop();
    }
}
