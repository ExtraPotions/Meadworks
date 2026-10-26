# Source notice

Reference source: https://github.com/geraldjglasgow/ValheimMods, commit31d8ee35ac4ccfd14c83aa930e4b602ac4dfa230, PackPanel0.7.0 and its required ValheimModLibs helpers. Four files come from the later pin 9da28eb58adbe065be1e4baef779a448acd38e0f (PackPanel 0.14.0): ValheimModLibs/Hotkeys/Hotkey.cs, PackPanel/src/Slots/KeptOnDeath.cs, PackPanel/src/Slots/KeptGroups.cs and PackPanel/src/Panels/ContainerBeside.cs, under the same GPL-3.0-or-later notice. On 2026-10-10 further PackPanel 0.15.0 files were added from pin 3bb778330f23645ccddb42fbc0c6ffab72c842b6 (Instant Equip, Auto Equip, the in-place upgrade, the backpack as equipment, the EliteCrafting integration with the ValheimModLibs EliteCraftingLink library, bound to Inscribe, and the EliteEquipment Feet slot), under the same GPL-3.0-or-later notice. Original legal notices remain in retained source and the exact upstream GPL license is in THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.

Mantle product metadata uses expDARE and com.meadworks.mantle. Namespace, configuration/save keys and embedded helper identities are renamed deliberately; the original milkyteam.packpanel GUID occurs only as an explicit incompatibility declaration. Renaming is not a claim of original ownership. Third-party artwork is limited to the PackPanel 0.12.0 inventory art below.

## Additional references

The native panel move/scale adaptation from MovableInventoryWindows0.4.1 was removed in the panel-movement removal work. No code from that reference or its license notice remains in the runtime product.


## PackPanel 0.12.0 inventory art (2026-10-07)

The 14 slot icons Mantle's adopted `SkinArt` still reads (formerly 21 images with the Brown panel, cell, buttons, key ring panel and line, 14 slot icons) are copied byte-identical from ValheimMods commit 01821b8557e4a77e37f5433c885b1f122cae8249 (`PackPanel/PackPanel/assets`) into `src/Mantle.Plugin/Adopted/assets` and embedded as `Mantle.assets.<name>.png`. These are the outputs of PackPanel's own drawing scripts, under PackPanel's GPL-3.0-or-later notice, which is byte-identical to the retained THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt; they are not original Mantle artwork and are not covered by LICENSE-ASSETS.md. PackPanel's two AI-generated Timber images are no longer shipped: `SkinArt` takes the Timber wallpaper and button from the Crucible.UI kit (`KitArt`), where Meadworks' own CC0-built images live once (Crucible's LICENSE-ASSETS.md). Since 0.1.9 the Elite Creatures Reborn world tier globe (`EliteCreaturesReborn/EliteCreaturesReborn/assets/globe.png`, same commit and GPL notice) is copied the same way for the HUD world level square.

## HoverCompare removed (0.1.9, 2026-10-12)

The item comparison cards and every file derived from HoverCompare 0.5.0 (its adopted code, overrides, derivation ledger and MIT notice) were removed at the user's request. No HoverCompare-derived code remains in the runtime product.

## Ring and belt slots (2026-10-09)

The Ring and Belt slot kinds, the accessory registry (`src/Mantle.Core/Accessories`, `src/Mantle.Plugin/Accessories`, `Mantle.Accessories.yml`) and their tests are independently implemented by expDARE: PackPanel 0.12.0 only lists "create ring slot" and "create belt item" as plans and ships no code for them, and no other reference was read for them. Where the work touches adopted PackPanel files (slot kinds and rules, layout, panel layout, words, settings, key-on-death groups), the change is made as a modification of the adopted file, which keeps its PackPanel notice. The coin purse, key ring and tacklebox are adopted PackPanel 0.7.0 code that had been switched off by overrides; enabling them adds no new source. The tackleboxes and the new rings and belts use native development visuals cloned at runtime, as the backpacks do; `Mantle.Tackleboxes.yml` is the adopted upstream config.


## Corresponding source for Mantle 0.1.28

This source archive contains the source snapshot for the paired runtime assemblies. SOURCE-SNAPSHOT.json records exact member identities; BUILD-RECIPES records the frozen source-member map used for the successful builds, runtime hashes, normalized build commands and scoped test-evidence hashes. Existing third-party notices and accepted art are retained. Machine paths and raw private test/authentication records are omitted.


Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.1.29/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory; a newer runtime provider does not replace those inputs. No new Git commit or broader native acceptance is asserted.
