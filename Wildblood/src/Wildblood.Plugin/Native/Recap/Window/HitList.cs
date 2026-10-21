// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>
    /// Every hit of the selected death under the video, one row each: when, who, how much, of what, and the health left.
    /// The latest hit by the playback time is highlighted and kept in view while playing; clicking a hit plays from a
    /// second before it.
    /// </summary>
    internal sealed class HitList
    {
        public const float Height = 26f;
        public const float Spacing = 27f;
        private const float LeadIn = 1f;

        private readonly ElementList _list;
        private DeathRecap? _recap;

        public HitList(ElementList list)
        {
            _list = list;
        }

        /// <summary>Asked to play from this clip time.</summary>
        public System.Action<float>? Seek;

        public void Sync(DeathRecap? recap, float time, bool playing)
        {
            if (recap != _recap)
            {
                Fill(recap);
            }
            _list.Mark(recap != null ? recap.HitAt(time) : -1, playing);
        }

        private void Fill(DeathRecap? recap)
        {
            _recap = recap;
            _list.Clear();
            if (recap == null)
            {
                return;
            }
            for (int i = 0; i < recap.Hits.Count; i++)
            {
                _list.Add(RecapText.Hit(recap.Hits[i], recap.HitTime(i)), 15f);
            }
            _list.Clicked = index => Seek?.Invoke(recap.HitTime(index) - LeadIn);
        }
    }
}
