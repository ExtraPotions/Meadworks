# Source availability notice

Product author: expDARE.

Burrow.dll contains adopted storage source and adaptations covered by the retained GPL-3.0-or-later notice in THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt. The pinned upstream is https://github.com/geraldjglasgow/ValheimMods at 31d8ee35ac4ccfd14c83aa930e4b602ac4dfa230. Independent code and other asset/library licenses retain their scopes in LICENSE_SCOPE.md; this notice does not grant additional rights or replace any license.

The Corresponding Source for this version is at https://github.com/ExtraPotions/Meadworks/tree/main/Burrow. No copyright transfer is inferred.

FastItemTransfer was a behavior reference only; its right-click transfer was removed on 2026-10-19 and no FastItemTransfer source or artwork is adopted. ChestSizeConfig and ChestPreview carry no license: only their behavior is followed (Burrow.Core ContainerSizeSpec and ChestPreviewRules, Parity/ChestPreview.cs); none of their code or art is used. Original upstream copyright/license notices remain intact.

The October 16, 2026 container-list update adapts the retained ContainerTemplate and SceneReady files with original append-only discovery behavior. It follows the public OpenKeep 5.0.0 changelog feature description without importing newer source. Existing upstream revisions, blob hashes and notices are preserved. Discovery writes commented examples only, validates the current YAML set and publishes without sending companion files through the shared disk writer.

The October 16 Top Up correction adapts TopUp.cs and ChestBatch.cs to carry an eligible ordinary destination through the existing writer. Upstream paths, revisions, blob hashes and license notices are unchanged. Mantle and the shared transfer protocol were not modified.

The October 16 tall-container wheel correction adapts only GridScrolls and its wheel guard from OpenKeep Store/Cycling.cs at retained revision 9da28eb58adbe065be1e4baef779a448acd38e0f. The rest of Burrow Stow/Cycling.cs retains its existing adaptation. Existing GPL notices remain applicable.

The companion tooltip-wheel guard adapts the retained newer Cycling tooltip ownership behavior and adds original generic ScrollRect overflow detection. It recognizes only active current tooltip scroll owners: overflowing enabled native ScrollRects or the actual visible Inscribe/EliteCrafting manual bars. No product assembly reference or new cross-product contract is added.

## Distribution version

Package 0.3.26 contains the accepted 0.3.25 assemblies. This package-only version increment does not change the compiled assembly version or runtime source.
