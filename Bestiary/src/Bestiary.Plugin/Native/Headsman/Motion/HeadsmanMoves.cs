// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// One of the Crypt Executioner's six attacks as its clip plays it: the clip (also the animator trigger its attack
    /// item sets), the clip's length, when it hits (the clip's OnAttackTrigger event), and the moments the code draws by
    /// the clip's own time on every peer - the axe let go (<see cref="Release"/>), a new one forming from
    /// <see cref="Ghost"/> to <see cref="Solid"/>, the sound cues - and on the owner the axe head
    /// cutting (<see cref="Cut"/>) and the wind-up played faster (<see cref="WindUp"/>). The numbers are the clips' own
    /// (AssetWorkshop unity/Assets/Editor/Headsman: HeadsmanMelee and HeadsmanRanged); change both together.
    /// </summary>
    public sealed class HeadsmanMove
    {
        public string Clip = "";
        public float Length, Hit = -1f, Release = -1f, Ghost = -1f, Solid = -1f;
        /// <summary>Clip seconds in which the axe head hits what it touches (<see cref="HeadsmanCut"/>); none by default.</summary>
        public Vector2 Cut = new Vector2(-1f, -1f);

        /// <summary>Clip seconds before which the clip plays at the wind-up speed (<see cref="HeadsmanSettings.SlamWindup"/>).</summary>
        public float WindUp = -1f;
        public (float time, string cue)[] Sounds = new (float, string)[0];

        public bool Throws => Release >= 0f;

        /// <summary>Whether the axe is in the fists at `time` (gone from the throw until the new one is solid).</summary>
        public bool AxeShown(float time) => !Throws || time < Release || time >= Solid;

        /// <summary>Whether the new axe is forming in the raised hands at `time`.</summary>
        public bool Forming(float time) => Throws && time >= Ghost && time < Solid;

        /// <summary>Whether the axe head hits at `time`.</summary>
        public bool Cutting(float time) => time >= Cut.x && time < Cut.y;
    }

    /// <summary>The six attacks, and the rear strike's turns done in code (a skeleton turns further than its muscles go).</summary>
    public static class HeadsmanMoves
    {
        /// <summary>Seconds the new axe takes to form; the skeleton raised where the thrown one broke forms as long.</summary>
        public const float Forming = 2f;

        /// <summary>Seconds the forming axe stays small and ghostly before it grows and its pieces take their colour.</summary>
        public const float Hold = 0.5f;

        public static readonly HeadsmanMove Slam = new HeadsmanMove
        {
            Clip = "ecp_headsman_slam", Length = 2.75f, Hit = 1.36f, Cut = new Vector2(1.32f, 1.42f), WindUp = 1.05f,
            Sounds = new[] { (0.15f, "vocal"), (1.12f, "whoosh_short"), (1.36f, "impact_ground") },
        };

        public static readonly HeadsmanMove Scrape = new HeadsmanMove
        {
            Clip = "ecp_headsman_scrape", Length = 1.85f, Hit = 0.48f, Cut = new Vector2(0.46f, 1.0f),
            Sounds = new[] { (0.05f, "vocal"), (0.3f, "whoosh_heavy"), (0.46f, "grind") },
        };

        public static readonly HeadsmanMove Spin = new HeadsmanMove
        {
            Clip = "ecp_headsman_spin", Length = 3.6f, Hit = 1.2f, Cut = new Vector2(0.85f, 1.55f),
            Sounds = new[] { (0.2f, "vocal"), (0.85f, "whoosh_spin") },
        };

        public static readonly HeadsmanMove Hurl = new HeadsmanMove
        {
            Clip = "ecp_headsman_hurl", Length = 5.1f, Hit = 1.15f, Release = 1.15f, Ghost = 2.35f, Solid = 2.35f + Forming,
            Sounds = new[] { (0.2f, "vocal"), (1.02f, "whoosh_heavy"), (1.15f, "throw"), (2.25f, "vocal_raise"), (2.35f, "regen"), (2.35f + Forming, "solid") },
        };

        public static readonly HeadsmanMove SpinThrow = new HeadsmanMove
        {
            Clip = "ecp_headsman_spinthrow", Length = 5.2f, Hit = 1.19f, Release = 1.19f, Ghost = 2.4f, Solid = 2.4f + Forming,
            Sounds = new[] { (0.15f, "vocal"), (0.75f, "whoosh_spin"), (1.19f, "throw"), (2.3f, "vocal_raise"), (2.4f, "regen"), (2.4f + Forming, "solid") },
        };

        public static readonly HeadsmanMove Rear = new HeadsmanMove
        {
            Clip = "ecp_headsman_rear", Length = 1.9f, Hit = 0.68f,
            Sounds = new[] { (0.05f, "creak"), (0.3f, "creak"), (0.52f, "whoosh_heavy"), (0.68f, "impact_hit") },
        };

        public static readonly HeadsmanMove[] All = { Slam, Scrape, Spin, Hurl, SpinThrow, Rear };

        private static readonly int[] Hashes = All.Select(m => Animator.StringToHash(m.Clip)).ToArray();

        /// <summary>The attack a state plays, by the state's short name hash (the states are named after their clips), or null.</summary>
        public static HeadsmanMove? ByState(int shortNameHash)
        {
            int i = System.Array.IndexOf(Hashes, shortNameHash);
            return i < 0 ? null : All[i];
        }

        /// <summary>The skull turned past the neck's reach, degrees to the right, in the rear strike.</summary>
        public static float HeadSpin(float time) => Keys(time, (0f, 0f), (0.05f, 0f), (0.3f, 180f), (0.5f, 0f));

        /// <summary>The upper body wrung round at the waist, degrees to the right, in the rear strike.</summary>
        public static float TorsoSpin(float time) => Keys(time, (0f, 0f), (0.3f, 0f), (0.5f, 180f), (0.95f, 180f), (1.35f, 0f));

        /// <summary>The creature's own turn in the rear strike, degrees to the right: the legs coming round under it.</summary>
        public static float RootYaw(float time) => Keys(time, (0f, 0f), (0.95f, 0f), (1.35f, 180f));

        /// <summary>Eased from key to key, flat at each (the workshop's curves are clamped the same way).</summary>
        private static float Keys(float time, params (float time, float value)[] keys)
        {
            if (time <= keys[0].time)
            {
                return keys[0].value;
            }
            for (int i = 1; i < keys.Length; i++)
            {
                if (time <= keys[i].time)
                {
                    float u = Mathf.SmoothStep(0f, 1f, (time - keys[i - 1].time) / (keys[i].time - keys[i - 1].time));
                    return Mathf.Lerp(keys[i - 1].value, keys[i].value, u);
                }
            }
            return keys[keys.Length - 1].value;
        }
    }
}
