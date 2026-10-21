using UnityEngine;

namespace Workshop.Crossbow
{
    /// <summary>
    /// A bolt leaving the muzzle at the shot along the crossbow's aim, flying straight at the settings' default speed
    /// (EliteCreaturesPack Crossbow/XbowSettings `Bolt Speed`), for the previews. Fed seconds into the fire clip as the
    /// rig reads them (<see cref="XbowFrames.FireTime"/>); shown for the first 0.4 s of its flight.
    /// </summary>
    public sealed class XbowFlight
    {
        private const float Speed = 40f;
        private readonly Transform muzzle;
        private readonly Transform bolt;
        private Vector3 from, along;
        private bool flying;

        public XbowFlight(Transform muzzle)
        {
            this.muzzle = muzzle;
            bolt = new GameObject("flying_bolt").transform;
            XbowBolt.In(bolt, "look", 1f);
            bolt.gameObject.SetActive(false);
        }

        /// <summary>The flying bolt: nock end at its origin, head along +Z; shown only while it flies.</summary>
        public Transform Bolt => bolt;

        public void Update(float time)
        {
            if (!flying && time >= XbowClips.Fire)
                (flying, from, along) = (true, muzzle.position, muzzle.parent.forward);
            float since = (time - XbowClips.Fire) / XbowClips.FireSpeed;
            bolt.gameObject.SetActive(flying && since >= 0f && since < 0.4f);
            if (!flying)
                return;
            bolt.position = from + along * (Speed * since);
            bolt.rotation = Quaternion.LookRotation(along);
        }
    }
}
