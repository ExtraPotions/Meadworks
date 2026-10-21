# Code license

Copyright (C) 2026 expDARE (ExtraPotions Meadworks).

Original Edge source code (including Edge.Core) is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

It is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

SPDX-License-Identifier: GPL-3.0-or-later (original files)

The complete, unmodified license text is included in `licenses/GPL-3.0.txt`. Official source: https://www.gnu.org/licenses/gpl-3.0.txt

Edge contains adapted logic and an internalized YAML dependency:

- SecondaryAttacks 1.2.17 (GPL-3.0, sighsorry1029). Its repository carries the GPLv3 text and no "or later" statement, and its source files carry no headers, so files adapted from it are GPL-3.0-only (SPDX `GPL-3.0-only` in their headers). Retained notice: `THIRD_PARTY_NOTICES/SecondaryAttacks-GPL-3.0.txt`.

- EliteCreaturesReborn 3.21.0 (GPL-3.0-or-later, Gerald Glasgow) at 9da28eb58adbe065be1e4baef779a448acd38e0f: the loaded-crossbow files `src/Edge.Plugin/Equipment/LoadedWeapons.cs` and `LoadedWeaponPatch.cs` keep SPDX `GPL-3.0-or-later`. Retained notice: `THIRD_PARTY_NOTICES/EliteCreaturesReborn-GPL-3.0-or-later.txt`.

Because Edge.Core contains GPL-3.0-only files, the compiled Edge.Core and Edge assemblies as a whole are distributed under GPL-3.0 (version 3). Original files keep their GPL-3.0-or-later grant. This grant does not replace or modify the upstream notice.

Slope assist (behavior of Digitalroot's Slope Combat Assistance, AGPL-3.0) and swim re-equip (behavior of KeepWeaponsAfterSwim, no license) are independent implementations from public behavior descriptions; no code of either is contained in Edge.

Crucible, Jotunn, BepInEx and game assemblies retain their own licenses and are not bundled or relicensed by Edge.

Additional adoption: SecondaryAttacks 1.2.17 raw YAML schemas, normalized models, weapon and summon normalizers, parser and compiled snapshot, with source rules retained and Edge lifecycle/configuration seams. Embedded YAML defaults retain pinned bytes. The map/scalar layer merge subset is adapted from EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, GPL-3.0-or-later; original Gerald Glasgow copyright retained and license text in THIRD_PARTY_NOTICES/EliteCrafting-GPL-3.0-or-later.txt. YamlDotNet 16.3.0 (MIT, package repository commit ae480660f4fb26f3eb0b41c1d1fcf21c0e9d9e73) is merged as private implementation inside Edge.dll; retained notice THIRD_PARTY_NOTICES/YamlDotNet-MIT.txt. No upstream ServerSync is incorporated.

The build-generated BepInEx.AssemblyPublicizer.MSBuild 0.4.3 attribute source shim retains its MIT notice at THIRD_PARTY_NOTICES/BepInEx.AssemblyPublicizer-MIT.txt.
