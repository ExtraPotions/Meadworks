// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Nightfall's tornadoes (its storming midnight is <see cref="NightfallSky"/>'s): every `every` to `every max`
    /// seconds of a fight one tornado rises a few metres from each player within `range` and whirls into existence over
    /// `form time` seconds, harmless; then it hunts its player across the ground at `tornado speed` % of a player's run
    /// speed until `life` seconds after it rose, and tears at any player inside its funnel - `base width` across at the
    /// ground, `top width` at its top, `height` tall - for `damage` a second. Attached by AspectInstaller on every
    /// machine. On the boss's owner it decides when and where and moves the tornadoes (<see cref="TornadoOwner"/>),
    /// self-gating on live ownership so the decision and the hunt move with the boss; on every client with a screen it
    /// draws them and judges its own player (<see cref="TornadoView"/>). The two meet only in what the owner writes
    /// into the boss's ZDO (<see cref="TornadoStore"/>). Every Nightfall boss loaded here is listed, so no player is
    /// ever hunted by two tornadoes at once, whichever bosses raised them.
    /// </summary>
    public sealed class NightfallStorm : MonoBehaviour
    {
        private static readonly List<NightfallStorm> Live = new List<NightfallStorm>();

        private ZNetView? _netView;
        private TornadoOwner? _owner;
        private TornadoView? _view;

        private void Start() => Guard.Run("NightfallStorm.Start", Setup);

        private void Setup()
        {
            EliteController controller = GetComponent<EliteController>();
            Character boss = GetComponent<Character>();
            if (controller == null || boss == null || controller.View == null || !controller.View.IsValid())
            {
                enabled = false;
                return;
            }
            _netView = controller.View;
            _owner = new TornadoOwner(controller, boss, boss.GetBaseAI());
            if (!StormEffects.Headless())
            {
                _view = new TornadoView(controller.View, boss, _owner.Hunt);
            }
            Live.Add(this);
        }

        private void Update() => Guard.Run("NightfallStorm.Update", static self => self.Step(), this);

        private void Step()
        {
            _owner?.Tick();
            _view?.Tick();
        }

        private void OnDisable() => _view?.Dispose();

        private void OnDestroy()
        {
            Live.Remove(this);
            _view?.Dispose();
        }

        /// <summary>Whether a live tornado of any Nightfall boss loaded here is hunting this player.</summary>
        public static bool Hunted(ZDOID player)
        {
            long now = NetTime.NowMs();
            foreach (NightfallStorm storm in Live)
            {
                if (storm != null && storm.isActiveAndEnabled && storm.GetComponent<EliteController>() is EliteController controller
                    && controller.Ready && storm.Hunts(player, now))
                {
                    return true;
                }
            }
            return false;
        }

        private bool Hunts(ZDOID player, long now)
        {
            ZDO? zdo = _netView != null && _netView.IsValid() ? _netView.GetZDO() : null;
            TornadoWave? wave = zdo != null ? TornadoStore.Read(zdo) : null;
            return wave != null && wave.Hunts(player, now);
        }
    }
}
