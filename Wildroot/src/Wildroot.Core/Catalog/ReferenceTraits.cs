using System.Collections.Generic;
namespace ExtraPotions.Wildroot {
 /// <summary>
 /// Growth biome masks and temperature tolerance of PlantEverything at its defaults where they are not the game's own
 /// prefab values; PlantEverything is the parity reference. The 1.21.3 values were measured field by field in game (plant
 /// settings of every entry, Wildroot lane pc network-006c9236 against the reference network-13d6c686). PlantEverything
 /// 1.21.4 (source commit 864defe2, StaticContent.GenerateCropRefs) adds AshLands back to the ten crops whose game mask has it,
 /// so they grow there again, under a shield or with heat tolerance; <see cref="AshlandsCrops"/> lists them. Barley, flax,
 /// magecap and Jotun puffs are unchanged. Biome masks use Heightmap.Biome bits (Valheim 1.0.20):
 /// Meadows 1, Swamp 2, Mountain 4, BlackForest 8, Plains 16, AshLands 32, DeepNorth 64, Ocean 256, Mistlands 512.
 /// FirTree_big_Sapling keeps the game's own values (Valheim 1.0.20 prefab: m_biome 29, Meadows|Mountain|BlackForest|Plains,
 /// cold tolerant), which is also PlantEverything 1.21.4's Timberwood mask; sapling_jotunpuffs' pin equals its 1.0.20 prefab mask (512).
 /// Ashlands-native plants (Ashvine, Ashwood sapling, Ashlands fern, Fiddlehead, Smoke puff) keep the game's masks.
 /// </summary>
 public static class ReferenceTraits {
  const int Meadows=1,Swamp=2,BlackForest=8,Plains=16,AshLands=32,Mistlands=512;
  const int Farmland=Meadows|BlackForest|Plains;
  static readonly Dictionary<string,int> biomes=new Dictionary<string,int>{
   {"sapling_turnip",Farmland|Swamp|Mistlands},{"sapling_seedturnip",Farmland|Swamp|Mistlands},
   {"sapling_carrot",Farmland},{"sapling_seedcarrot",Farmland},{"sapling_onion",Farmland},{"sapling_seedonion",Farmland},
   {"sapling_oat",Farmland},{"sapling_poteitr",Farmland},{"sapling_Kale",Farmland},{"sapling_seedkale",Farmland},
   {"sapling_barley",Plains},{"sapling_flax",Plains},{"sapling_magecap",Mistlands},{"sapling_jotunpuffs",Mistlands}};
  /// <summary>Crops PlantEverything 1.21.4 lets grow in the Ashlands (its StandardCropBiomes and HardyCropBiomes).</summary>
  static readonly HashSet<string> ashlandsSet=new HashSet<string>{
   "sapling_turnip","sapling_seedturnip","sapling_carrot","sapling_seedcarrot","sapling_onion","sapling_seedonion",
   "sapling_oat","sapling_poteitr","sapling_Kale","sapling_seedkale"};
  public static IReadOnlyCollection<string> AshlandsCrops=>ashlandsSet;
  static readonly Dictionary<string,bool> heat=new Dictionary<string,bool>{{"Ashwood_Sapling",true}};
  static readonly Dictionary<string,bool> cold=new Dictionary<string,bool>();
  /// <summary>
  /// The reference growth biome mask while biomes are enforced, or null where the game's own mask applies. With
  /// <paramref name="ashlandsCrops"/> on (PlantEverything 1.21.4, the default) the <see cref="AshlandsCrops"/> include
  /// the Ashlands; off gives the 1.21.3 masks.
  /// </summary>
  public static int? Biome(string? prefab,bool ashlandsCrops=true){
   if(prefab==null||!biomes.TryGetValue(prefab,out var mask))return null;
   return ashlandsCrops&&ashlandsSet.Contains(prefab)?mask|AshLands:mask;
  }
  /// <summary>The reference heat tolerance, or null where the game's own value applies.</summary>
  public static bool? Heat(string? prefab)=>prefab!=null&&heat.TryGetValue(prefab,out var v)?v:(bool?)null;
  /// <summary>The reference cold tolerance, or null where the game's own value applies.</summary>
  public static bool? Cold(string? prefab)=>prefab!=null&&cold.TryGetValue(prefab,out var v)?v:(bool?)null;
 }
}
