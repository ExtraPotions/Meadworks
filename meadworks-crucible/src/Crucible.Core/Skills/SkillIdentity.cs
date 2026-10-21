using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Valheim's string hash (assembly_utils <c>StringExtensionMethods.GetStableHashCode</c>), reimplemented so Core
    /// stays free of game assemblies. Two interleaved djb2-xor hashes over even and odd characters, stopping at the
    /// first NUL, combined as <c>a + b * 1566083941</c> with wrap-around. Test vectors come from the game's own method.
    /// </summary>
    public static class StableHash {
        public static int Of(string text){
            if(text==null)throw new ArgumentNullException(nameof(text));
            unchecked{
                int a=5381,b=a;
                for(int i=0;i<text.Length&&text[i]!='\0';i+=2){
                    a=((a<<5)+a)^text[i];
                    if(i==text.Length-1||text[i+1]=='\0')break;
                    b=((b<<5)+b)^text[i+1];
                }
                return a+b*1566083941;
            }
        }
    }

    /// <summary>The shape of a skill identity: lower case, starts with a letter, 3 to 64 of a-z, 0-9, '_' and '.'.</summary>
    public static class SkillIdentities {
        static readonly Regex Shape=new Regex("^[a-z][a-z0-9_.]{2,63}$",RegexOptions.CultureInvariant);
        public static bool IsValid(string? identity)=>identity!=null&&Shape.IsMatch(identity);
        internal static string Require(string? identity,string parameter){
            if(identity==null)throw new ArgumentNullException(parameter);
            if(!IsValid(identity))throw new ArgumentException("Skill identities are 3 to 64 characters of a-z, 0-9, '_' and '.', starting with a letter.",parameter);
            return identity;
        }
    }

    /// <summary>
    /// A skill's number, the value of the game's <c>Skills.SkillType</c>. Vanilla skills use 1 to 998 (999 is "All").
    /// A custom skill's number is the stable hash of its identity masked positive (<c>&amp; 0x7FFFFFFF</c>), exactly as
    /// GrindstoneSkills' <c>CustomSkill</c> derives it, so a Meadworks skill that keeps a GrindstoneSkills identity
    /// reads the same saved level. Jotunn's SkillManager uses <c>Math.Abs</c> instead, which differs for negative
    /// hashes (grindstone_defense), so Crucible does not register through Jotunn.
    /// </summary>
    public readonly struct SkillId:IEquatable<SkillId> {
        /// <summary>Numbers below this belong to the game (Jotunn refuses them for the same reason).</summary>
        public const int FirstCustom=1000;
        public int Value { get; }
        SkillId(int value){Value=value;}
        public bool IsVanilla=>Value>0&&Value<FirstCustom;
        public bool IsCustom=>Value>=FirstCustom;
        public static SkillId FromIdentity(string identity){
            SkillIdentities.Require(identity,nameof(identity));
            int value=StableHash.Of(identity)&0x7FFFFFFF;
            if(value<FirstCustom)throw new ArgumentException("This identity hashes into the game's skill numbers; choose another.",nameof(identity));
            return new SkillId(value);
        }
        public static SkillId Vanilla(int value){
            if(value<=0||value>=999)throw new ArgumentOutOfRangeException(nameof(value),"Vanilla skill numbers are 1 to 998.");
            return new SkillId(value);
        }
        /// <summary>Any stored number (a saved record, a <c>Skills.SkillType</c> cast); negative numbers are refused.</summary>
        public static SkillId FromRaw(int value){
            if(value<0)throw new ArgumentOutOfRangeException(nameof(value));
            return new SkillId(value);
        }
        public bool Equals(SkillId other)=>Value==other.Value;
        public override bool Equals(object? obj)=>obj is SkillId other&&Equals(other);
        public override int GetHashCode()=>Value;
        public static bool operator==(SkillId a,SkillId b)=>a.Value==b.Value;
        public static bool operator!=(SkillId a,SkillId b)=>a.Value!=b.Value;
        public override string ToString()=>Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The game's skills (<c>Skills.SkillType</c>, read from assembly_valheim 2026-10-06) for products that modify them.</summary>
    public static class VanillaSkills {
        public static readonly SkillId Swords=SkillId.Vanilla(1),Knives=SkillId.Vanilla(2),Clubs=SkillId.Vanilla(3),Polearms=SkillId.Vanilla(4),
            Spears=SkillId.Vanilla(5),Blocking=SkillId.Vanilla(6),Axes=SkillId.Vanilla(7),Bows=SkillId.Vanilla(8),ElementalMagic=SkillId.Vanilla(9),
            BloodMagic=SkillId.Vanilla(10),Unarmed=SkillId.Vanilla(11),Pickaxes=SkillId.Vanilla(12),WoodCutting=SkillId.Vanilla(13),Crossbows=SkillId.Vanilla(14),
            Jump=SkillId.Vanilla(100),Sneak=SkillId.Vanilla(101),Run=SkillId.Vanilla(102),Swim=SkillId.Vanilla(103),Fishing=SkillId.Vanilla(104),
            Cooking=SkillId.Vanilla(105),Farming=SkillId.Vanilla(106),Crafting=SkillId.Vanilla(107),Dodge=SkillId.Vanilla(108),Ride=SkillId.Vanilla(110);
        static readonly KeyValuePair<string,SkillId>[] Names={
            new("Swords",Swords),new("Knives",Knives),new("Clubs",Clubs),new("Polearms",Polearms),new("Spears",Spears),new("Blocking",Blocking),
            new("Axes",Axes),new("Bows",Bows),new("ElementalMagic",ElementalMagic),new("BloodMagic",BloodMagic),new("Unarmed",Unarmed),
            new("Pickaxes",Pickaxes),new("WoodCutting",WoodCutting),new("Crossbows",Crossbows),new("Jump",Jump),new("Sneak",Sneak),new("Run",Run),
            new("Swim",Swim),new("Fishing",Fishing),new("Cooking",Cooking),new("Farming",Farming),new("Crafting",Crafting),new("Dodge",Dodge),new("Ride",Ride)};
        public static IReadOnlyList<KeyValuePair<string,SkillId>> All=>Names;
        public static bool TryParse(string? name,out SkillId id){
            foreach(var pair in Names)if(string.Equals(pair.Key,name,StringComparison.OrdinalIgnoreCase)){id=pair.Value;return true;}
            id=default;return false;
        }
        /// <summary>The enum name, or null for a number the game does not define.</summary>
        public static string? NameOf(SkillId id)=>Names.Where(pair=>pair.Value==id).Select(pair=>pair.Key).FirstOrDefault();
    }
}
