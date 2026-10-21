// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Tethered: the pull between the two bosses of a pair. Attached to both on every machine, it measures each frame how
    /// far apart their health has drawn (<see cref="TetherPair"/>, read from the ZDOs, so every machine agrees without a
    /// message) and turns that gap into the aspect's two powers. Both attack faster the wider the gap (read by
    /// <c>AnimSpeedPatch</c>, which animates locally on every machine), and the weaker of the two - the one with less of
    /// its health left - takes less damage (<see cref="Brace"/>, on its owner, measured at the hit itself). It also draws
    /// the tether between them on every client (<see cref="TetherLine"/>), from whichever of the two sorts first by ZDO id,
    /// so a pair shows one line, never two.
    /// </summary>
    public sealed class TetherLink : MonoBehaviour
    {
        /// <summary>The slowest a misconfigured negative `attack speed` may make a boss animate, so it never freezes.</summary>
        private const float MinSwing = 0.1f;

        /// <summary>Seconds between looks for a partner this machine does not hold (dead, or not loaded here).</summary>
        private const float FindRetry = 1f;

        // The link of each Tethered boss loaded here, so the per-step speed read and the per-hit brace find it with one
        // lookup rather than a component search.
        private static readonly Dictionary<EliteController, TetherLink> Links = new Dictionary<EliteController, TetherLink>();

        private Character _character = null!;
        private ZNetView _nview = null!;
        private ZDOID _partnerId = ZDOID.None;
        private Character? _partner;
        private bool _leads;
        private TetherLine? _line;
        private EliteController? _controller;
        private float _nextFind;

        /// <summary>How taut the tether is on this machine this frame, 0 to 1; 0 until the pair is known.</summary>
        public float Strength { get; private set; }

        private void Start()
        {
            _character = GetComponent<Character>();
            _nview = GetComponent<ZNetView>();
            _controller = GetComponent<EliteController>();
            if (_controller != null)
            {
                Links[_controller] = this;
            }
            _line = TetherLine.Create();
        }

        private void Update() => Guard.Run("TetherLink.Update", static self => self.Measure(), this);

        // After the bodies have moved this frame, so the line's ends sit on them rather than a frame behind.
        private void LateUpdate() => Guard.Run("TetherLink.LateUpdate", static self => self.Draw(), this);

        // `is not null` rather than Unity's ==: the controller may already read as destroyed here, and its entry must go.
        private void OnDisable() => _line?.Hide();

        private void OnDestroy()
        {
            _line?.Dispose();
            if (_controller is not null)
            {
                Links.Remove(_controller);
            }
        }

        private void Measure()
        {
            Character? partner = Partner();
            Strength = _partnerId == ZDOID.None
                ? 0f
                : TetherPair.Strength(TetherPair.Fraction(_character), TetherPair.Fraction(partner));
        }

        private void Draw()
        {
            if (_line == null)
            {
                return;
            }
            if (_leads && TetherPair.Alive(_character) && TetherPair.Alive(_partner))
            {
                _line.Draw(_character.GetCenterPoint(), _partner!.GetCenterPoint(), Strength);
            }
            else
            {
                _line.Hide();
            }
        }

        /// <summary>The other boss as this machine holds it, looked up again whenever the one held is gone - at most once
        /// every <see cref="FindRetry"/> while it cannot be found, as after it has died.</summary>
        private Character? Partner()
        {
            if (_partnerId == ZDOID.None)
            {
                ReadPartner();
            }
            if (!TetherPair.Alive(_partner) && Time.time >= _nextFind)
            {
                _partner = TetherPair.Find(_partnerId);
                _nextFind = _partner == null ? Time.time + FindRetry : 0f;
            }
            return _partner;
        }

        // The partner key arrives with the ZDO and never changes once there; until it does this boss stays unpaired.
        private void ReadPartner()
        {
            ZDO? zdo = _nview != null && _nview.IsValid() ? _nview.GetZDO() : null;
            if (zdo == null)
            {
                return;
            }
            _partnerId = AspectStore.GetTether(zdo);
            _leads = _partnerId != ZDOID.None && zdo.m_uid.CompareTo(_partnerId) < 0;
        }

        /// <summary>
        /// For <c>AnimSpeedPatch</c>, on every machine: how much faster the tether makes this creature animate, up to
        /// `attack speed`% at full strength; 1 for anything that is not a Tethered boss.
        /// </summary>
        public static float SwingFactor(EliteController controller)
        {
            if (!controller.Traits.HasAspect(Aspect.Tethered))
            {
                return 1f;
            }
            if (!Links.TryGetValue(controller, out TetherLink link))
            {
                return 1f;
            }
            float boost = AspectMath.Boost(AspectMath.Power(Aspect.Tethered, Fields.AttackSpeed) * link.Strength);
            return Mathf.Max(MinSwing, boost);
        }

        /// <summary>
        /// From <see cref="AspectDamage.Incoming"/> on the victim's owner: the weaker of the pair takes up to `armour`%
        /// less, by how taut the tether is. Measured at the hit, so two blows in one frame each see the health the
        /// last one left.
        /// </summary>
        public static void Brace(EliteController victim, HitData hit)
        {
            float cut = Links.TryGetValue(victim, out TetherLink link) ? link.ArmourNow() : 0f;
            if (cut > 0f)
            {
                hit.ApplyModifier(AspectMath.Cut(cut));
            }
        }

        private float ArmourNow()
        {
            Character? partner = Partner();
            if (_partnerId == ZDOID.None || !TetherPair.Alive(_character))
            {
                return 0f;
            }
            float own = TetherPair.Fraction(_character);
            float other = TetherPair.Fraction(partner);
            return own < other ? AspectMath.Power(Aspect.Tethered, Fields.Armour) * TetherPair.Strength(own, other) : 0f;
        }
    }
}
