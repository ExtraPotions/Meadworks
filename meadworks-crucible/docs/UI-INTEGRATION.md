# MeadWorks UI integration

Use the existing MeadWorks theme. Crucible.UI supplies shared presentation and interaction; each product owns its gameplay, permissions, data and actions. Keep the graphical layer separate from code loaded by headless hosts.

## Existing product registration

Pass the product's existing `ProductRegistration` to typed notices and dialogs. Do not register another product identity for a window. Check that the registration is active and the graphical UI is available before requesting presentation; retain the existing native or headless fallback when they are absent.

`KitNotices.Show(source, new KitNotice(text, KitNoticeKind.Center))` routes local feedback through an optional provider and native fallback. Preserve the original amount, icon, localization and journal policy. Actor-directed `Player.Message` calls can target another player or use native RPCs: do not replace those with local feedback without preserving the recipient.

`KitDialogs.Confirm(source, title, text, accepted, cancelled)` owns a single response. Its callback runs after the owned presentation closes; do not pop the native popup again. Revalidate mutable game state inside the accepted action. Use `localizeText: false` for already-localized literal bodies where the original popup did so. Keep the returned request when an operation needs explicit cancellation; dispose it when its owner becomes invalid. Disposal closes presentation without accepting a gameplay action.

## Controls and windows

Use the existing `KitWindow`, shared controls and `GameButtons` factory. A required native clone may retain its geometry, navigation and callbacks and bind `KitForged.Button` for the shared look. Use toggle switches and rounded rectangles. Do not replace native item or foreign UI hosts solely to change their appearance.

Ordinary windows retain gameplay-input and HUD-blocking behavior. An intentional HUD editor can keep HUD content visible while it owns input. Do not release gameplay input merely to make a panel visible.

## HUD identity and layout

Use the `KitHudPanel.Create` overload with the product's stable GUID and a stable panel ID. Keep that ID across updates; it identifies saved local layout preferences. Product code continues supplying automatic placement, visibility, scale and opacity. User layout overrides apply through the shared layout service. Dispose the panel when its owner ends. Embedded map, inventory, combat-effect and native hint-row content keeps its appropriate native host rather than becoming a freely movable HUD panel.

## Settings

Use `MeadworksConfig` with the existing intended local/server/cheat scope. Add a `ConfigDependency` tag through a `ConfigDescription` only when a verified runtime parent gate exists. Its predicate must read current effective values. Keep the original acceptable values and metadata. Dependency presentation does not replace authority or runtime validation and must not reset a disabled child's saved value.

Accepted menu edits must use the established durable amendment path. Preserve personal values during updates, and never write a remote server overlay into the client's saved settings. Verify actual disk state, a cold game restart and an update for changes to persistence behavior.

`MeadworksMenuSettings.Available/Open` accept a dotted full plugin GUID unchanged, including third-party GUIDs; a bare alias such as `hex` resolves to `com.meadworks.hex`. Missing or whitespace-containing IDs return false before calling the optional provider. A provider can decline or fail; callers retain their existing fallback. This route does not add a plugin to the MeadWorks roster. Dispose the registration lease when the provider ends.

## Live setting labels and help

A managed owner can add an optional typed `ConfigPresentation<T>` tag to the existing `ConfigDescription`. Almanac resolves its label, section caption and help when rebuilding displayed rows and refreshes on game-language changes. Keep the section/key identifiers stable. Use your existing translation catalog; missing translations should return the original text.

```csharp
var detail = MeadworksConfig.Bind(config, "Display", "Detail", 2,
    new ConfigDescription("Amount of detail.", new AcceptableValueRange<int>(0, 5),
        new ConfigPresentation<int>(ProductGuid, (entry, language) =>
            new ConfigPresentationText(
                label: ExistingCatalog(language, "Detail"),
                section: ExistingCatalog(language, "Display"),
                help: ExistingCatalog(language, "Amount of detail.")))));
```

`ExistingCatalog` is the owning mod's translator, not a new Crucible API. Keep callbacks free of setting writes and gameplay actions. They receive the exact typed entry and current language; their results contain presentation text only. The entry must still be bound and registered to the exact product GUID. Unregistered, mismatched, duplicate or failing providers retain conventional metadata/raw text. Blank result fields retain their individual fallback. Existing ranges, visibility, owner, declared scope and write admission remain authoritative.

A section keeps its stable grouping and collapse identity. One consistent supplied caption can label that group; conflicting captions fall back to the original heading. Almanac's own description-mode setting uses this hook with Almanac's existing catalog. The hook does not supply translations for other mods or prove catalog quality.

## Tooltips

Use ordinary shared tooltips for plain text. Use `KitTipDocument` and `KitStructuredTip` for supplied costs, statuses and comparisons. Supply localized display facts from the owning calculation; do not parse formatted native strings or duplicate gameplay formulas. Preserve the native/foreign body as the fallback. Clear old structured state when a reused control changes back to a plain tooltip or loses its item.

## Verification

Build affected projects in dependency order. Check real caller behavior, provider decline/failure, cancellation, teardown, relevant authority and persistence boundaries. Review normal-game captures for visual claims. Portable models and replayed UI callbacks do not establish physical controller behavior, a cold game restart or complete product migration.
