using UnityEngine;

namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>A hidden, scene-independent behaviour whose Update drives this copy's charters once per frame.</summary>
internal sealed class Ticker : MonoBehaviour
{
	private static Ticker? instance;

	public static void Ensure()
	{
		if (instance != null)
		{
			return;
		}
		GameObject holder = new("Charter.Ticker");
		holder.hideFlags = HideFlags.HideAndDontSave;
		Object.DontDestroyOnLoad(holder);
		instance = holder.AddComponent<Ticker>();
	}

	private void Update() => GameHooks.Tick(Time.realtimeSinceStartup);
}
