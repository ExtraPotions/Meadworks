using System;

namespace ExtraPotions.Meadworks.Contracts {
    /// <summary>
    /// Shared read capability <c>meadworks.explore.radius</c> version 1.0: Keel provides the local player's sea
    /// exploration factor from Sailing, Cairn multiplies it into its sea explore radius. The shape is the BCL delegate
    /// <c>Func&lt;float&gt;</c>: the provider reads the local player itself (Contracts has no game reference), returning
    /// <c>1 + share(Exploration Radius At 100, Sailing level)</c> while the local player is aboard a ship, else exactly 1.
    /// A valid factor is finite and in (0, <see cref="MaximumFactor"/>]; 1 is neutral.
    /// Providers register <c>AddCapability&lt;Func&lt;float&gt;&gt;(Id, ContractVersion, ...)</c>; consumers resolve the
    /// same type with <see cref="ContractVersion"/> as the minimum and read through
    /// <see cref="ReadFactor(Func{float})"/>, which gives the neutral factor for an absent, throwing or invalid provider.
    /// Both sides call the delegate on the Unity main thread only. Any change of meaning is a new major version.
    /// </summary>
    public static class ExploreRadiusContract {
        public const string Id="meadworks.explore.radius";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public const float NeutralFactor=1f;
        public const float MaximumFactor=16f;
        public static Type DelegateType=>typeof(Func<float>);

        /// <summary>True for a finite factor above 0 and at most <see cref="MaximumFactor"/>.</summary>
        public static bool Valid(float factor)=>!float.IsNaN(factor)&&!float.IsInfinity(factor)&&factor>0f&&factor<=MaximumFactor;

        /// <summary>Calls the provider; an absent, throwing or out-of-bounds provider gives false and the neutral factor.</summary>
        public static bool TryRead(Func<float>? provider,out float factor){
            factor=NeutralFactor;
            if(provider==null)return false;
            float raw;
            try{raw=provider();}catch{return false;}
            if(!Valid(raw))return false;
            factor=raw;
            return true;
        }

        /// <summary>The provider's factor, or <see cref="NeutralFactor"/> when it cannot be read.</summary>
        public static float ReadFactor(Func<float>? provider)=>TryRead(provider,out var factor)?factor:NeutralFactor;
    }

    /// <summary>
    /// Shared read capability <c>meadworks.explore.current</c> version 1.0: Cairn provides the local player's current
    /// computed exploration radius in meters (after its land or sea radius, every factor and its clamp), Keel's ship panel
    /// shows it. The shape is the BCL delegate <c>Func&lt;float&gt;</c>. A valid radius is finite and in
    /// [0, <see cref="MaximumRadius"/>]. Without a provider Keel shows the vanilla radius times its own factor.
    /// Providers register <c>AddCapability&lt;Func&lt;float&gt;&gt;(Id, ContractVersion, ...)</c>; consumers resolve the
    /// same type with <see cref="ContractVersion"/> as the minimum and read through <see cref="TryRead(Func{float}, out float)"/>.
    /// Both sides call the delegate on the Unity main thread only. Any change of meaning is a new major version.
    /// </summary>
    public static class ExploreCurrentContract {
        public const string Id="meadworks.explore.current";
        public static readonly System.Version ContractVersion=new System.Version(1,0);
        public const float MaximumRadius=10000f;
        public static Type DelegateType=>typeof(Func<float>);

        /// <summary>True for a finite radius from 0 to <see cref="MaximumRadius"/> meters.</summary>
        public static bool Valid(float radius)=>!float.IsNaN(radius)&&!float.IsInfinity(radius)&&radius>=0f&&radius<=MaximumRadius;

        /// <summary>Calls the provider; an absent, throwing or out-of-bounds provider gives false and radius 0.</summary>
        public static bool TryRead(Func<float>? provider,out float radius){
            radius=0f;
            if(provider==null)return false;
            float raw;
            try{raw=provider();}catch{return false;}
            if(!Valid(raw))return false;
            radius=raw;
            return true;
        }
    }
}
