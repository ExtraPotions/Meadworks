using System;
using System.Globalization;

namespace ExtraPotions.Meadworks.Contracts {
    /// <summary>Weather event kind of <see cref="WeatherEventsContract"/> 1.0. New kinds need a new major version.</summary>
    public enum WeatherEventKind { None=0, Blizzard=1, Thunderstorm=2 }

    /// <summary>Weather event phase of <see cref="WeatherEventsContract"/> 1.0 (Tempest's timeline).</summary>
    public enum WeatherEventPhase { Idle=0, Warning=1, Active=2, Clearing=3 }

    /// <summary>
    /// Shared read capability <c>meadworks.weather.events</c> version 1.0: Tempest provides the one shared world weather
    /// event, Cairn's time and weather HUD shows its warning and countdown (Tempest then hides its own badge). The shape
    /// is <c>Func&lt;Tuple&lt;int,int,float,int,bool,int&gt;&gt;</c> with items in this order: kind
    /// (<see cref="WeatherEventKind"/>), phase (<see cref="WeatherEventPhase"/>), seconds left in the phase, eligible
    /// biomes as the game's <c>Heightmap.Biome</c> flag bits, whether the biome under the local player is affected, and a
    /// revision that the provider increments whenever kind, phase or biomes change (the change event: a consumer redraws
    /// when the revision differs from the one it last saw; the countdown alone does not bump it).
    /// Idle has kind None, 0 seconds, no biomes and is never affected; a non-idle phase has a kind.
    /// Providers register <c>AddCapability&lt;Func&lt;Tuple&lt;int,int,float,int,bool,int&gt;&gt;&gt;(Id, ContractVersion, ...)</c>;
    /// consumers resolve the same type with <see cref="ContractVersion"/> as the minimum and read through
    /// <see cref="TryRead(Func{Tuple{int,int,float,int,bool,int}}, out WeatherEventState)"/>.
    /// Both sides call the delegate on the Unity main thread only. Additive fields or kinds need a new major version.
    /// </summary>
    public static class WeatherEventsContract {
        public const string Id="meadworks.weather.events";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        /// <summary>Upper bound for seconds left in one phase (one hour; Tempest's longest phase is far shorter).</summary>
        public const float MaximumSecondsLeft=3600f;
        public static Type DelegateType=>typeof(Func<Tuple<int,int,float,int,bool,int>>);

        public static Tuple<int,int,float,int,bool,int> Pack(WeatherEventState state)=>
            Tuple.Create((int)state.Kind,(int)state.Phase,state.SecondsLeft,state.Biomes,state.LocalAffected,state.Revision);

        /// <summary>Reads one provider reply; a null or invalid reply gives false and <see cref="WeatherEventState.Idle"/>.</summary>
        public static bool TryRead(Tuple<int,int,float,int,bool,int>? raw,out WeatherEventState state){
            state=WeatherEventState.Idle;
            if(raw==null||!WeatherEventState.Valid(raw.Item1,raw.Item2,raw.Item3,raw.Item4,raw.Item5))return false;
            state=new WeatherEventState((WeatherEventKind)raw.Item1,(WeatherEventPhase)raw.Item2,raw.Item3,raw.Item4,raw.Item5,raw.Item6);
            return true;
        }

        /// <summary>Calls the provider and reads its reply; an absent or throwing provider gives false and the idle state.</summary>
        public static bool TryRead(Func<Tuple<int,int,float,int,bool,int>>? provider,out WeatherEventState state){
            state=WeatherEventState.Idle;
            if(provider==null)return false;
            Tuple<int,int,float,int,bool,int> raw;
            try{raw=provider();}catch{return false;}
            return TryRead(raw,out state);
        }
    }

