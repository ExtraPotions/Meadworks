# Building Meadworks

## Requirements

- The .NET SDK (the release was built with .NET 11). Projects target netstandard2.1 and restore NuGet packages from nuget.org.
- A Valheim client or dedicated server installation. Set `VALHEIM_DIR` to it, or pass `-p:GameDir=<folder>`.
  Game assemblies are compile-time references only and are not distributed.
- BepInExPack_Valheim 5.4.2351 and Jotunn 2.30.2 from Thunderstore, extracted into `assets/dependencies/` at the root of
  this repository, or passed with `-p:BepInExDir=<BepInEx folder>` and `-p:JotunnDir=<folder containing Jotunn.dll>`.

## Layout

```text
Meadworks/
  assets/dependencies/BepInExPack_Valheim-5.4.2351/BepInExPack_Valheim/BepInEx/
  assets/dependencies/Jotunn-2.30.2/plugins/Jotunn.dll
  meadworks-crucible/      Crucible, Crucible.Core, Crucible.Contracts, Crucible.UI
  <Product>/               one folder per product
```

Every product builds against `meadworks-crucible/` through its `CrucibleRoot` property. To use another Crucible checkout,
pass `-p:CrucibleRoot=<path>`. `assets/dependencies/` is ignored by git.

## Build

Run from the repository root. Each product's plugin project also builds its Core project and Crucible.

```text
dotnet build meadworks-crucible/src/Crucible.Plugin/Crucible.Plugin.csproj -c Release
dotnet build meadworks-crucible/src/Crucible.UI/Crucible.UI.csproj -c Release
dotnet build Almanac/src/Almanac.Plugin/Almanac.Plugin.csproj -c Release
dotnet build Beacon/src/Beacon.Plugin/Beacon.Plugin.csproj -c Release
dotnet build Bestiary/src/Bestiary.Plugin/Bestiary.Plugin.csproj -c Release
dotnet build Burrow/src/Burrow.Plugin/Burrow.Plugin.csproj -c Release
dotnet build Cairn/src/Cairn.Plugin/Cairn.Plugin.csproj -c Release
dotnet build Delve/src/Delve.Plugin/Delve.Plugin.csproj -c Release
dotnet build Edge/src/Edge.Plugin/Edge.Plugin.csproj -c Release
dotnet build Ember/src/Ember.Plugin/Ember.Plugin.csproj -c Release
dotnet build Hex/src/Hex.Plugin/Hex.Plugin.csproj -c Release
dotnet build Inscribe/src/Inscribe.Plugin/Inscribe.Plugin.csproj -c Release
dotnet build Keel/src/Keel.Plugin/Keel.Plugin.csproj -c Release
dotnet build Kindle/src/Kindle.Plugin/Kindle.Plugin.csproj -c Release
dotnet build Larder/src/Larder.Plugin/Larder.Plugin.csproj -c Release
dotnet build Magpie/src/Magpie.Plugin/Magpie.Plugin.csproj -c Release
dotnet build Mantle/src/Mantle.Plugin/Mantle.Plugin.csproj -c Release
dotnet build Saga/src/Saga.Plugin/Saga.Plugin.csproj -c Release
dotnet build Shepherd/src/Shepherd.Plugin/Shepherd.Plugin.csproj -c Release
dotnet build Tempest/src/Tempest.Plugin/Tempest.Plugin.csproj -c Release
dotnet build Thing/src/Thing.Plugin/Thing.Plugin.csproj -c Release
dotnet build Threshold/src/Threshold.Plugin/Threshold.Plugin.csproj -c Release
dotnet build Timber/src/Timber.Plugin/Timber.Plugin.csproj -c Release
dotnet build Wayfarer/src/Wayfarer.Plugin/Wayfarer.Plugin.csproj -c Release
dotnet build Wildblood/src/Wildblood.Plugin/Wildblood.Plugin.csproj -c Release
dotnet build Wildroot/src/Wildroot.Plugin/Wildroot.Plugin.csproj -c Release
```

Outputs are written to `<folder>/src/<project>/bin/Release/netstandard2.1/`. Code and embedded resources match the
published assemblies; the informational version (it names the commit that last changed the build inputs) and
assembly metadata such as the module ID differ.

## Line endings

Burrow, Mantle, Crucible, Wildroot embed text resources with Windows (CRLF) line endings. Their `.gitattributes` checks text
out with CRLF on every platform. The other folders store every file byte for byte.

## Bestiary creature art bundles

`Bestiary.dll` embeds the sixteen bundles under `Bestiary/build/bundles`; the build refuses bundles that differ from
`Bestiary/assets/bestiary/records/bundles.json`. Their editable inputs are included: the pinned AssetWorkshop subset in
`Bestiary/build/workshop`, curves and kits in `Bestiary/build/assets`, and the export and Unity adapters in
`Bestiary/tools/assets`. Regenerating them needs Blender 5.2.1, Unity 6000.0.75f1 (Windows and Linux build support) and
Python. From the `Bestiary` folder:

```text
python tools/assets/workshop.py blender
python tools/assets/workshop.py hashes
python tools/assets/unity_build.py stage
python tools/assets/unity_build.py bundle --platforms windows,linux
```

Set `BESTIARY_BLENDER`, `BESTIARY_UNITY` and `BESTIARY_PYTHON` when the tools are not on the default paths.
Regenerated bundles can differ in bytes from the ledgered ones.

## Original item art

Bestiary, Inscribe, Larder, Mantle and Shepherd embed original item bundles from `assets/original-items/`. Their editable
source and rebuild steps are in each folder's `assets/original-items/source/` (`SOURCE-REBUILD.md`).
