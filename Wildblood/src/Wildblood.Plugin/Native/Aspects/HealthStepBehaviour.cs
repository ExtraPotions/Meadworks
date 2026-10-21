// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The shape two boss aspects share: something happens each time the boss's health falls past another mark -
    /// Summoner's waves, Phantom's splits. The marks already passed are counted in the boss's ZDO, so an ownership
    /// hand-over neither repeats nor skips one, and healing back above a mark never re-arms it. A hit that crosses two
    /// marks fires twice. Attached on every machine, it acts only on the boss's owner and never once the boss is at
    /// zero, so nothing arrives over its body.
    /// </summary>
    public abstract class HealthStepBehaviour : MonoBehaviour
    {
        private const float Interval = 0.25f;

        private float _timer;
        private string _label = "";

        protected Character Character { get; private set; } = null!;
        protected EliteController Controller { get; private set; } = null!;

        protected void Start()
        {
            Character = GetComponent<Character>();
            Controller = GetComponent<EliteController>();
            _label = GetType().Name + ".Update";
        }

        protected void Update() => Guard.Run(_label, static self => self.Step(), this);

        /// <summary>How many marks a boss at this fraction of its maximum health has passed.</summary>
        protected abstract int Passed(float fraction);

        /// <summary>How many marks have already fired, read from the boss's ZDO.</summary>
        protected abstract int Counted(ZDO zdo);

        protected abstract void Count(ZDO zdo, int passed);

        /// <summary>What one mark brings, on the boss's owner.</summary>
        protected abstract void Fire();

        private void Step()
        {
            if (Character == null || Controller == null || !Controller.IsOwner() || Character.IsDead())
            {
                return;
            }
            _timer += Time.deltaTime;
            if (_timer < Interval)
            {
                return;
            }
            _timer = 0f;
            FireDue();
        }

        private void FireDue()
        {
            float fraction = Character.GetHealthPercentage();
            ZDO zdo = Controller.View.GetZDO();
            if (fraction <= 0f || zdo == null)
            {
                return;
            }
            int done = Counted(zdo);
            int due = Passed(fraction);
            if (due <= done)
            {
                return;
            }
            Count(zdo, due); // counted before firing, so a throw mid-way can never repeat a mark
            for (int mark = done; mark < due; mark++)
            {
                Fire();
            }
        }
    }
}
