using System.Collections.Generic;
using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// The new axe forming in the raised hands, for the preview (<see cref="HeadsmanForming"/> round the ghost axe,
    /// <see cref="HeadsmanGhost"/>): the light sits at the grip, the ring turns under the boss. No motes: the user did
    /// not want rocks swirling round it.
    /// </summary>
    public sealed class HeadsmanRegen
    {
        private readonly HeadsmanForming forming = new HeadsmanForming("regen", 0, 5, 1f);
        private readonly GameObject boss;

        public HeadsmanRegen(GameObject boss) => this.boss = boss;

        public IEnumerable<Renderer> Renderers => forming.Renderers;

        /// <summary>`axe` is the held axe's transform (it follows the fist even while hidden); `grip` the fist's middle.</summary>
        public void Update(HeadsmanMove move, float time, Transform axe, Vector3 grip)
        {
            if (move == null || move.Ghost < 0f)
            {
                forming.Hide();
                return;
            }
            Vector3 Along(float u) => axe.TransformPoint(new Vector3(-0.2f * u, 0.2f + 1.1f * u, 0f));
            forming.Update(time - move.Ghost, move.Solid - move.Ghost, Along, grip, boss.transform.position, time);
        }
    }
}
