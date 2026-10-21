// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>
    /// The row under the timeline: Play/Pause, the four speeds (the chosen one's label in the game's highlight orange),
    /// and the time into the clip over its length. Texts change only when what they show changes.
    /// </summary>
    internal sealed class ControlRow
    {
        private static readonly Color Chosen = new Color(1f, 0.72f, 0.25f, 1f);

        private readonly Button _play;
        private readonly TMP_Text? _playLabel;
        private readonly Button[] _speeds;
        private readonly TMP_Text?[] _speedLabels;
        private readonly Color _normal;
        private readonly TMP_Text _time;
        private int _shownTenths = -1;
        private int _shownState = -1;

        public ControlRow(Button play, Button[] speeds, TMP_Text time)
        {
            _play = play;
            _playLabel = play.GetComponentInChildren<TMP_Text>(true);
            _speeds = speeds;
            _speedLabels = new TMP_Text?[speeds.Length];
            for (int i = 0; i < speeds.Length; i++)
            {
                _speedLabels[i] = speeds[i].GetComponentInChildren<TMP_Text>(true);
            }
            _normal = _playLabel != null ? _playLabel.color : Color.white;
            _time = time;
        }

        public void Sync(Playback playback)
        {
            DeathRecap? recap = playback.Recap;
            int state = (recap != null ? 1 : 0) + (playback.Playing ? 2 : 0) + 4 * System.Array.IndexOf(Playback.Speeds, playback.Speed);
            if (state != _shownState)
            {
                _shownState = state;
                Buttons(playback);
            }
            int tenths = recap != null ? Mathf.FloorToInt(playback.Time * 10f) : -2;
            if (tenths != _shownTenths)
            {
                _shownTenths = tenths;
                _time.text = recap != null ? RecapText.Clip(playback.Time) + " / " + RecapText.Clip(recap.Duration) : "";
            }
        }

        private void Buttons(Playback playback)
        {
            bool loaded = playback.Recap != null;
            _play.interactable = loaded;
            if (_playLabel != null)
            {
                _playLabel.text = playback.Playing ? "Pause" : "Play";
            }
            for (int i = 0; i < _speeds.Length; i++)
            {
                _speeds[i].interactable = loaded;
                TMP_Text? label = _speedLabels[i];
                if (label != null)
                {
                    label.color = Mathf.Approximately(Playback.Speeds[i], playback.Speed) ? Chosen : _normal;
                }
            }
        }
    }
}
