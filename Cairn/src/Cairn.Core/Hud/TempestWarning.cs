// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Tempest's weather event on Cairn's HUD (design C8, 8.2, CA09) through the Crucible contract
// meadworks.weather.events 1.0 (Crucible main cda9696) and meadworks.weather.events.detail 1.0. Cairn.Core has no Crucible
// reference, so the contracts' kind and phase numbers and the detail reply rules are restated here; the plugin reads 1.0
// through WeatherEventsContract.TryRead and the detail through TempestDetail.TryRead (no newer Crucible.Contracts needed).
using System;
using System.Globalization;
using ExtraPotions.Cairn.Localization;

namespace ExtraPotions.Cairn.Hud
{
    /// <summary>Event kind, numbered as <c>WeatherEventKind</c> of the 1.0 contract (0 to 2) and the open kind number of the
    /// detail contract (3 and up).</summary>
    public enum TempestKind { None = 0, Blizzard = 1, Thunderstorm = 2, LightningStorm = 3, DenseFog = 4, AshStorm = 5, MeteorShower = 6 }

    /// <summary>The kit glyph the HUD row shows for an event (the plugin maps it to Crucible's KitGlyph).</summary>
    public enum TempestGlyph { None, Snowflake, Bolt, Cloud, Star }

    /// <summary>Event phase, numbered as <c>WeatherEventPhase</c> of the contract.</summary>
    public enum TempestPhase { Idle = 0, Warning = 1, Active = 2, Clearing = 3 }

    public enum TempestLineKind { None, Warning, Active, Clearing }

    /// <summary>The HUD's Tempest row: which line, its catalog key, the event's name key and the countdown text.</summary>
    public readonly struct TempestLine
    {
        public TempestLine(TempestLineKind kind, string textKey, string eventKey, string countdown, TempestGlyph glyph = TempestGlyph.None)
        {
            Kind = kind;
            TextKey = textKey;
            EventKey = eventKey;
            Countdown = countdown;
            Glyph = glyph;
        }

        public static TempestLine None => new TempestLine(TempestLineKind.None, "", "", "");

        public TempestLineKind Kind { get; }

        /// <summary>Warning: <see cref="CairnTextKeys.TempestWarning"/> ({0} event, {1} countdown); Active: the event's
        /// own name key; Clearing: <see cref="CairnTextKeys.TempestClearing"/> ({0} event).</summary>
        public string TextKey { get; }

        /// <summary>The event's name key, worded as Tempest names it: tempest_blizzard, tempest_thunderstorm.</summary>
        public string EventKey { get; }

        /// <summary>m:ss left in the warning; empty for the other lines.</summary>
        public string Countdown { get; }

        /// <summary>The event's glyph, as on Tempest's own badge.</summary>
        public TempestGlyph Glyph { get; }
    }

    public static class TempestWarning
    {
        /// <summary>The event reaches the player: the biome under them is one of the event's biomes (game
        /// <c>Heightmap.Biome</c> bits), or the provider says the local biome is affected now.</summary>
        public static bool Reaches(int biomes, int localBiome, bool localAffected) => localAffected || (biomes & localBiome) != 0;

        /// <summary>
        /// The row for one reading; nothing while Tempest Warnings is off, idle, without a kind Cairn can name or elsewhere.
        /// The name is <c>tempest_&lt;token&gt;</c> when the detail contract gave a token Cairn knows, else the kind's own.
        /// </summary>
        public static TempestLine Line(bool enabled, TempestKind kind, TempestPhase phase, float secondsLeft, int biomes, int localBiome, bool localAffected,
            string? token = null)
        {
            if (!enabled || !Reaches(biomes, localBiome, localAffected)) return TempestLine.None;
            var known = Token(kind);
            string eventKey;
            if (token != null && Array.IndexOf(Tokens, token) >= 0) eventKey = CairnTextKeys.TempestToken(token);
            else if (known != null) eventKey = CairnTextKeys.TempestEvent(kind);
            else return TempestLine.None;
            var glyph = GlyphOf(known != null ? kind : KindOf(token!));
            switch (phase)
            {
                case TempestPhase.Warning: return new TempestLine(TempestLineKind.Warning, CairnTextKeys.TempestWarning, eventKey, Countdown(secondsLeft), glyph);
                case TempestPhase.Active: return new TempestLine(TempestLineKind.Active, eventKey, eventKey, "", glyph);
                case TempestPhase.Clearing: return new TempestLine(TempestLineKind.Clearing, CairnTextKeys.TempestClearing, eventKey, "", glyph);
                default: return TempestLine.None;
            }
        }

        /// <summary>The name tokens Cairn's catalogs carry (as Tempest names its kinds), in kind order from 1.</summary>
        public static readonly string[] Tokens = { "blizzard", "thunderstorm", "lightning_storm", "dense_fog", "ash_storm", "meteor_shower" };

        /// <summary>A known kind's token, or null.</summary>
        public static string? Token(TempestKind kind) => kind > TempestKind.None && (int)kind <= Tokens.Length ? Tokens[(int)kind - 1] : null;

        static TempestKind KindOf(string token) => (TempestKind)(Array.IndexOf(Tokens, token) + 1);

        /// <summary>
        /// The Tempest event the status row's weather name and mark show instead of the weather category, so the row reads as
        /// the Tempest row and badge do (Lightning Storm, not Thunderstorm). The environment's own event (a Tempest event
        /// environment) comes first, then the active row where the event reaches the player. Indoors and boss arenas keep
        /// their category. Null: show the category.
        /// </summary>
        public static (string Key, TempestGlyph Glyph)? StatusEvent(WeatherCategory category, string? environment, TempestLine line)
        {
            if (category == WeatherCategory.Indoors || category == WeatherCategory.Boss) return null;
            var token = WeatherCategories.EventToken(environment);
            if (token != null) return (CairnTextKeys.TempestToken(token), GlyphOf(KindOf(token)));
            if (line.Kind == TempestLineKind.Active) return (line.EventKey, line.Glyph);
            return null;
        }

