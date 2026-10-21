using System;

namespace ExtraPotions.Meadworks.Contracts {
    /// <summary>
    /// Shared read capability <c>meadworks.ember.sleep</c> version 1.0: Ember provides the current sleep state, Cairn
    /// (or any HUD) reads it. Crucible resolves a capability by its exact contract <see cref="Type"/>, so the shape is a
    /// BCL delegate over a BCL tuple, identical in every assembly, and no product references another product:
    /// <c>Func&lt;Tuple&lt;bool,int,int,int,double,bool,float&gt;&gt;</c> with items in this order:
    /// window open, players, sleeping, required, extra rate, full skip running, full skip progress.
    /// Providers register it with <c>AddCapability&lt;Func&lt;Tuple&lt;...&gt;&gt;&gt;(Id, ContractVersion, ...)</c>;
    /// consumers resolve the same type with <see cref="ContractVersion"/> as the minimum and read it through
    /// <see cref="TryRead(Func{Tuple{bool,int,int,int,double,bool,float}}, out EmberSleepState)"/>.
    /// Both sides call the delegate on the Unity main thread only. Additive fields need a new major version.
    /// </summary>
    public static class EmberSleepContract {
        public const string Id="meadworks.ember.sleep";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        /// <summary>Bounds shared with Ember's <c>meadworks.ember.sleep.state</c> peer command reply.</summary>
        public const int MaximumPlayers=1024;
        public const double MaximumExtraRate=30;
        public static Type DelegateType=>typeof(Func<Tuple<bool,int,int,int,double,bool,float>>);

        public static Tuple<bool,int,int,int,double,bool,float> Pack(EmberSleepState state)=>
            Tuple.Create(state.WindowOpen,state.Players,state.Sleeping,state.Required,state.ExtraRate,state.SkipRunning,state.SkipProgress);

        /// <summary>Reads one provider reply; a null or out-of-bounds reply gives false and <see cref="EmberSleepState.Closed"/>.</summary>
        public static bool TryRead(Tuple<bool,int,int,int,double,bool,float>? raw,out EmberSleepState state){
            state=EmberSleepState.Closed;
            if(raw==null||!EmberSleepState.Valid(raw.Item2,raw.Item3,raw.Item4,raw.Item5,raw.Item7))return false;
            state=new EmberSleepState(raw.Item1,raw.Item2,raw.Item3,raw.Item4,raw.Item5,raw.Item6,raw.Item7);
            return true;
        }

        /// <summary>Calls the provider and reads its reply; an absent or throwing provider gives false and the closed state.</summary>
        public static bool TryRead(Func<Tuple<bool,int,int,int,double,bool,float>>? provider,out EmberSleepState state){
            state=EmberSleepState.Closed;
            if(provider==null)return false;
            Tuple<bool,int,int,int,double,bool,float> raw;
            try{raw=provider();}catch{return false;}
            return TryRead(raw,out state);
        }
    }

    /// <summary>One sleep state reading of <see cref="EmberSleepContract"/>.</summary>
    public readonly struct EmberSleepState:IEquatable<EmberSleepState> {
        public static EmberSleepState Closed=>default;
        /// <summary>The night sleep window is open (players may lie down to speed time or skip).</summary>
        public bool WindowOpen{get;}
        /// <summary>Players counted for sleep (0 to <see cref="EmberSleepContract.MaximumPlayers"/>).</summary>
        public int Players{get;}
        /// <summary>Players in bed now (at most <see cref="Players"/>).</summary>
        public int Sleeping{get;}
        /// <summary>Players needed in bed for the full skip (at most <see cref="Players"/>).</summary>
        public int Required{get;}
        /// <summary>Extra time rate from sleepers (0 to <see cref="EmberSleepContract.MaximumExtraRate"/>); above zero is boosting.</summary>
        public double ExtraRate{get;}
        /// <summary>The full skip to morning is running.</summary>
        public bool SkipRunning{get;}
        /// <summary>Full skip progress, 0 to 1.</summary>
        public float SkipProgress{get;}
        public bool Boosting=>ExtraRate>0;

        public EmberSleepState(bool windowOpen,int players,int sleeping,int required,double extraRate,bool skipRunning,float skipProgress){
            if(players<0||players>EmberSleepContract.MaximumPlayers)throw new ArgumentOutOfRangeException(nameof(players));
            if(sleeping<0||sleeping>players)throw new ArgumentOutOfRangeException(nameof(sleeping));
            if(required<0||required>players)throw new ArgumentOutOfRangeException(nameof(required));
            if(double.IsNaN(extraRate)||double.IsInfinity(extraRate)||extraRate<0||extraRate>EmberSleepContract.MaximumExtraRate)throw new ArgumentOutOfRangeException(nameof(extraRate));
            if(float.IsNaN(skipProgress)||float.IsInfinity(skipProgress)||skipProgress<0||skipProgress>1)throw new ArgumentOutOfRangeException(nameof(skipProgress));
            WindowOpen=windowOpen;Players=players;Sleeping=sleeping;Required=required;ExtraRate=extraRate;SkipRunning=skipRunning;SkipProgress=skipProgress;
        }

        internal static bool Valid(int players,int sleeping,int required,double extraRate,float skipProgress)=>
            players>=0&&players<=EmberSleepContract.MaximumPlayers&&sleeping>=0&&sleeping<=players&&required>=0&&required<=players
            &&!double.IsNaN(extraRate)&&!double.IsInfinity(extraRate)&&extraRate>=0&&extraRate<=EmberSleepContract.MaximumExtraRate
            &&!float.IsNaN(skipProgress)&&!float.IsInfinity(skipProgress)&&skipProgress>=0&&skipProgress<=1;

        public bool Equals(EmberSleepState other)=>WindowOpen==other.WindowOpen&&Players==other.Players&&Sleeping==other.Sleeping&&Required==other.Required
            &&ExtraRate.Equals(other.ExtraRate)&&SkipRunning==other.SkipRunning&&SkipProgress.Equals(other.SkipProgress);
        public override bool Equals(object? obj)=>obj is EmberSleepState other&&Equals(other);
        public override int GetHashCode()=>((((WindowOpen?1:0)*397^Players)*397^Sleeping)*397^Required)*397^ExtraRate.GetHashCode()^(SkipRunning?7919:0)^SkipProgress.GetHashCode();
        public override string ToString()=>(WindowOpen?"open":"closed")+" "+Sleeping+"/"+Players+" (need "+Required+") rate "+ExtraRate.ToString(System.Globalization.CultureInfo.InvariantCulture)+(SkipRunning?" skip "+SkipProgress.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture):"");
    }
}
