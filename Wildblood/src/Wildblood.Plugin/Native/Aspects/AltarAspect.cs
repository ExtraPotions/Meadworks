// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
// Boss progression and material behavior adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
// Changed by expDARE, 2026: an altar whose boss is on Protected Prefabs offers no stars or aspect.
using ExtraPotions.Wildblood.Adopted.Display;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Runtime;
using RootPlugin = ExtraPotions.Wildblood.Runtime.Plugin;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The draw on one boss altar: the boss's stars and its aspect. It lives in the altar's own ZDO - the stars, the
    /// aspect and the world time of the next shift - so every player at the bowl reads the same thing and a restart does
    /// not shuffle it. The altar's owner rolls each part that is on the first time, and at each shift rolls both again,
    /// the aspect never to the one it already shows (the stars may repeat); every machine draws the hover lines from the
    /// ZDO, so no message is ever sent for them. A shift to Bountiful rolls its extras in the same moment and keeps them
    /// beside it in the ZDO, so the group reads exactly which aspects it will fight. At the offering the current stars and
    /// aspects are locked on this machine - the one that will instantiate the boss - so a shift during the few seconds
    /// before the boss appears cannot change the fight that was offered for. A part that is off (boss stars or aspects)
    /// is left as it is in the ZDO, and neither shown nor locked.
    /// </summary>
    public sealed class AltarAspect : MonoBehaviour
    {
        private const float Interval = 1f;

        /// <summary>Used for an in-game hour when the environment is not up yet: 1/24 of the default 30-minute day.</summary>
        private const double FallbackHourSeconds = 75.0;

        private OfferingBowl _bowl = null!;
        private string _boss = "";
        private float _timer;
        private BossDraw? _locked;

        // The hover lines as last drawn, kept while the bowl's ZDO, the whole second of the shift count-down and the
        // rule set (a reload replaces it) are the ones they were drawn from: the hover is asked for every frame.
        private string _hover = "";
        private uint _hoverRevision = uint.MaxValue;
        private int _hoverSecond = int.MinValue;
        private int _hoverLanguage = int.MinValue;
        private int _hoverKills = -1;
        private RuleSet? _hoverRules;

        /// <summary>Added to an altar that summons a boss, on every machine, as the bowl starts.</summary>
        public static void Attach(OfferingBowl bowl)
        {
            Character? boss = bowl.m_bossPrefab != null ? bowl.m_bossPrefab.GetComponent<Character>() : null;
            if (boss != null && boss.IsBoss() && bowl.GetComponent<AltarAspect>() == null)
            {
                bowl.gameObject.AddComponent<AltarAspect>();
            }
        }

        private void Start()
        {
            _bowl = GetComponent<OfferingBowl>();
            _boss = Utils.GetPrefabName(_bowl.m_bossPrefab);
        }

        private void Update() => Guard.Run("AltarAspect.Update", static self => self.Tick(), this);

        private void Tick()
        {
            if (!Open) { _locked = null; return; }
            _timer += Time.deltaTime;
            if (_timer < Interval)
            {
                return;
            }
            _timer = 0f;
            ZDO? zdo = OwnedZdo();
            if (zdo != null && (StarsOn || AspectsOn))
            {
                Refresh(zdo);
            }
        }

        // Owner only. A due shift rolls every part that is on and stores the next time; between shifts only a part that is
        // on but was never rolled is rolled, alone - so an altar from before stars came to altars shows its stars at once
        // and keeps its aspect and its shift time. While shifting is off the stored time is not moved, so switching it
        // back on shifts at once.
        private void Refresh(ZDO zdo)
        {
            double interval = RuleState.Active.Boss.Aspects.ShiftInterval(HourSeconds());
            long wait = AspectStore.GetAltarShiftAt(zdo) - NowMs();
            bool due = interval > 0.0 && (wait <= 0L || wait > (long)(interval * 1000.0)); // shortened since: shift now
            Roll(zdo, due);
            if (due)
            {
                AspectStore.SetAltarShiftAt(zdo, NowMs() + (long)(interval * 1000.0));
            }
        }

        /// <summary>Rolls the stars and the aspect, each only while it is on: both on a shift, else only a missing one.</summary>
        private void Roll(ZDO zdo, bool shift)
        {
            if (StarsOn && (shift || !AspectStore.AltarStarsRolled(zdo)))
            {
                AspectStore.SetAltarStars(zdo, BossDraw.RollStars(_boss));
            }
            if (AspectsOn && (shift || !AspectStore.AltarRolled(zdo)))
            {
                Aspect? current = AspectStore.AltarRolled(zdo) ? AspectStore.GetAltarAspect(zdo) : (Aspect?)null;
                AspectStore.SetAltarAspects(zdo, AspectRoller.RollBoss(_boss, current));
            }
        }

        /// <summary>At the offering, on the machine that will spawn the boss: fix the stars and aspects on the bowl now.</summary>
        public void Lock()
        {
            ZDO? zdo = OwnedZdo();
            if (zdo == null || !(StarsOn || AspectsOn))
            {
                _locked = null; // no altar state to read: the boss rolls its own, as an altar-less boss does
                return;
            }
            Roll(zdo, shift: false); // offered before a part's first roll landed: roll it now, so the bowl and the fight agree
            _locked = new BossDraw(StarsOn ? AspectStore.GetAltarStars(zdo) : 0,
                AspectsOn ? AspectStore.GetAltarAspects(zdo) : BossAspects.None);
        }

        public BossDraw? TakeLocked()
        {
            BossDraw? locked = Open && _locked.HasValue
                ? new BossDraw(StarsOn ? _locked.Value.Stars : 0, AspectsOn ? _locked.Value.Aspects : BossAspects.None)
                : (BossDraw?)null;
            _locked = null;
            return locked;
        }

        /// <summary>The lines appended to the bowl's hover text, drawn from the ZDO on whichever machine is looking, and
        /// drawn again only when what they show has changed.</summary>
        public string HoverLines()
        {
            if (!Open)
            {
                _hover = ""; _hoverRules = null; _hoverRevision = uint.MaxValue;
                return "";
            }
            ZDO? zdo = Zdo();
            if (zdo == null)
            {
                return "";
            }
            RuleSet set = RuleState.Active;
            double seconds = SecondsToShift(zdo, set.Boss.Aspects);
            int second = seconds < 0 ? -1 : Mathf.CeilToInt((float)seconds);
            int kills = ExtraPotions.Wildblood.Adopted.Runtime.BossProgress.Count(_boss);
            if (kills != _hoverKills || _hoverLanguage != ExtraPotions.Wildblood.Runtime.WildbloodText.LanguageVersion || zdo.DataRevision != _hoverRevision || second != _hoverSecond || !ReferenceEquals(set, _hoverRules))
            {
                _hoverLanguage = ExtraPotions.Wildblood.Runtime.WildbloodText.LanguageVersion;
                _hoverRevision = zdo.DataRevision;
                _hoverSecond = second;
                _hoverKills = kills;
                _hoverRules = set;
                _hover = DrawHover(zdo, set.Boss.Aspects, seconds) + ProgressionLine(kills,set.Boss.HarderEachKill);
            }
            return _hover;
        }

        private static string ProgressionLine(int kills,bool enabled)
        {
            string key=ExtraPotions.Wildblood.Bosses.BossProgression.HintKey(kills,enabled);
            if(key.Length==0)return "";
            string text=kills==1?ExtraPotions.Wildblood.Runtime.WildbloodText.Text(key):ExtraPotions.Wildblood.Runtime.WildbloodText.Format(key,kills);
            return "\n<color=#A0A0A0>"+text+"</color>";
        }

        private string DrawHover(ZDO zdo, AspectRules rules, double secondsToShift)
        {
            int? stars = StarsOn && AspectStore.AltarStarsRolled(zdo) ? AspectStore.GetAltarStars(zdo) : (int?)null;
            BossAspects? aspects = AspectsOn && AspectStore.AltarRolled(zdo) ? AspectStore.GetAltarAspects(zdo) : (BossAspects?)null;
            float factor=aspects is BossAspects drawn?ExtraPotions.Wildblood.Adopted.Loot.AspectLoot.FactorOf(drawn,rules):1f;
            return (stars==null&&aspects==null?"":AspectText.AltarLines(stars, aspects, rules, secondsToShift))
                +ExtraPotions.Wildblood.Adopted.Runtime.BossMaterialRewards.AltarLine(_boss,stars??0,factor);
        }

        /// <summary>Seconds until the next shift; negative when shifting is off and the altar is fixed.</summary>
        private static double SecondsToShift(ZDO zdo, AspectRules rules) =>
            rules.ShiftInterval(HourSeconds()) > 0.0 ? (AspectStore.GetAltarShiftAt(zdo) - NowMs()) / 1000.0 : -1.0;

        private static bool Available => RootPlugin.NativeRulesReady
            && ExtraPotions.Wildblood.AdoptedCm.NativeModifierRules.Ready
            && WildbloodModules.Active(WildbloodModule.Bosses);
        // A protected boss spawns plain (no controller), so its altar must not promise stars or an aspect.
        private bool Open => Available && !ExtraPotions.Wildblood.Runtime.ProtectedCreatures.IsProtected(_boss);

        private static bool StarsOn => Available && RuleState.Active.Boss.Enabled;

        private static bool AspectsOn => Available && RuleState.Active.Boss.Aspects.Enabled;

        internal void SuspendForShutdown()
        {
            _locked = null; _hover = ""; _hoverRules = null;
            enabled = false;
        }

        private ZDO? Zdo()
        {
            ZNetView? view = _bowl != null ? _bowl.m_nview : null;
            return view != null && view.IsValid() ? view.GetZDO() : null;
        }

        private ZDO? OwnedZdo()
        {
            ZDO? zdo = Zdo();
            return zdo != null && _bowl.m_nview.IsOwner() ? zdo : null;
        }

        private static long NowMs() => ZNet.instance != null ? (long)(ZNet.instance.GetTimeSeconds() * 1000.0) : 0L;

        private static double HourSeconds() =>
            EnvMan.instance != null && EnvMan.instance.m_dayLengthSec > 0 ? EnvMan.instance.m_dayLengthSec / 24.0 : FallbackHourSeconds;
    }
}
