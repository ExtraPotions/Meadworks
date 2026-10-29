# Source availability notice

Product author: expDARE.

Barter.Core.dll and Barter.dll contain original Meadworks code (GPL-3.0-or-later) and code adapted from TradersExtended 2.0.5 by shudnal at `2d5dd879ead70f809f53025d6b28feb3dee7b605`, which the author released into the public domain under the Unlicense (`licenses/Unlicense-TradersExtended.txt`). YamlDotNet.dll 13.7.1 is a packaged runtime dependency under its retained MIT notice.

What was adopted, in three claims that are kept apart in `docs/source-provenance.json` (every file, its upstream files, SHA256 values and a note):

- Adapted code: lot and price arithmetic (`TradeAmounts.cs`, with the factor-to-decimal step changed), trader name normalization, list file name parsing, the list entry model, the CSV reader, the assembly of lists per trader, availability rules, currency rule, price curve, purse refill arithmetic, the sell price table and the sell offer grouping. These are in Barter.Core and are tested without the game.
- Behaviour-based new code: the Harmony integration points, the list-to-trade-item conversion, purse storage, purchase and sale flow, and the purse card and sell panel. They were written with the upstream implementation open as the reference, so their structure and some expressions follow it; no file was copied and the UI is new Crucible.UI kit code.
- Default values only: the purse and list settings follow the upstream defaults (2000 to 6000 balance, +1000 a day, discount 0.7, markup 1.5, discovery on).

Original Meadworks code, not adopted: the settings catalogue and scopes, server-authoritative list synchronization and hot reload through Crucible, the trade book snapshot with revision, the YAML and JSON list reader, the side table that carries quality and currency, step-aside, localization and every test.

`python tools/adopt-barter.py --check` verifies the destination hashes, the credit in each header, the upstream hashes against the pinned reference (when the workspace is present) and that the tree holds no image, sound, model, DLL or archive apart from the candidate icon.

Local delivery must include the matching source ZIP alongside the mod ZIP. `scripts/package.py` requires a clean source tree and packages the exact current Git HEAD source. No archives or download link exist yet; packaging waits for the in-game run. No publication or copyright transfer is inferred. Original upstream notices remain intact.

The build-only BepInEx.AssemblyPublicizer 0.4.3 dependency keeps its MIT notice in THIRD_PARTY_NOTICES/BepInEx.AssemblyPublicizer-MIT.txt; its binary is not shipped. The approved package icon is original MeadWorks artwork; see `docs/art-provenance.md`.


## Corresponding source for Barter 0.1.6

This source archive contains the source snapshot for the paired runtime assemblies. SOURCE-SNAPSHOT.json records exact member identities; BUILD-RECIPES records the frozen source-member map used for the successful builds, runtime hashes, normalized build commands and scoped test-evidence hashes. Existing third-party notices and accepted art are retained. Machine paths and raw private test/authentication records are omitted.

Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.1.8/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory. No new Git commit or broader native acceptance is asserted.

Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.1.8/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory. No broader native acceptance is asserted.

Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.1.8/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory. No broader native acceptance is asserted.

Corresponding source for this build is identified by SOURCE-SNAPSHOT.json and BUILD-RECIPES/code-release-0.1.8/BUILD.json. Historical compiler-reference source and DLL supplements remain part of the release source inventory. No broader native acceptance is asserted.
