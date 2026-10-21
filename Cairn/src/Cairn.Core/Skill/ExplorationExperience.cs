// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: where Exploration experience comes from (design D2, D5). The explore circle follows the game's
// Minimap.Explore behavior as read from assembly_valheim (no code copied).
using System;

namespace ExtraPotions.Cairn.Skill
{
    /// <summary>
    /// The game's <c>GetStableHashCode</c> and the custom skill number CS1 derives from it (hash masked positive), so the
    /// pinned <see cref="ExplorationSkill.GameType"/> can be checked without the game.
    /// </summary>
    public static class SkillHash
    {
        public static int Of(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            unchecked
            {
                int a = 5381, b = a;
                for (var i = 0; i < text.Length && text[i] != '\0'; i += 2)
                {
                    a = ((a << 5) + a) ^ text[i];
                    if (i == text.Length - 1 || text[i + 1] == '\0') break;
                    b = ((b << 5) + b) ^ text[i + 1];
                }
                return a + b * 1566083941;
            }
        }

        public static int CustomType(string identity) => Of(identity) & 0x7FFFFFFF;
    }

    /// <summary>Who is uncovering the map right now. Only <see cref="Natural"/> and <see cref="Vantage"/> earn experience.</summary>
    public enum ExploreSource
    {
        /// <summary>Anything else: a cheat or another mod's reveal, the map table, shared map data.</summary>
        Other,

        /// <summary>The game's own explore tick around the local player (<c>Minimap.UpdateExplore</c>).</summary>
        Natural,

        /// <summary>Cairn's own Vantage uncover.</summary>
        Vantage,
    }

    /// <summary>The explore source in effect; entered around the game's explore tick and Cairn's Vantage. Main thread only.</summary>
    public sealed class ExploreScope
    {
        public ExploreSource Current { get; private set; } = ExploreSource.Other;

        /// <summary>Enters <paramref name="source"/>; returns the previous source for <see cref="Exit"/>.</summary>
        public ExploreSource Enter(ExploreSource source)
        {
            var previous = Current;
            Current = source;
            return previous;
        }

        public void Exit(ExploreSource previous) => Current = previous;
    }

    /// <summary>Counts the texels one <c>Minimap.Explore(position, radius)</c> call newly uncovers.</summary>
    public static class ExploreCells
    {
        /// <summary>
        /// Texels within <c>ceil(radius / pixelSize)</c> texels of (<paramref name="px"/>, <paramref name="py"/>), inside the
        /// texture, that <paramref name="explored"/> (by index <c>y * textureSize + x</c>) reports as not yet explored.
        /// </summary>
        public static int CountNew(int px, int py, float radius, float pixelSize, int textureSize, Func<int, bool> explored)
        {
            if (explored == null) throw new ArgumentNullException(nameof(explored));
            if (textureSize <= 0 || !(pixelSize > 0f) || float.IsInfinity(pixelSize) || float.IsNaN(radius) || float.IsInfinity(radius)) return 0;
            var reach = (int)Math.Ceiling(Math.Max(0f, radius) / pixelSize);
            var reachSquared = (long)reach * reach;
            var count = 0;
            for (var y = Math.Max(0, py - reach); y <= Math.Min(textureSize - 1, py + reach); y++)
            {
                for (var x = Math.Max(0, px - reach); x <= Math.Min(textureSize - 1, px + reach); x++)
                {
                    long dx = x - px, dy = y - py;
                    if (dx * dx + dy * dy > reachSquared) continue;
                    if (!explored(y * textureSize + x)) count++;
                }
            }
            return count;
        }
    }

    /// <summary>
    /// Natural discovery requests. The game's Vegvisir and runestone ask the server for the closest location
    /// (<c>Game.DiscoverClosestLocation</c>) and add the pin when the response returns, so the natural scope cannot span the
    /// call: the request is recorded while the player's interaction runs, and a response with the same pin name and type
    /// within <see cref="WindowSeconds"/> counts as natural (a discover-all Vegvisir answers several times). Main thread only.
    /// </summary>
    public sealed class DiscoveryRequests
    {
        /// <summary>How long a request waits for its server response, in seconds.</summary>
        public const float WindowSeconds = 30f;

        private readonly System.Collections.Generic.List<(string Name, int Type, float At)> pending =
            new System.Collections.Generic.List<(string Name, int Type, float At)>();

        public void Record(string? pinName, int pinType, float now)
        {
            if (pinName == null) return;
            Prune(now);
            pending.Add((pinName, pinType, now));
        }

        /// <summary>True when a response for <paramref name="pinName"/> and <paramref name="pinType"/> answers a recorded request.</summary>
        public bool Natural(string? pinName, int pinType, float now)
        {
            if (pinName == null) return false;
            Prune(now);
            foreach (var request in pending)
                if (request.Type == pinType && string.Equals(request.Name, pinName, StringComparison.Ordinal)) return true;
            return false;
        }

        public void Clear() => pending.Clear();

        private void Prune(float now) => pending.RemoveAll(r => float.IsNaN(now) || now - r.At > WindowSeconds || now < r.At - WindowSeconds);
    }

    /// <summary>Experience for each source (design D2): area per square kilometer, a new map pin, an unknown biome.</summary>
    public static class ExplorationExperience
    {
        public static float Area(ExploreSource source, int newTexels, float texelMetres, SkillValues values) =>
            source == ExploreSource.Other ? 0f : ExplorationSkill.AreaExperience(newTexels, texelMetres, values);

        /// <summary>
        /// <paramref name="pinAdded"/>: <c>Minimap.DiscoverLocation</c> added a pin (it returns false for a known one);
        /// <paramref name="natural"/>: the pin answers a Vegvisir or runestone the player used (user decision 2026-10-08),
        /// so pins from a cheat, the console or another mod earn nothing.
        /// </summary>
        public static float Discovery(bool pinAdded, bool natural, SkillValues values) =>
            pinAdded && natural ? ExplorationSkill.DiscoveryExperience(values) : 0f;

        /// <summary><paramref name="knownBefore"/>: the player already knew the biome sector before <c>Player.AddKnownBiome</c>.</summary>
        public static float Biome(bool knownBefore, SkillValues values) => knownBefore ? 0f : ExplorationSkill.BiomeExperience(values);
    }
}
