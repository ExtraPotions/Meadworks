// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Bundles;

/// <summary>
/// Loads an asset bundle a mod embeds as a resource. A bundle only loads on the platform it was built for, so a mod
/// embeds one per platform, named <c>&lt;name&gt;.windows</c> and <c>&lt;name&gt;.linux</c> (dedicated servers run the
/// Linux player); macOS takes <c>&lt;name&gt;.osx</c> when there is one and otherwise tries the Windows build.
/// Each bundle loads once per process and stays loaded.
/// <para>
/// It is read straight from the resource stream (the assembly's own memory), not copied into a byte array first: a
/// copy left the size of every bundle as garbage on the first world load. Unity keeps reading the stream for as long
/// as the bundle is loaded (its LZ4 chunks load on demand), so the stream is kept with it and never closed.
/// </para>
/// </summary>
public static class EmbeddedBundle
{
	private static readonly Dictionary<string, AssetBundle> loaded = new();
	private static readonly List<Stream> streams = new();

	public static AssetBundle Load(Assembly assembly, string name)
	{
		if (loaded.TryGetValue(name, out AssetBundle bundle))
		{
			return bundle;
		}
		string resource = FindResource(assembly, name);
		Stream stream = assembly.GetManifestResourceStream(resource);
		bundle = AssetBundle.LoadFromStream(stream);
		if (bundle == null)
		{
			stream.Dispose();
			throw new InvalidOperationException($"asset bundle {resource} did not load on {Application.platform}");
		}
		streams.Add(stream);
		loaded[name] = bundle;
		return bundle;
	}

	public static GameObject Prefab(AssetBundle bundle, string asset) =>
		bundle.LoadAsset<GameObject>(asset) ?? throw new InvalidOperationException($"{bundle.name} has no prefab {asset}");

	private static string FindResource(Assembly assembly, string name)
	{
		string[] resources = assembly.GetManifestResourceNames();
		foreach (string platform in Platforms())
		{
			string match = Array.Find(resources, r => r.EndsWith($"{name}.{platform}", StringComparison.Ordinal));
			if (match != null)
			{
				return match;
			}
		}
		throw new InvalidOperationException($"{assembly.GetName().Name} embeds no {name} bundle for {Application.platform}");
	}

	private static string[] Platforms() => Application.platform switch
	{
		RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxServer => new[] { "linux" },
		RuntimePlatform.OSXPlayer or RuntimePlatform.OSXServer => new[] { "osx", "windows" },
		_ => new[] { "windows" },
	};
}
