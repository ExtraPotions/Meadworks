# License scope

Product author: expDARE. ExtraPotions Meadworks.

## Original code

All original Almanac code, including Almanac.Core, is GPL-3.0-or-later. Copyright (C) 2026 expDARE. Complete terms are in licenses/GPL-3.0.txt; see LICENSE-CODE.md.

## Original artwork and documentation

Original documentation and artwork use CC BY-NC-SA 4.0, with complete terms in licenses/CC-BY-NC-SA-4.0.txt; see LICENSE-ASSETS.md.

## Conventions read by name, no adopted source

- BepInEx.ConfigurationManager (LGPL-3.0): the `ConfigurationManagerAttributes` tag convention (member names and meaning) is read by reflection by name. No source is copied.
- Vapok BepInEx_ConfigDrawers 1.1.1 (MIT): behavior and native bind metadata reference only; its toggle/open state is read at run time for coexistence, and its real preference file can be imported without modifying the source. No manager source is copied.
- Jotunn, ServerSync (MIT-0), ConditionalConfigSync and PackPanel Charter: lock markers are read by type and member name at run time. No source is copied.

## Separately installed dependency

The actual shared window controls and services come from the required Crucible package rather than copied manager code. Crucible and Crucible.UI carry their own corresponding source and applicable legal notices in their separate package. Almanac distributes only its own two assemblies and original GPL/CC license files; Jotunn, shared framework and game binaries are excluded.
