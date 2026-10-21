using System;
using System.Collections.Generic;
using System.Linq;
namespace Wayfarer.Core {
    /// <summary>Which destinations the server has switched on. Bed is a feature; every other destination is a Travel cheat that also needs the actor's Mischief travel access.</summary>
    public sealed class TeleportPolicy {
        /// <summary>Travel-cheat destinations the admin lets every player use, whatever their Mischief travel access.</summary>
        public IReadOnlyCollection<Category> PlayerCategories{get;}
        public bool AllowTeleport{get;}public bool AllowBed{get;}public bool AllowStones{get;}public bool AllowMap{get;}public bool AllowBoat{get;}public bool AllowGrave{get;}public bool AllowPlayer{get;}
        public TeleportPolicy(bool allowTeleport=true,bool allowBed=true,bool allowStones=false,bool allowMap=false,bool allowBoat=false,bool allowGrave=false,bool allowPlayer=false,IReadOnlyCollection<Category>? playerCategories=null){
            PlayerCategories=playerCategories??Array.Empty<Category>();
            AllowTeleport=allowTeleport;AllowBed=allowBed;AllowStones=allowStones;AllowMap=allowMap;AllowBoat=allowBoat;AllowGrave=allowGrave;AllowPlayer=allowPlayer;
        }
        public static bool IsCheat(Category category)=>category!=Category.Bed;
        public bool Enables(Category category)=>AllowTeleport&&(category==Category.Bed?AllowBed:category==Category.Stones?AllowStones:category==Category.Map?AllowMap:category==Category.Boat?AllowBoat:category==Category.Grave?AllowGrave:category==Category.Player&&AllowPlayer);
        public bool AllowsCategory(Category category,bool travelCheats)=>Refusal(category,travelCheats)==null;
        /// <summary>Why <paramref name="category"/> is refused (a <see cref="TeleportRefusals"/> code), or null when allowed.</summary>
        public string? Refusal(Category category,bool travelCheats)=>!AllowTeleport?TeleportRefusals.TeleportDisabled:!Enables(category)?TeleportRefusals.DestinationDisabled:IsCheat(category)&&!travelCheats&&!PlayerCategories.Contains(category)?TeleportRefusals.TravelRestricted:null;
    }
    /// <summary>Server refusal codes, one per cause; each is also the catalog key of the player's message.</summary>
    public static class TeleportRefusals {
        /// <summary>Server.AllowTeleport is off.</summary>
        public const string TeleportDisabled="teleportdisabled";
        /// <summary>The server has not switched this destination on.</summary>
        public const string DestinationDisabled="destinationdisabled";
        /// <summary>The destination is a server-enabled travel cheat, but the Mischief Travel policy does not grant it to this player.</summary>
        public const string TravelRestricted="travelrestricted";
        public static readonly IReadOnlyList<string> Codes=Array.AsReadOnly(new[]{TeleportDisabled,DestinationDisabled,TravelRestricted});
        public static bool Is(string? code)=>code!=null&&Codes.Contains(code);
    }
    public sealed class TeleportRequest {
        public string RequestId{get;}public Category Category{get;}public string DestinationId{get;}public float X{get;}public float Y{get;}public float Z{get;}public bool IsLastKnown{get;}
        public TeleportRequest(string request,Category category,string destination,float x,float y,float z,bool isLastKnown=false){Text(request);Text(destination);if(!Enum.IsDefined(typeof(Category),category))throw new ArgumentException("Unknown category.");RequestId=request;Category=category;DestinationId=destination;X=x;Y=y;Z=z;IsLastKnown=isLastKnown;}
        internal static void Text(string value){if(string.IsNullOrWhiteSpace(value)||value.Length>128||value.IndexOfAny(new[]{'\0','\r','\n'})>=0)throw new ArgumentException("Invalid teleport correlation.");}
    }
    public sealed class TeleportActorFacts {
        public string ActorId{get;}public long WorldId{get;}public string SessionId{get;}public bool TravelCheats{get;}public bool Present{get;}public bool Alive{get;}public bool Teleporting{get;}public bool CooldownReady{get;}
        public TeleportActorFacts(string actor,long world,string session,bool travelCheats,bool present,bool alive,bool teleporting,bool cooldownReady){TeleportRequest.Text(actor);TeleportRequest.Text(session);ActorId=actor;WorldId=world;SessionId=session;TravelCheats=travelCheats;Present=present;Alive=alive;Teleporting=teleporting;CooldownReady=cooldownReady;}
    }
    public sealed class TeleportTargetFacts {
        public bool Valid{get;}public float X{get;}public float Y{get;}public float Z{get;}
        public TeleportTargetFacts(bool valid,float x,float y,float z){Valid=valid;X=x;Y=y;Z=z;}
    }
    public sealed class AuthorizationDecision {
        public bool Allowed{get;}public string Code{get;}public TeleportTargetFacts Target{get;}
        internal AuthorizationDecision(bool allowed,string code,TeleportTargetFacts target){Allowed=allowed;Code=code;Target=target;}
    }
    public static class TeleportAuthorization {
        public static bool Advanced(Category category)=>category==Category.Boat||category==Category.Grave||category==Category.Player;
        public static AuthorizationDecision Evaluate(TeleportPolicy policy,TeleportActorFacts actor,TeleportRequest request,TeleportTargetFacts target){
            var code=policy.Refusal(request.Category,actor.TravelCheats)??(!actor.Present||actor.WorldId==0?"unavailable":!actor.Alive?"dead":actor.Teleporting||!actor.CooldownReady?"busy":!LandingPolicy.IsValid(request.X,request.Y,request.Z)||!target.Valid||!LandingPolicy.IsValid(target.X,target.Y,target.Z)?"unavailable":"success");
            return new AuthorizationDecision(code=="success",code,target);
        }
    }
    public sealed class TeleportGrant {
        public string ActorId{get;}public long WorldId{get;}public string SessionId{get;}public string RequestId{get;}public string GrantId{get;}public Category Category{get;}public string DestinationId{get;}public float X{get;}public float Y{get;}public float Z{get;}public double ExpiresAt{get;}
        public TeleportGrant(string actor,long world,string session,string request,string grant,Category category,string destination,float x,float y,float z,double expires){
            TeleportRequest.Text(actor);TeleportRequest.Text(session);TeleportRequest.Text(request);TeleportRequest.Text(grant);TeleportRequest.Text(destination);
            if(world==0||!Enum.IsDefined(typeof(Category),category)||!LandingPolicy.IsValid(x,y,z)||double.IsNaN(expires)||double.IsInfinity(expires))throw new ArgumentException("Invalid grant.");
            ActorId=actor;WorldId=world;SessionId=session;RequestId=request;GrantId=grant;Category=category;DestinationId=destination;X=x;Y=y;Z=z;ExpiresAt=expires;
        }
    }
    public sealed class TeleportCompletion {
        public string RequestId{get;}public string GrantId{get;}public bool Accepted{get;}
        public TeleportCompletion(string request,string grant,bool accepted){TeleportRequest.Text(request);TeleportRequest.Text(grant);RequestId=request;GrantId=grant;Accepted=accepted;}
    }
}
