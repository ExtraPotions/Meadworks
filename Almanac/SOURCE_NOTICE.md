# Source availability notice

Product author: expDARE. Almanac requires Crucible, which includes Crucible.UI.

Almanac contains no copied external config-manager implementation. It reads BepInEx/ConfigurationManager, Jotunn, ServerSync, ConditionalConfigSync and Charter conventions through actual native APIs and member metadata. ConfigDrawers 1.1.1 is a behavior and preference-bind reference; its source is not incorporated.

The runtime reuses the separately installed Meadworks Crucible services and Crucible.UI kit, whose corresponding source and legal notices belong to the separate required Crucible package. The Almanac package does not duplicate those assemblies.

World rows are original expDARE integration over the existing Crucible Snapshot/stamped metadata API and existing kit controls. They add no copied external manager implementation or new transport.

The Corresponding Source for this version is at https://github.com/ExtraPotions/Meadworks/tree/main/Almanac. No copyright transfer is inferred.

The native uGUI drawer host (src/Almanac.Plugin/UI/AlmanacUguiDrawer.cs) follows the behavior of BepInEx.ConfigDrawers 1.1.1 by Vapok (MIT): the CustomUguiDrawer member names, the delegate parameter rules and the scope member names. No ConfigDrawers code is copied.

The text editor coloring, live JSON/YAML checks (src/Almanac.Core/Editors/TextSyntax.cs) and the table editor (src/Almanac.Core/Editors/TableText.cs) follow the described behavior of BepInEx.ConfigDrawers 1.1.3 (code editor and data tables); no ConfigDrawers code is copied.


## Corresponding source for Almanac 0.1.23

This source archive contains the source snapshot for the paired runtime assemblies. SOURCE-SNAPSHOT.json records exact member identities; BUILD-RECIPES records the frozen source-member map used for the successful builds, runtime hashes, normalized build commands and scoped test-evidence hashes. Existing third-party notices and accepted art are retained. Machine paths and raw private test/authentication records are omitted.


Successor corresponding source: this package uses the exact member map and build receipts in SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.1.29/BUILD.json. The matching accepted provider-source supplements remain part of the release source inventory. No new Git commit is asserted.

Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.1.29/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory. No broader native acceptance is asserted.

Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.1.29/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory. No broader native acceptance is asserted.

Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.1.29/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory. No broader native acceptance is asserted.