    /// <summary>
    /// Shared read capability <c>meadworks.weather.events.detail</c> version 1.0, provided next to
    /// <see cref="WeatherEventsContract"/> 1.0 (which stays unchanged for older consumers). The shape is
    /// <c>Func&lt;Tuple&lt;int,int,float,int,bool,int,string&gt;&gt;</c>: the 1.0 items in the same order and meaning, with an
    /// open kind number, then the kind's name token. Kind numbers 1 and 2 keep their 1.0 meaning (blizzard, thunderstorm);
    /// Tempest adds 3 lightning storm, 4 dense fog, 5 ash storm and 6 meteor shower, and consumers must accept numbers they do
    /// not know (they show the token's name, or nothing). The token is 1 to 32 lowercase letters, digits or underscores
    /// (Tempest: blizzard, thunderstorm, lightning_storm, dense_fog, ash_storm, meteor_shower) and empty while idle.
    /// Consumers resolve this first and fall back to 1.0 when it is absent. Main thread only, as 1.0.
    /// </summary>
    public static class WeatherEventsDetailContract {
        public const string Id="meadworks.weather.events.detail";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public const int MaximumTokenLength=32;
        public static Type DelegateType=>typeof(Func<Tuple<int,int,float,int,bool,int,string>>);

        /// <summary>Reads one provider reply; a null or invalid reply gives false, the idle state and an empty token.</summary>
        public static bool TryRead(Tuple<int,int,float,int,bool,int,string>? raw,out WeatherEventDetail detail){
            detail=WeatherEventDetail.Idle;
            if(raw==null||!Valid(raw.Item1,raw.Item2,raw.Item3,raw.Item4,raw.Item5,raw.Item7))return false;
            detail=new WeatherEventDetail(raw.Item1,(WeatherEventPhase)raw.Item2,raw.Item3,raw.Item4,raw.Item5,raw.Item6,raw.Item7);
            return true;
        }

        /// <summary>Calls the provider and reads its reply; an absent or throwing provider gives false and the idle state.</summary>
        public static bool TryRead(Func<Tuple<int,int,float,int,bool,int,string>>? provider,out WeatherEventDetail detail){
            detail=WeatherEventDetail.Idle;
            if(provider==null)return false;
            Tuple<int,int,float,int,bool,int,string> raw;
            try{raw=provider();}catch{return false;}
            return TryRead(raw,out detail);
        }

        /// <summary>A name token: 1 to <see cref="MaximumTokenLength"/> of a-z, 0-9 and underscore.</summary>
        public static bool ValidToken(string? token){
            if(string.IsNullOrEmpty(token)||token!.Length>MaximumTokenLength)return false;
            foreach(var c in token)if(!(c>='a'&&c<='z')&&!(c>='0'&&c<='9')&&c!='_')return false;
            return true;
        }

        internal static bool Valid(int kind,int phase,float secondsLeft,int biomes,bool localAffected,string? token){
            if(kind<0||phase<(int)WeatherEventPhase.Idle||phase>(int)WeatherEventPhase.Clearing)return false;
            if(float.IsNaN(secondsLeft)||float.IsInfinity(secondsLeft)||secondsLeft<0f||secondsLeft>WeatherEventsContract.MaximumSecondsLeft)return false;
            if(biomes<0)return false;
            if(phase==(int)WeatherEventPhase.Idle)return kind==0&&secondsLeft==0f&&biomes==0&&!localAffected&&string.IsNullOrEmpty(token);
            return kind>0&&ValidToken(token);
        }
    }

