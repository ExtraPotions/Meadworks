// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Consumes the existing adopted EcrImport reader and Wildblood EggDecode; source keys remain untouched.
// Adapted from EliteCreaturesReborn 3.30.0 Breeding/EggData.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: 0471a49e972e4453a453e365cb90b5fd20d0f4ea
// Changed by expDARE, 2026: migrate optional egg traits once while preserving native quality and legacy data.
using System.Globalization;
using ExtraPotions.Shepherd.Breeding;
using ExtraPotions.Shepherd.Runtime.Integration;

namespace ExtraPotions.Shepherd.Runtime.Breeding
{
    internal static class LegacyBreeding
    {
        const string EggImported = "shepherd.imported.ecr_egg";
        static bool Allowed(ZNetView view) => NativeBreeding.InheritanceActive && !ShepherdSources.EcrPresent
            && view != null && view.IsValid() && view.IsOwner();
        static string EggText(int stars, int mask, int biome) => stars.ToString(CultureInfo.InvariantCulture) + ","
            + mask.ToString(CultureInfo.InvariantCulture) + "," + biome.ToString(CultureInfo.InvariantCulture);

        internal static void Sire(Procreation parent)
        {
            var view = parent.m_nview;
            if (!Allowed(view)) return;
            var zdo = view.GetZDO(); var store = new ZdoKeyStore(zdo);
            if (store.GetInt(EcrImport.SireImportedKey) != 0 || BreedingStore.ReadPendingSire(store) != null) return;
            var legacy = EcrImport.ReadSire(store.GetInt(EcrImport.SireStarsKey), store.GetInt(EcrImport.SireMaskKey));
            if (legacy == null) return;
            var traits = WildbloodTraitsCapability.Current;
            string carried = legacy.Mask != 0 ? traits.DecodeLegacyEgg(EggText(0, legacy.Mask, 0)) : "";
            if (!Allowed(view) || parent.m_nview != view || !ReferenceEquals(view.GetZDO(), zdo)) return;
            if (!EcrImport.ImportPendingSire(store, out _)) return;
            if (carried.Length != 0) BreedingStore.WritePendingSire(store, new SireRecord("", "", legacy.Level, 0, carried));
            else if (legacy.Mask != 0) NativeBreeding.Verbose("legacy ECR sire level imported; trait mask omitted because no usable provider decoded it");
        }

        internal static void Egg(EggGrow egg)
        {
            var view = egg.m_nview; var item = egg.m_item;
            if (!Allowed(view) || item == null || item.m_nview != view || item.m_itemData.m_stack != 1) return;
            var data = item.m_itemData;
            if (data.m_customData.ContainsKey(EggImported) || data.m_customData.ContainsKey(BreedingKeys.Egg)
                || !data.m_customData.TryGetValue(EcrImport.EggKey, out var raw) || !EcrImport.TryParseEgg(raw, out var legacy)) return;
            var zdo = view.GetZDO();
            string carried = WildbloodTraitsCapability.Current.DecodeLegacyEgg(EggText(legacy.Stars, legacy.Mask, legacy.Biome));
            if (!Allowed(view) || egg.m_nview != view || item.m_nview != view || !ReferenceEquals(view.GetZDO(), zdo)) return;
            if (carried.Length != 0) data.m_customData[BreedingKeys.Egg] = EggCodec.Encode(new EggRecord("", "", "", "", 0, 0, "", carried));
            else if (legacy.Mask != 0) NativeBreeding.Verbose("legacy ECR egg metadata imported; trait mask omitted because no usable provider decoded it");
            data.m_customData[EggImported] = "1";
            item.Save();
        }
    }
}
