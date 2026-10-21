// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.AdoptedLibs.Hotkeys;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>
    /// The open recap window: the deaths on the left, the selected one's killer, video, timeline, controls and hits on
    /// the right. Each frame it advances the playback by real time and brings every part up to it; the parts change
    /// their texts and pictures only when what they show changes. Space plays or pauses, the arrow keys step a frame.
    /// A death recorded while the window is open joins the list at once.
    /// </summary>
    internal sealed class WindowView : MonoBehaviour
    {
        public readonly Playback Playback = new Playback();

        private Pane _pane = null!;
        private DeathList _deaths = null!;
        private HitList _hits = null!;
        private DeathRecap? _selected;
        private bool _ready;

        public void Init(WindowParts parts, Slider sliderTemplate)
        {
            _pane = PaneBuilder.Build(parts, sliderTemplate, this);
            ElementList deaths = new ElementList(parts.ListRoot, parts.Element, parts.ListScroll, DeathList.RowHeight, DeathList.RowSpacing);
            _deaths = new DeathList(deaths) { Clicked = Select };
            ElementList hits = new ElementList(parts.AreaContent, parts.Element, parts.AreaScroll, HitList.Height, HitList.Spacing);
            _hits = new HitList(hits) { Seek = Playback.Seek };
            _pane.Timeline.Seek = Playback.Seek;
            _ready = true;
        }

        /// <summary>Shown with the newest death selected and playing from its start.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            _deaths.Fill(RecapStore.Deaths);
            Select(RecapStore.Deaths.Count > 0 ? 0 : -1);
        }

        public void Hide()
        {
            Playback.Pause();
            gameObject.SetActive(false);
        }

        public void TogglePlay() => Playback.Toggle();

        public void SetSpeed(float speed) => Playback.SetSpeed(speed);

        private void OnEnable() => RecapStore.Changed += Refresh;

        private void OnDisable() => RecapStore.Changed -= Refresh;

        private void OnDestroy() => _pane?.Video.Destroy();

        // A fault here would repeat every frame while open, so it is reported once and the window closes.
        private void Update()
        {
            try
            {
                Frame();
            }
            catch (System.Exception e)
            {
                ExtraPotions.Wildblood.AdoptedLibs.PatchGuard.Guard.Report(e, "death recap window");
                Hide();
            }
        }

        private void Frame()
        {
            if (!_ready)
            {
                return;
            }
            Keys();
            Playback.Advance(Time.unscaledDeltaTime);
            DeathRecap? recap = Playback.Recap;
            float time = Playback.Time;
            _pane.Video.Sync(recap, time);
            _pane.Timeline.Sync(recap, time);
            _hits.Sync(recap, time, Playback.Playing);
            _pane.Controls.Sync(Playback);
        }

        private void Keys()
        {
            if (Typing.Active)
            {
                return;
            }
            if (ZInput.GetKeyDown(KeyCode.Space))
            {
                Playback.Toggle();
            }
            else if (ZInput.GetKeyDown(KeyCode.LeftArrow))
            {
                Playback.Step(-1);
            }
            else if (ZInput.GetKeyDown(KeyCode.RightArrow))
            {
                Playback.Step(1);
            }
        }

        private void Select(int index)
        {
            _selected = index >= 0 && index < RecapStore.Deaths.Count ? RecapStore.Deaths[index] : null;
            Playback.Load(_selected);
            _deaths.Mark(_selected != null ? index : -1);
            Header(_selected);
        }

        // A new death while open: the list is rebuilt and the death being watched stays selected (the newest otherwise).
        private void Refresh()
        {
            _deaths.Fill(RecapStore.Deaths);
            int index = _selected != null ? RecapStore.Deaths.IndexOf(_selected) : -1;
            if (index >= 0)
            {
                _deaths.Mark(index);
            }
            else
            {
                Select(RecapStore.Deaths.Count > 0 ? 0 : -1);
            }
        }

        private void Header(DeathRecap? recap)
        {
            if (recap == null)
            {
                _pane.Title.text = "No deaths yet";
                _pane.Summary.text = "Deaths are kept here until the game closes. " + RecapNotice.OpenWith() + " opens and closes this window.";
                return;
            }
            _pane.Title.text = RecapText.Killer(recap);
            _pane.Summary.text = RecapText.Summary(recap);
        }
    }
}
