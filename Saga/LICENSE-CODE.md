# Code license

Copyright (C) 2026 expDARE (ExtraPotions Meadworks).

Original Saga source code (including Saga.Core) is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

It is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

SPDX-License-Identifier: GPL-3.0-or-later (original files)

The complete, unmodified license text is included in `licenses/GPL-3.0.txt`. Official source: https://www.gnu.org/licenses/gpl-3.0.txt

Saga contains logic adapted from these sources:

- GrindstoneSkills 0.14.1 and ValheimModLibs (ValheimMods monorepo, Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later). Adapted files keep the upstream notice and SPDX `GPL-3.0-or-later`. Retained notices: `THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt`, `THIRD_PARTY_NOTICES/ValheimModLibs-GPL-3.0-or-later.txt`.
- SecondaryAttacks 1.2.17 (GPL-3.0, sighsorry1029). Its repository carries the GPLv3 text and no "or later" statement, and its source files carry no headers, so files adapted from it are GPL-3.0-only (SPDX `GPL-3.0-only` in their headers). Retained notice: `THIRD_PARTY_NOTICES/SecondaryAttacks-GPL-3.0.txt`.

Because Saga.Core contains a GPL-3.0-only file, the compiled Saga.Core and Saga assemblies as a whole are distributed under GPL-3.0 (version 3). Original files keep their GPL-3.0-or-later grant. This grant does not replace or modify the upstream notices.

Powers (behaviour of ProgressivePowers, no licence) and the sneak speed default (behaviour of SNEAKer, no licence) are independent implementations from public behaviour descriptions and the users' own configuration files; no code, text or image of either is contained in Saga.

Crucible, Jotunn, BepInEx and game assemblies retain their own licenses and are not bundled or relicensed by Saga.

Saga.dll internalizes YamlDotNet 16.3.0 under its MIT license. Its complete upstream notice is retained in `THIRD_PARTY_NOTICES/YamlDotNet-MIT.txt`. The build uses ILRepack 2.0.46 as a private build dependency; it is not a runtime payload.
