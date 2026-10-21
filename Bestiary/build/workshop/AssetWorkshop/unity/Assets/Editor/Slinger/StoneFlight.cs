using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// The preview's flying stone: launched from the muzzle at the release towards a player-height target 15 m in
    /// front, on the arc the mod computes (<see cref="SlingStone.Elevation"/>), falling under the stone's gravity.
    /// </summary>
    public sealed class StoneFlight
    {
        private static readonly Vector3 Target = new Vector3(0f, 1.0f, 15f);
        private readonly Transform muzzle;
        private readonly GameObject stone;
        private Vector3 start, velocity;

        public StoneFlight(GameObject greydwarf)
        {
            muzzle = SlingerReference.Bone(greydwarf, "ecr_sling_muzzle");
            stone = SlingStone.Make("preview_flying_stone");
            stone.transform.localScale = Vector3.one * SlingStone.Scale;
            stone.SetActive(false);
        }

        public void Update(float time)
        {
            float flown = time - SlingClip.Release;
            if (flown < 0f)
                Aim();
            stone.SetActive(flown >= 0f && flown < 2.2f);
            stone.transform.position = start + velocity * flown + Vector3.down * (0.5f * SlingStone.Gravity * flown * flown);
            stone.transform.rotation = Quaternion.Euler(flown * 500f, flown * 200f, 0f);
        }

        private void Aim()
        {
            start = muzzle.position;
            Vector3 span = Target - start;
            var flat = new Vector3(span.x, 0f, span.z);
            float elevation = SlingStone.Elevation(SlingStone.Speed, SlingStone.Gravity, flat.magnitude, span.y);
            velocity = (flat.normalized * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation)) * SlingStone.Speed;
        }
    }
}