        /// <summary>Tempest's badge glyph per kind: snowflake, bolt for both storms, cloud for fog, star for the ash storm and meteor shower.</summary>
        public static TempestGlyph GlyphOf(TempestKind kind)
        {
            switch (kind)
            {
                case TempestKind.Blizzard: return TempestGlyph.Snowflake;
                case TempestKind.Thunderstorm:
                case TempestKind.LightningStorm: return TempestGlyph.Bolt;
                case TempestKind.DenseFog: return TempestGlyph.Cloud;
                case TempestKind.AshStorm:
                case TempestKind.MeteorShower: return TempestGlyph.Star;
                default: return TempestGlyph.None;
            }
        }

        /// <summary>Minutes and seconds, whole seconds rounded up; 0:00 for non-finite or negative input.</summary>
        public static string Countdown(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return "0:00";
            int whole = (int)Math.Ceiling(Math.Min(seconds, 359999f));
            return (whole / 60).ToString(CultureInfo.InvariantCulture) + ":" + (whole % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>One reading of either contract: the open kind number, phase, countdown, biomes, local flag, revision and token.</summary>
    public readonly struct TempestReading
    {
        public TempestReading(int kind, TempestPhase phase, float secondsLeft, int biomes, bool localAffected, int revision, string token)
        {
            Kind = kind;
            Phase = phase;
            SecondsLeft = secondsLeft;
            Biomes = biomes;
            LocalAffected = localAffected;
            Revision = revision;
            Token = token ?? "";
        }

        public int Kind { get; }
        public TempestPhase Phase { get; }
        public float SecondsLeft { get; }
        public int Biomes { get; }
        public bool LocalAffected { get; }
        public int Revision { get; }

        /// <summary>The detail contract's name token; empty from the 1.0 contract or while idle.</summary>
        public string Token { get; }

        public bool Running => Phase != TempestPhase.Idle;
    }

    /// <summary>
    /// Cairn's reader of <c>meadworks.weather.events.detail</c> 1.0 (Crucible <c>WeatherEventsDetailContract</c>), restated so
    /// Cairn runs with any Crucible: shape <c>Func&lt;Tuple&lt;int,int,float,int,bool,int,string&gt;&gt;</c>, the 1.0 items with an
    /// open kind number, then a token of 1 to 32 of a-z, 0-9 and underscore (empty while idle). Resolved before 1.0; without
    /// it Cairn falls back to 1.0.
    /// </summary>
    public static class TempestDetail
    {
        public const string Id = "meadworks.weather.events.detail";
        public static readonly Version ContractVersion = new Version(1, 0);
        public const float MaximumSecondsLeft = 3600f;

        public static bool TryRead(Tuple<int, int, float, int, bool, int, string>? raw, out TempestReading reading)
        {
            reading = default;
            if (raw == null || !Valid(raw.Item1, raw.Item2, raw.Item3, raw.Item4, raw.Item5, raw.Item7)) return false;
            reading = new TempestReading(raw.Item1, (TempestPhase)raw.Item2, raw.Item3, raw.Item4, raw.Item5, raw.Item6, raw.Item7);
            return true;
        }

        public static bool TryRead(Func<Tuple<int, int, float, int, bool, int, string>>? provider, out TempestReading reading)
        {
            reading = default;
            if (provider == null) return false;
            Tuple<int, int, float, int, bool, int, string> raw;
            try { raw = provider(); } catch { return false; }
            return TryRead(raw, out reading);
        }

        static bool Valid(int kind, int phase, float seconds, int biomes, bool local, string? token)
        {
            if (kind < 0 || phase < 0 || phase > 3 || biomes < 0) return false;
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f || seconds > MaximumSecondsLeft) return false;
            if (phase == 0) return kind == 0 && seconds == 0f && biomes == 0 && !local && string.IsNullOrEmpty(token);
            if (kind == 0 || string.IsNullOrEmpty(token) || token!.Length > 32) return false;
            foreach (var c in token) if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_') return false;
            return true;
        }
    }

    /// <summary>The one center notice per warning: true the first time a revision is seen in the Warning phase where the
    /// event reaches the player (the contract bumps the revision on every kind, phase or biome change). The notice is held
    /// until the HUD row draws (CA06b sets <see cref="HudRowDrawn"/>; user decision, Group 2 follow-up of 2026-10-08): the
    /// plugin shows it only while <see cref="CentreEnabled"/> is true.</summary>
    public sealed class TempestNotice
    {
        private int? announced;

        /// <summary>Whether the kit HUD draws the Tempest row this frame. The plugin's HUD (CA06b) sets it; false until then and
        /// again after the HUD is disposed.</summary>
        public static bool HudRowDrawn { get; set; }

        /// <summary>Whether the center notice is shown at all (only alongside the HUD row).</summary>
        public static bool CentreEnabled => ShowCentre(HudRowDrawn);

        /// <summary>The center notice is shown only when the HUD row is drawn too.</summary>
        public static bool ShowCentre(bool hudRowDrawn) => hudRowDrawn;

        public bool Update(int revision, TempestPhase phase, bool relevant)
        {
            if (phase != TempestPhase.Warning || !relevant || announced == revision) return false;
            announced = revision;
            return true;
        }

        public void Reset() => announced = null;
    }
}
