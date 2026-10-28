# Source availability notice

Product author: expDARE.

Inscribe.Core.dll and Inscribe.dll contain logic adapted from EliteCrafting 0.7.0 and ValheimModLibs (pinned at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, GPL-3.0-or-later) Versions before 0.2.0 also contained logic adapted from ItemEnhancements 1.0.2 (pinned at 02201869cbb3e1356f4b1931d01a778af3c07532, GPL-3.0); 0.2.0 removed it with the enhancement system. Four approved EliteCrafting rune/lootglow platform bundles and their included sprites retain that source's license. Package/prototype icons and models, ItemEnhancements icon and replaced configuration/input code remain excluded. Gameplay follows EliteCrafting 0.10.0 (release d3b57606cb24775d61b7bda244b22eb89e7e7717, GPL-3.0-or-later; the 2026-10-10 parity port: the five-rune rework, 8-tier ladders, sockets, the Dvergr Chisel, the eleven gems, boss gems, salvage runes and the Rune Table rules); files carrying it name that release beside or instead of 0.7.0. The 0.10.0 gem, rune-table and table-effect bundles and the essence images are not adopted: the chisel and gems wear tinted game items and the socket and seal marks are drawn in code. EliteCrafting 0.8.0 (dab238855e21e6623e06217b686492e66a6c2bc1) and 0.9.1 (9da28eb58adbe065be1e4baef779a448acd38e0f) supply converter fixtures only.

YamlDotNet 13.7.1 (MIT) is internalized into Inscribe.dll to prevent a public parser dependency collision with other products. Its complete upstream notice is included in THIRD_PARTY_NOTICES/YamlDotNet-MIT.txt.

The Corresponding Source for this version is at https://github.com/ExtraPotions/Meadworks/tree/main/Inscribe. No copyright transfer is inferred. Original upstream copyright and license notices remain intact.

The tall-tooltip feature adopts Display/Tooltips/TooltipScroll.cs from the existing EliteCrafting 0.7.0 pin. It keeps the native tooltip panel, fonts and border, with a clipped body and scroll-position bar only when needed. Inscribe adds display/headless admission, existing-scroll-owner refusal, scale/topic fit invalidation and current-owned-tooltip cleanup. No additional art or source family is imported.

Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.2.9/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory. No new Git commit or broader native acceptance is asserted.
