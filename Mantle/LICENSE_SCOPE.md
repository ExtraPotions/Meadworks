# License scope

Product author: expDARE. ExtraPotions Meadworks.

## Original code

All original Mantle code, including its authored code adaptations, is GPL-3.0-or-later. Copyright (C) 2026 expDARE. Complete terms are in licenses/GPL-3.0.txt; see LICENSE-CODE.md.

## Artwork

Original Mantle artwork and documentation use CC BY-NC-SA 4.0; see LICENSE-ASSETS.md and licenses/CC-BY-NC-SA-4.0.txt. The approved Meadworks icon is included at icon.png.

## Adopted sources and preserved notices

Code adoption is pinned to ValheimMods31d8ee35ac4ccfd14c83aa930e4b602ac4dfa230, PackPanel0.7.0, except four files at ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f, PackPanel 0.14.0 (Hotkey.cs, KeptOnDeath.cs, KeptGroups.cs, ContainerBeside.cs; same GPL-3.0-or-later license). The upstream GPL-3.0-or-later notice is preserved byte-for-byte. The 14 PackPanel 0.12.0 slot icons (pin 01821b85) and the EliteCreaturesReborn world-tier globe are adopted byte-identical under the GPL-3.0-or-later notice, not under LICENSE-ASSETS.md. PackPanel's AI-generated Timber images are not shipped. No other upstream artwork, bundles, icons or textures were copied. The backpack YAML catalog is retained as functional configuration.

Development backpack visuals reference and clone existing native Valheim assets only at runtime; those assets are not included in this repository and are not relicensed by Mantle. Native game and third-party binary build references are not adopted or redistributed by this source unit. User authorization to adapt source does not substitute for the upstream license.

### Reference boundaries

HoverCompare-derived comparison code was removed in 0.1.9; no HoverCompare code, notice or artwork remains. No icon, texture or bundle is copied into Mantle. Native icons/fonts/sprites are accessed at runtime only. The former MovableInventoryWindows-derived panel movement code and its retained notice were removed; that reference is no longer part of the runtime product.

ILSpyCmd 9.1.0.7988 is a development tool, not a runtime dependency, and is not distributed with Mantle. Its package declares MIT and source revision 03b7444943e720b3134d296c0c8dd3876f8ea4ce; the full exact-revision LICENSE was reviewed.

The ring and belt slot code and registry are independently implemented GPL-3.0-or-later Mantle code (see SOURCE_NOTICE.md); the adopted purse, key ring and tacklebox code keeps its PackPanel notices.
