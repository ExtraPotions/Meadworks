using System.Collections.Generic;
using UnityEngine;

namespace Burrow.Reach
{
    /// <summary>
    /// Remembers which requirement names were just paid from containers so the panel rows can flash once. The
    /// rows are rebuilt every frame by the game, so the flash is a weight the row patch reads, not a coroutine.
    /// </summary>
    public static class ReachFlash
    {
        private const float Duration = 0.6f;
        private static readonly Dictionary<string, float> pulled = new Dictionary<string, float>();

        public static void Mark(string sharedName)
        {
            if (!string.IsNullOrEmpty(sharedName))
                pulled[sharedName] = Time.time;
        }

        /// <summary>0 when the name was not pulled recently or the flash is off, else a single 0..1..0 pulse.</summary>
        public static float Weight(string sharedName)
        {
            if (!ReachSettings.FlashOnPull.Value || sharedName == null || !pulled.TryGetValue(sharedName, out float at))
                return 0f;
            float t = (Time.time - at) / Duration;
            if (t >= 1f)
            {
                pulled.Remove(sharedName);
                return 0f;
            }
            return Mathf.Sin(t * Mathf.PI);
        }
    }
}
