using System;
using System.Globalization;

namespace ExtraPotions.Meadworks.Contracts {
    /// <summary>Season of <see cref="SeasonsContract"/> 1.0, numbered as Solstice's calendar publishes it.</summary>
    public enum SeasonKind { Spring=0, Summer=1, Fall=2, Winter=3 }

    /// <summary>
    /// Shared read capability <c>meadworks.seasons.view</c> version 1.0: Solstice provides the current season, products that
    /// keep their own seasonal rules (Ember night length and torches, Shepherd procreation, Wildblood drops, Cairn) read it and
    /// apply their own modifier, falling back to their plain behavior when it is absent. The shape is
    /// <c>Func&lt;Tuple&lt;int,int,int,int,int,int&gt;&gt;</c> with items in this order: season (<see cref="SeasonKind"/>),
    /// day in the season (1 to the season length), season length in days, day of the year, year length in days, and a revision
    /// that the provider bumps whenever any of the other five change (any value is valid; compare for inequality only).
    /// All zero means the provider is loaded but off for this session: it reads as valid and <see cref="SeasonState.Available"/>
    /// is false.
    /// Providers register <c>AddCapability&lt;Func&lt;Tuple&lt;int,int,int,int,int,int&gt;&gt;&gt;(Id, ContractVersion, ...)</c>;
    /// consumers resolve the same type with <see cref="ContractVersion"/> as the minimum and read through
    /// <see cref="ReadOrUnavailable(Func{Tuple{int,int,int,int,int,int}})"/> or
    /// <see cref="TryRead(Func{Tuple{int,int,int,int,int,int}}, out SeasonState)"/>.
    /// Both sides call the delegate on the Unity main thread only. Additive fields need a new major version.
    /// </summary>
    public static class SeasonsContract {
        public const string Id="meadworks.seasons.view";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public static Type DelegateType=>typeof(Func<Tuple<int,int,int,int,int,int>>);

        public static Tuple<int,int,int,int,int,int> Pack(SeasonState state)=>
            Tuple.Create((int)state.Season,state.DayInSeason,state.DaysInSeason,state.DayOfYear,state.YearLength,state.Revision);

        /// <summary>Reads one provider reply; a null or out-of-range reply gives false and <see cref="SeasonState.Unavailable"/>.</summary>
        public static bool TryRead(Tuple<int,int,int,int,int,int>? raw,out SeasonState state){
            state=SeasonState.Unavailable;
            if(raw==null||!SeasonState.Valid(raw.Item1,raw.Item2,raw.Item3,raw.Item4,raw.Item5))return false;
            state=new SeasonState((SeasonKind)raw.Item1,raw.Item2,raw.Item3,raw.Item4,raw.Item5,raw.Item6);
            return true;
        }

        /// <summary>Calls the provider and reads its reply; an absent or throwing provider gives false and the unavailable state.</summary>
        public static bool TryRead(Func<Tuple<int,int,int,int,int,int>>? provider,out SeasonState state){
            state=SeasonState.Unavailable;
            if(provider==null)return false;
            Tuple<int,int,int,int,int,int> raw;
            try{raw=provider();}catch{return false;}
            return TryRead(raw,out state);
        }

        /// <summary>
        /// The fallback path in one call: the provider's reading, or <see cref="SeasonState.Unavailable"/> when there is no provider,
        /// it throws, its reply is invalid or it reports itself off. Consumers branch on <see cref="SeasonState.Available"/>.
        /// </summary>
        public static SeasonState ReadOrUnavailable(Func<Tuple<int,int,int,int,int,int>>? provider)=>TryRead(provider,out var state)?state:SeasonState.Unavailable;
    }

    /// <summary>One reading of <see cref="SeasonsContract"/>.</summary>
    public readonly struct SeasonState:IEquatable<SeasonState> {
        /// <summary>No provider, or the provider is off for this session.</summary>
        public static SeasonState Unavailable=>default;
        public SeasonKind Season{get;}
        /// <summary>Day in the current season, 1 to <see cref="DaysInSeason"/>.</summary>
        public int DayInSeason{get;}
        public int DaysInSeason{get;}
        /// <summary>Day of the year, at most <see cref="YearLength"/>.</summary>
        public int DayOfYear{get;}
        public int YearLength{get;}
        /// <summary>Provider change counter; any value is valid, consumers compare it for inequality only.</summary>
        public int Revision{get;}
        /// <summary>A season is published; false means use the product's plain (no seasons) behavior.</summary>
        public bool Available=>DaysInSeason>0;

        public SeasonState(SeasonKind season,int dayInSeason,int daysInSeason,int dayOfYear,int yearLength,int revision){
            if(!Valid((int)season,dayInSeason,daysInSeason,dayOfYear,yearLength))throw new ArgumentOutOfRangeException(nameof(season),"invalid season state");
            Season=season;DayInSeason=dayInSeason;DaysInSeason=daysInSeason;DayOfYear=dayOfYear;YearLength=yearLength;Revision=revision;
        }

        // The bounds Solstice's own reader applies to what it publishes, so Crucible never refuses a reading Solstice emits.
        internal static bool Valid(int season,int day,int length,int dayOfYear,int year){
            if(season<(int)SeasonKind.Spring||season>(int)SeasonKind.Winter)return false;
            return length>=0&&year>=length&&day>=0&&day<=length&&dayOfYear>=0&&dayOfYear<=year;
        }

        public bool Equals(SeasonState other)=>Season==other.Season&&DayInSeason==other.DayInSeason&&DaysInSeason==other.DaysInSeason&&DayOfYear==other.DayOfYear&&YearLength==other.YearLength&&Revision==other.Revision;
        public override bool Equals(object? obj)=>obj is SeasonState other&&Equals(other);
        public override int GetHashCode()=>((((((int)Season*397^DayInSeason)*397^DaysInSeason)*397^DayOfYear)*397)^YearLength)*397^Revision;
        public override string ToString()=>Available?Season+" day "+DayInSeason.ToString(CultureInfo.InvariantCulture)+"/"+DaysInSeason.ToString(CultureInfo.InvariantCulture)+", year day "+DayOfYear.ToString(CultureInfo.InvariantCulture)+"/"+YearLength.ToString(CultureInfo.InvariantCulture)+" r"+Revision.ToString(CultureInfo.InvariantCulture):"seasons unavailable r"+Revision.ToString(CultureInfo.InvariantCulture);
    }
}
