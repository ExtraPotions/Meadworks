// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// One game clip in a sound cue (<see cref="HeadsmanSoundTable"/>): where in the clip it starts, how long after the
    /// cue, its volume, its high- and low-pass (Hz, 0 for none), its pitch, how long it plays, and whether it fades in
    /// (a clip cut at the start would click).
    /// </summary>
    public sealed class HeadsmanSoundLayer
    {
        public readonly string Clip;
        public readonly float Start, Delay, Volume, Low, High, Pitch, Longest;
        public readonly bool FadeIn;

        public HeadsmanSoundLayer(string clip, float start, float delay, float volume, float low, float high, float pitch, float longest, bool fadeIn)
        {
            (Clip, Start, Delay, Volume, Low, High) = (clip, start, delay, volume, low, high);
            (Pitch, Longest, FadeIn) = (pitch, longest, fadeIn);
        }
    }
}
