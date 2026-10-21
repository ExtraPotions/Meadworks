using System;

namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>
/// Puts the fragments of one push back together, per sequence: fragments of an older sequence are dropped as
/// soon as a newer one starts, duplicates are ignored, the body is decompressed when the header says so.
/// </summary>
internal sealed class FragmentAssembler
{
	private int sequence = -1;
	private byte[]?[] parts = Array.Empty<byte[]?>();
	private int received;
	private bool compressed;

	/// <summary>Adds one fragment; returns the complete, decompressed body when the last one arrived.</summary>
	public byte[]? Add(int seq, int index, int count, bool packed, byte[] data, out int wireBytes)
	{
		wireBytes = 0;
		if (seq < sequence || count < 1 || index < 0 || index >= count)
		{
			return null;
		}
		if (seq > sequence)
		{
			Start(seq, count, packed);
		}
		if (parts.Length != count || parts[index] != null)
		{
			return null;
		}
		parts[index] = data;
		received++;
		if (received < count)
		{
			return null;
		}
		byte[] whole = Join();
		wireBytes = whole.Length;
		parts = Array.Empty<byte[]?>();
		return compressed ? Fragmenter.Decompress(whole) : whole;
	}

	public void Reset()
	{
		sequence = -1;
		parts = Array.Empty<byte[]?>();
		received = 0;
	}

	private void Start(int seq, int count, bool packed)
	{
		sequence = seq;
		parts = new byte[]?[count];
		received = 0;
		compressed = packed;
	}

	private byte[] Join()
	{
		int total = 0;
		foreach (byte[]? part in parts)
		{
			total += part?.Length ?? 0;
		}
		byte[] whole = new byte[total];
		int offset = 0;
		foreach (byte[]? part in parts)
		{
			Array.Copy(part!, 0, whole, offset, part!.Length);
			offset += part.Length;
		}
		return whole;
	}
}
