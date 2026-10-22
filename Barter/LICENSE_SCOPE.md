# License scope

Product author: expDARE. ExtraPotions Meadworks.

## Original code

All original Barter code, including Barter.Core and Barter.Plugin, is GPL-3.0-or-later. Copyright (C) 2026 expDARE. Complete terms are in licenses/GPL-3.0.txt; see LICENSE-CODE.md.

## Original documentation and artwork

Original documentation uses CC BY-NC-SA 4.0, with complete terms in licenses/CC-BY-NC-SA-4.0.txt; see LICENSE-ASSETS.md. `icon.png` is an icon drawn for Barter (docs/art-provenance.md), approved by the user 2026-10-10. No upstream image, model or sound is included; the TradersExtended screenshots and package images are not adopted.

## Adopted source

| Source | Scope and terms | Retained notice |
| --- | --- | --- |
| TradersExtended 2.0.5 by shudnal, commit `2d5dd879ead70f809f53025d6b28feb3dee7b605` | Trade list model and file names, CSV reading, availability rules, currency rule, price curve, purse refill arithmetic, lot arithmetic and sell offer grouping (adapted code); the integration points, purchase and sale flow, purse storage and the shape of the UI (new code written with the upstream as reference). Released into the public domain under the Unlicense. | `licenses/Unlicense-TradersExtended.txt` |

Public-domain source can be combined with GPL-3.0-or-later code, so Barter as a whole is distributed under GPL-3.0-or-later; the public-domain status of the upstream parts is not changed by that. Files that adopt upstream code or follow its behaviour say so in their first lines and are listed, with the upstream files they come from and their SHA256, in `docs/source-provenance.json`. The distinction between adapted code, behaviour-based new code and default values only is made there file by file. Verify with `python tools/adopt-barter.py --check`.

## Not adopted

The upstream configuration editor, its own configuration synchronization (Conditional Config Sync), its IMGUI and UI construction code, the buyback, repair, tooltip price and coin item modules, the EpicLoot and extra-slots integrations and the per-trader settings files are not part of Barter 0.1.0. Server authority, settings, text and panels come from Crucible, Crucible.UI and Meadworks localization.

## Third-party dependencies

Crucible, Jotunn, BepInEx and the game assemblies keep their own licenses and are not bundled or relicensed by Barter. YamlDotNet 13.7.1 (MIT) is packaged unmodified with its notice in `THIRD_PARTY_NOTICES/YamlDotNet-MIT.txt`. The build-time BepInEx.AssemblyPublicizer 0.4.3 notice is in `THIRD_PARTY_NOTICES/BepInEx.AssemblyPublicizer-MIT.txt`; its binary is not shipped. No remote publication has occurred.
