using System.Collections.Generic;
using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>How a thrown axe turns in the air: end over end (the overhead hurl) or flat like a disc (the spin throw).</summary>
    public enum Flight { None, Tumble, Disc }

    /// <summary>
    /// One headsman attack: its clip's keys and length, and the moments that are not in the clip but that the mod (and
    /// the previews) must do at the clip's times: the head turned past the neck's muscles (<see cref="HeadSpin"/>), the
    /// creature's own turn (<see cref="HeadsmanKeys.RootYaw"/>), the axe let go at <see cref="Release"/> and gone
    /// until the new one forms (<see cref="Ghost"/>, a ghost of it appears in the raised hands; <see cref="Solid"/>, it is
    /// real), the edge meeting the ground or the target (<see cref="Impact"/>), and the ground scraped (<see cref="Scrape"/>).
    /// Times are the clip's own seconds.
    /// </summary>
    public sealed class HeadsmanMove
    {
        public const string Prefix = "ecp_headsman_";

        public string Name;
        public string Title;
        public float Length;
        public HeadsmanKeys Keys;
        public AnimationCurve HeadSpin;

        /// <summary>The upper body turned at the waist past the back's muscles (degrees, to the right), done in code.</summary>
        public AnimationCurve TorsoSpin;
        public float Release = -1f, Ghost = -1f, Solid = -1f, Impact = -1f;
        public Vector2 Scrape = new Vector2(-1f, -1f);
        public Flight Flight;

        /// <summary>Named moments for the timeline, besides the ones above.</summary>
        public readonly List<(float time, string name)> Moments = new List<(float, string)>();

        /// <summary>
        /// Sounds at clip times, by cue (vocal, whoosh_heavy, whoosh_short, whoosh_spin, impact_ground, impact_hit, grind,
        /// rumble, throw, vocal_raise, regen, solid, creak; no footsteps or falling rubble: the user found them not
        /// needed): each cue is a recipe
        /// of the game's own recordings (assets/ecp_headsman/sfx.py), which the mod plays the same way at runtime.
        /// </summary>
        public readonly List<(float time, string cue)> Sounds = new List<(float, string)>();

        public string Clip => Prefix + Name;

        /// <summary>Whether the axe in the fists shows: not from the throw until the new one is solid.</summary>
        public bool AxeShown(float time) => Release < 0f || time < Release || time >= Solid;

        /// <summary>Whether the ghost of the forming axe shows.</summary>
        public bool GhostShown(float time) => Ghost >= 0f && time >= Ghost && time < Solid;

        public float Spin(float time) => HeadSpin == null ? 0f : HeadSpin.Evaluate(time);

        public float Torso(float time) => TorsoSpin == null ? 0f : TorsoSpin.Evaluate(time);

        /// <summary>Every named moment in time order, the fixed ones included.</summary>
        public IEnumerable<(float time, string name)> Timeline()
        {
            if (Impact >= 0f)
                yield return (Impact, "impact");
            if (Scrape.x >= 0f)
                yield return (Scrape.x, "scrape");
            if (Release >= 0f)
                yield return (Release, "release");
            if (Ghost >= 0f)
                yield return (Ghost, "axe forming");
            if (Solid >= 0f)
                yield return (Solid, "axe solid");
            foreach (var moment in Moments)
                yield return moment;
        }
    }
}