    /// <summary>One reading of <see cref="WeatherEventsDetailContract"/>: the open kind number and its name token.</summary>
    public readonly struct WeatherEventDetail {
        public static WeatherEventDetail Idle=>new WeatherEventDetail(0,WeatherEventPhase.Idle,0f,0,false,0,"");
        public int Kind{get;}
        public WeatherEventPhase Phase{get;}
        public float SecondsLeft{get;}
        public int Biomes{get;}
        public bool LocalAffected{get;}
        public int Revision{get;}
        public string Token{get;}
        public bool Running=>Phase!=WeatherEventPhase.Idle;

        public WeatherEventDetail(int kind,WeatherEventPhase phase,float secondsLeft,int biomes,bool localAffected,int revision,string token){
            if(!WeatherEventsDetailContract.Valid(kind,(int)phase,secondsLeft,biomes,localAffected,token))throw new ArgumentOutOfRangeException(nameof(kind),"invalid weather event detail");
            Kind=kind;Phase=phase;SecondsLeft=secondsLeft;Biomes=biomes;LocalAffected=localAffected;Revision=revision;Token=token??"";
        }

        public override string ToString()=>Running?Token+" ("+Kind.ToString(CultureInfo.InvariantCulture)+") "+Phase+" "+SecondsLeft.ToString("0",CultureInfo.InvariantCulture)+"s biomes "+Biomes+(LocalAffected?" here":"")+" r"+Revision:"idle r"+Revision;
    }

    /// <summary>One reading of <see cref="WeatherEventsContract"/>.</summary>
    public readonly struct WeatherEventState:IEquatable<WeatherEventState> {
        public static WeatherEventState Idle=>default;
        public WeatherEventKind Kind{get;}
        public WeatherEventPhase Phase{get;}
        /// <summary>Seconds left in the current phase (0 to <see cref="WeatherEventsContract.MaximumSecondsLeft"/>).</summary>
        public float SecondsLeft{get;}
        /// <summary>Eligible biomes as <c>Heightmap.Biome</c> flag bits (never negative).</summary>
        public int Biomes{get;}
        /// <summary>The biome under the local player gets this event's environment now.</summary>
        public bool LocalAffected{get;}
        /// <summary>Provider change counter; any value is valid, consumers compare it for inequality only.</summary>
        public int Revision{get;}
        public bool Running=>Phase!=WeatherEventPhase.Idle;

        public WeatherEventState(WeatherEventKind kind,WeatherEventPhase phase,float secondsLeft,int biomes,bool localAffected,int revision){
            if(!Valid((int)kind,(int)phase,secondsLeft,biomes,localAffected))throw new ArgumentOutOfRangeException(nameof(kind),"invalid weather event state");
            Kind=kind;Phase=phase;SecondsLeft=secondsLeft;Biomes=biomes;LocalAffected=localAffected;Revision=revision;
        }

        internal static bool Valid(int kind,int phase,float secondsLeft,int biomes,bool localAffected){
            if(kind<(int)WeatherEventKind.None||kind>(int)WeatherEventKind.Thunderstorm)return false;
            if(phase<(int)WeatherEventPhase.Idle||phase>(int)WeatherEventPhase.Clearing)return false;
            if(float.IsNaN(secondsLeft)||float.IsInfinity(secondsLeft)||secondsLeft<0f||secondsLeft>WeatherEventsContract.MaximumSecondsLeft)return false;
            if(biomes<0)return false;
            if(phase==(int)WeatherEventPhase.Idle)return kind==(int)WeatherEventKind.None&&secondsLeft==0f&&biomes==0&&!localAffected;
            return kind!=(int)WeatherEventKind.None;
        }

        public bool Equals(WeatherEventState other)=>Kind==other.Kind&&Phase==other.Phase&&SecondsLeft.Equals(other.SecondsLeft)&&Biomes==other.Biomes&&LocalAffected==other.LocalAffected&&Revision==other.Revision;
        public override bool Equals(object? obj)=>obj is WeatherEventState other&&Equals(other);
        public override int GetHashCode()=>((((int)Kind*397^(int)Phase)*397^SecondsLeft.GetHashCode())*397^Biomes)*397^Revision^(LocalAffected?7919:0);
        public override string ToString()=>Running?Kind+" "+Phase+" "+SecondsLeft.ToString("0",CultureInfo.InvariantCulture)+"s biomes "+Biomes+(LocalAffected?" here":"")+" r"+Revision:"idle r"+Revision;
    }
}
