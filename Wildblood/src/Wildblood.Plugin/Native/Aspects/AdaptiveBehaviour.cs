// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Adaptive: the boss takes `resist` percent less of whichever damage type players and their allies dealt it most
    /// in the last `window` seconds, and glows that type's colour. Attached on every machine. The owner keeps the
    /// rolling window (fed by <see cref="AdaptiveResist"/> from each hit), re-reads the dominant type as old hits age
    /// out, and writes it to the boss's ZDO only when it changes; every client reads it back from there to colour the
    /// glow, so all agree. The window itself is never sent: a machine that takes the boss over starts a fresh one
    /// holding a token of the stored type, so the boss keeps resisting and glowing as it was for one window, until the
    /// fight says otherwise.
    /// </summary>
    public sealed class AdaptiveBehaviour : MonoBehaviour
    {
        /// <summary>How often the owner re-reads the window as hits age out of it.</summary>
        private const float TickSeconds = 0.25f;

        /// <summary>The stored type's token on a new owner: enough to hold it, less than any real hit.</summary>
        private const float CarryOver = 0.01f;

        private readonly AdaptiveWindow _window = new AdaptiveWindow();
        private EliteController _controller = null!;
        private AdaptiveGlow? _glow;
        private bool _owned;
        private float _timer;

        /// <summary>The type the boss resists now, from its ZDO: the same answer on every machine.</summary>
        public AdaptiveType Current => AdaptiveTypes.Read(Zdo());

        // Awake, not Start: the component is added mid-frame as the boss resolves, and a hit can land before Start.
        private void Awake() =>
            Guard.Run("AdaptiveBehaviour.Awake", () => _controller = GetComponent<EliteController>());

        private void Start() => Guard.Run("AdaptiveBehaviour.Start", Build);

        private void Build()
        {
            Character character = GetComponent<Character>();
            _glow = character != null ? AdaptiveGlow.Create(character) : null;
        }

        private void Update() => Guard.Run("AdaptiveBehaviour.Update", static self => self.Step(), this);

        private void Step()
        {
            if (Zdo() == null)
            {
                return;
            }
            if (_controller.IsOwner())
            {
                TakeOver();
                Tick();
            }
            else
            {
                _owned = false;
            }
            _glow?.Tick(Current);
        }

        /// <summary>Owner side of a hit: what it dealt, type by type, recorded and the dominant type re-read.</summary>
        public void Record(HitData.DamageTypes landing)
        {
            TakeOver();
            float now = Time.time;
            for (int i = 1; i <= AdaptiveTypes.Last; i++)
            {
                AdaptiveType type = (AdaptiveType)i;
                _window.Add(now, type, AdaptiveTypes.Amount(landing, type));
            }
            Decide();
        }

        /// <summary>On gaining the boss: a fresh window seeded with the stored type, so a hand-over changes nothing.
        /// </summary>
        private void TakeOver()
        {
            if (_owned)
            {
                return;
            }
            _owned = true;
            _window.Clear();
            _window.Add(Time.time, Current, CarryOver);
        }

        private void Tick()
        {
            _timer += Time.deltaTime;
            if (_timer >= TickSeconds)
            {
                _timer = 0f;
                Decide();
            }
        }

        private void Decide()
        {
            ZDO? zdo = Zdo();
            if (zdo == null || !_controller.IsOwner())
            {
                return;
            }
            AdaptiveType current = AdaptiveTypes.Read(zdo);
            AdaptiveType next = _window.Dominant(Time.time, AspectMath.Power(Aspect.Adaptive, Fields.Window), current);
            if (next != current)
            {
                AdaptiveTypes.Write(zdo, next); // the only write, and only on a change
            }
        }

        private ZDO? Zdo()
        {
            ZNetView? view = _controller != null ? _controller.View : null;
            return view != null && view.IsValid() ? view.GetZDO() : null;
        }
    }
}
