namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>
/// Who is a steward: on the server, a peer whose host name is on the admin list. Watches the list once a second
/// and tells a peer whose status changed with a flags-only push.
/// </summary>
internal sealed class Stewardship
{
	private const float Interval = 1f;

	private readonly Courier courier;
	private readonly Publisher publisher;
	private float next;

	public Stewardship(Courier courier, Publisher publisher)
	{
		this.courier = courier;
		this.publisher = publisher;
	}

	public static bool IsSteward(ZNetPeer peer)
	{
		if (ZNet.instance == null)
		{
			return false;
		}
		return ZNet.instance.IsAdmin(Side.HostOf(peer));
	}

	public void Tick(float now)
	{
		if (!Side.IsServer || now < next)
		{
			return;
		}
		next = now + Interval;
		foreach (ZNetPeer peer in Side.ReadyPeers())
		{
			bool? last = courier.LastSteward(peer);
			if (last != null && last != IsSteward(peer))
			{
				publisher.SendFlags(peer);
			}
		}
	}
}
