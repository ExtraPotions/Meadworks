using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace ExtraPotions.Mantle.Embedded.Charter;

/// <summary>
/// Cuts a push body into fragment packages and compresses it first when it is worth it. Every fragment carries
/// the push header: protocol, guid, sequence, fragment index and count, the compression flag, then its bytes.
/// </summary>
internal static class Fragmenter
{
	public const int Protocol = 1;
	public const int FragmentBytes = 64 * 1024;
	public const int CompressAbove = 1024;
	public const int LargestBody = 8 * 1024 * 1024;

	public static List<ZPackage> Split(string guid, int sequence, byte[] body, out int wireBytes, out bool compressed)
	{
		compressed = body.Length > CompressAbove;
		byte[] payload = compressed ? Compress(body) : body;
		wireBytes = payload.Length;
		int count = Math.Max(1, (payload.Length + FragmentBytes - 1) / FragmentBytes);
		List<ZPackage> fragments = new(count);
		for (int index = 0; index < count; index++)
		{
			int offset = index * FragmentBytes;
			byte[] slice = new byte[Math.Min(FragmentBytes, payload.Length - offset)];
			Array.Copy(payload, offset, slice, 0, slice.Length);
			fragments.Add(Fragment(guid, sequence, index, count, compressed, slice));
		}
		return fragments;
	}

	private static ZPackage Fragment(string guid, int sequence, int index, int count, bool compressed, byte[] slice)
	{
		ZPackage pkg = new();
		pkg.Write(Protocol);
		pkg.Write(guid);
		pkg.Write(sequence);
		pkg.Write(index);
		pkg.Write(count);
		pkg.Write(compressed);
		pkg.Write(slice);
		return pkg;
	}

	public static byte[] Compress(byte[] data)
	{
		using MemoryStream output = new();
		using (GZipStream zip = new(output, CompressionMode.Compress, leaveOpen: true))
		{
			zip.Write(data, 0, data.Length);
		}
		return output.ToArray();
	}

	public static byte[] Decompress(byte[] data)
	{
		using MemoryStream input = new(data);
		using GZipStream zip = new(input, CompressionMode.Decompress);
		using MemoryStream output = new();
		zip.CopyTo(output);
		return output.ToArray();
	}
}
