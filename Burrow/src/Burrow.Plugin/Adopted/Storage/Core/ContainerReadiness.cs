namespace Burrow.Core
{
    /// <summary>ReachReady's ask-and-use-later boundary, exposed to the existing resource queue without another scheduler.</summary>
    public static class ContainerReadiness
    {
        /// <summary>Asks eligible remote sources within the caller's existing range. No remote inventory is read or paid.</summary>
        public static bool AskNearby(float range, ContainerUse use, float askEvery = HandOver.ActionSeconds)
        {
            if (float.IsNaN(range) || float.IsInfinity(range) || range < 0 || !Player.m_localPlayer) return false;
            bool waiting = false;
            foreach (Container chest in ContainerScan.Nearby(Player.m_localPlayer.transform.position, range, use))
            {
                if (ContainerScan.CanClaimNow(chest) || !ContainerScan.IsRemote(chest) || ContainerScan.IsShip(chest) || ContainerScan.IsCart(chest)) continue;
                HandOver.Ask(chest, askEvery);
                waiting = true; // A throttled request is still awaiting the existing owner's reply/data.
            }
            return waiting;
        }
    }
}
