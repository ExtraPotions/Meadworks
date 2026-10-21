# Crucible public API

This page describes the Crucible surface that other mods can build against. It covers only behavior the code implements. Statements the code does not settle are marked **Unverified**. For the network request and peer-command contracts, see [multiplayer-consumer-api.md](multiplayer-consumer-api.md). For UI integration rules, see [UI-INTEGRATION.md](UI-INTEGRATION.md).

## Assemblies and plugins

| Assembly | Plugin GUID | Contents | Loaded on dedicated servers |
| --- | --- | --- | --- |
| `Crucible.Core.dll` | none | Registries, metadata, authority and config policies, build fingerprint types. No Unity UI references. | yes |
| `Crucible.Contracts.dll` | none | Shared capability contract types used between first-party products. | yes |
| `Crucible.dll` (Crucible.Plugin) | `com.meadworks.crucible` | `Plugin.Services`, `MeadworksConfig`, `MeadworksAuthority`, `MeadworksMischief`, `MeadworksLog`, the native transports. | yes |
| `Crucible.UI.dll` | `com.meadworks.crucible.ui` | The Kit: notices, dialogs, tooltips, HUD panels, windows, appearance. Depends on Crucible one way. | loaded, but idle on a headless process (binds, patches and creates nothing) |

Reference these assemblies with `Private="false"`. Do not ship copies of them. Meadworks products import `RuntimeReferences.props` for the BepInEx, Jotunn, Harmony, Unity and game references.

## What is public and what is stable

Every `public` type in these assemblies is reachable, but only some of it is intended for consumers:

- **Consumer API (covered on this page):** `Plugin.Services` / `Plugin.IsReady`, `ProductRegistry.Register`, `ProductMetadata`, `ProductRegistration`, `CapabilityRegistry` (`TryResolve`, `ResolveEach`, `Providers`, `Any`, `Generation`), `CapabilityLink<T>`, `FeatureLink`, `OperationRegistry.Register` with `OperationDescriptor`, `IOperationCodec<,>`, `OperationExecution<T>`, `MeadworksConfig`, `MeadworksAuthority`, `MeadworksMischief`, `MeadworksLog`, `BuildEntry` / `BuildComparison`, and in Crucible.UI `KitNotices`, `KitDialogs`, `KitTooltip`, `KitStructuredTip`, `KitHudPanel`, `KitAppearance`, `KitLook`, `KitWindow`.
- **Public for Crucible's own wiring:** for example `AuthorityGate`, `ConfigEditPolicy`, `ConfigAmendment`, `ServerPresenceTimer`, `ScopedValue<T>`, `KitHudLayout.Initialize/Tick/Shutdown` and `KitDialogs.Initialize/Tick/Shutdown`. The Crucible plugins already call the lifecycle methods. Consumers should not call them.

There is no separate stability annotation in the code. The only machine-checked compatibility numbers are the registry API version and capability versions described below. Everything else follows the Crucible package version. **Unverified:** no written policy says which public members may change in a minor release. Treat anything not listed as consumer API as internal.

## Versions

### Registry API (`ProductRegistry.ApiVersion`, currently 0.1)

`ProductMetadata` carries `MinimumApi` and `MaximumApi`. `ProductRegistry.Register` throws `NotSupportedException` when `ApiVersion` falls outside `[MinimumApi, MaximumApi]` (inclusive, compared as `System.Version`). The constructor rejects a reversed interval. Every current product declares `0.1`–`0.1`.

### Capability versions

Each capability is published with a `major.minor` version. `TryResolve<T>(id, minimumVersion, out service)` matches a provider only when all of these hold:

- the id is equal,
- the provider's **major equals** `minimumVersion.Major`,
- the provider's version is **at or above** `minimumVersion`,
- the registered contract type is **exactly** `typeof(T)` (no assignability check).

`TryResolve` returns false when no provider matches **or when more than one does**. Use `ResolveEach<T>` for capabilities that are meant to have one provider per product. It returns every match ordered by owner id. A newer major is invisible to an older consumer, and the reverse is also true. To evolve a contract compatibly, raise the minor version and keep the same interface type. A breaking change needs a new major, and providers that want to serve old consumers must register both.

Because matching uses the exact `Type`, two mods can share a capability only when both reference the assembly that declares the contract interface. First-party contracts live in `Crucible.Contracts`.

### Other versioned contracts

| Contract | Id | Version |
| --- | --- | --- |
| Notice provider | `KitNotices.CapabilityId` = `meadworks.ui.notices` | `KitNotices.ContractVersion` 1.0 |
| Dialog provider | `KitDialogs.CapabilityId` = `meadworks.ui.dialogs` | `KitDialogs.ContractVersion` 1.0 |
| Build hashes | `BuildComparison.Capability` = `meadworks.crucible.build_hashes` | 1.0 |
| Remote operations | `OperationDescriptor.Protocol` | must equal the transport protocol 1.0, otherwise `Register` throws |

## Identifiers

- Product ids, capability ids and operation ids must match `^[a-z][a-z0-9]*(\.[a-z][a-z0-9_]*)+$`. That means lowercase letters, at least two dotted segments, at most 128 characters, and digits and underscores only after a segment's first letter. Hyphens and uppercase letters are invalid. A malformed id throws `ArgumentException` at `AddCapability`, `CapabilityLink` construction, `OperationDescriptor` and `ProductMetadata`. `TryResolve` is an optional lookup: a malformed id or version returns false.
- A capability id starts with `meadworks.` or with the owning product's own id followed by a dot (a product `com.example.mod` may publish `com.example.mod.forecast`). Any other prefix throws at `AddCapability`. Operation ids must start with `meadworks.`: operations are part of the network protocol.
- `MeadworksConfig` records each setting's owner as the config file name without extension. For a plugin's default `Config`, that name is the plugin GUID. Pass the same string as `productId` to `Effective`, `Amend` and `MeadworksAuthority`, and register `ProductMetadata` under that GUID so the authority gate finds its peer class. A plugin whose GUID contains uppercase letters cannot satisfy both rules.

## Registration lifecycle

```csharp
if (!CruciblePlugin.IsReady) { /* Crucible failed to start: stay off */ return; }
registration = CruciblePlugin.Services.Register(new ProductMetadata(
    Guid, Name, new Version(Version), new Version(0, 1), new Version(0, 1), MultiplayerPolicy.Optional));
registration.AddCapability<IMyContract>("meadworks.mine.thing", new Version(1, 0), service);
// ...
void OnDestroy() => registration?.Dispose();
```

- Declare a hard `BepInDependency` on `com.meadworks.crucible` so Crucible's `Awake` runs first. `Plugin.Services` is assigned in that `Awake`. `Plugin.IsReady` becomes true only at its end. Check `IsReady` before registering, as Hex does.
- `Register` throws when the id is already registered or when the registry is disposed.
- `MultiplayerPolicy` maps to a peer class (`PeerClasses.From`): `RequiredOnAllPeers` → RequiredOnAllPeers; `ClientOnly` and `Optional` → ClientOptional; `ServerOnly` → ServerOnly. The peer class decides whether the product is active for a session (see Settings).
- `ProductRegistration.Dispose()` removes the product entry and every capability it added, clears its UI descriptor, removes its operations (they subscribe to the registration's disposal), and makes the registration inactive. `IsActive` reports this state. A disposed registration throws `ObjectDisposedException` on `AddCapability`, `AddUi` or operation registration. Kit notices and dialogs stop accepting it (`Show` returns false, and dialog answers are ignored).
- `OperationRegistry.Register` returns its own `IDisposable` for removing one operation early. Disposing the registration also removes it.
- `CapabilityRegistry.Generation` changes on every add or remove. `CapabilityLink<T>` caches one resolution and re-resolves only when the generation changes. Use it for per-frame optional lookups. It sees providers that register after your `Awake` and providers that leave.
- The registries are main-thread only. **Unverified:** nothing enforces this, but the code comments state it, and none of the collections are synchronized.

## Optional integration and fallback

| Missing piece | Pattern |
| --- | --- |
| A capability | `TryResolve` or `CapabilityLink.TryGet` returns false. Keep a standalone behavior or skip the feature. `FeatureLink.TryOwned` returns false when no product owns a feature or when the owner's check throws. |
| Crucible.UI | Declare `[BepInDependency("com.meadworks.crucible.ui", SoftDependency)]`. Check `Chainloader.PluginInfos.ContainsKey(...)`, and put all Crucible.UI type references in a separate `[MethodImpl(NoInlining)]` method so the runtime does not load `Crucible.UI.dll` when the plugin is absent. Then check `CrucibleUiPlugin.Active`, which is false on headless hosts. |
| A Kit notice | `KitNotices.Show` returns false and writes the text to Crucible.UI's log when there is no UI, when the HUD is missing (for example in `Awake`) or when the source registration is inactive. When the game HUD is hidden, it returns true without showing a notice unless `ShowWhenHidden` is set. |
| Crucible itself | Not demonstrated. Every first-party product hard-depends on Crucible. A mod that wants to run without Crucible would have to keep every Crucible type reference behind the same soft-dependency isolation. **Unverified.** |

Local discovery of a capability says nothing about the remote peer. See [multiplayer-consumer-api.md](multiplayer-consumer-api.md) for remote negotiation.

## Settings

### Scopes

`ConfigScope` has three values:

| Scope | Synchronized | Who edits on a remote client | Value a remote client acts on |
| --- | --- | --- | --- |
| `Local` | no | the player | the player's own value |
| `ServerAuthoritative` | yes (Jotunn, admin-only) | an administrator, through a server amendment. Others are refused (`ServerLocked`). | the server's value while connected |
| `Cheat` | yes (Jotunn, admin-only) | an administrator | the server's value. Locked to the **safe value** while authority is pending, on a vanilla server, or when the product is off. |

```csharp
var name   = MeadworksConfig.Bind(Config, "Local", "GreetingName", "Viking", "What it does. Local: stored on this machine.");
var policy = MeadworksConfig.Bind(Config, "Server", "AnnounceOnSpawn", true, "What it does. Server-authoritative.", ConfigScope.ServerAuthoritative);
var cheat  = MeadworksConfig.BindCheat(Config, "Cheats", "NoCost", false, "What it does.", MischiefCategory.Economy);
```

- Call `MeadworksConfig.EnsureSchema(Config, 1)` before binding. It backs up the file before each schema migration step.
- `BindCheat` makes the safe value the default and tags a Mischief category. An untagged cheat counts as `World`, the strictest category.
- `BindHidden` binds an entry that configuration UIs do not list. Cheats cannot be hidden.
- The `ConfigDescription` overloads keep your tags. A `ConfigDependency` tag only describes UI enable or visible state. It does not change stored values or authority.
- On a host, single-player game or dedicated server, every scope edits that machine's own file. In the main menu, the player edits their own file.
- For a `ServerAuthoritative` setting of a client-optional product, `serverLocked: false` (in `Effective`, `Amend` or `MeadworksAuthority.Decide`) leaves the client's own value in effect. The default is `true`.

### Product activity

On a remote client, `MeadworksAuthority.ProductActive` returns false in three cases: a `ServerOnly` product; a `RequiredOnAllPeers` product on a vanilla server; and a `RequiredOnAllPeers` product on a Crucible server that lacks it. A client-optional product stays active, but on a server without it, its cheats are locked to the safe value. The client treats the server as vanilla after `Authority.VanillaServerTimeoutSeconds` (default 10) without a Crucible Hello.

### Reading values (live)

`MeadworksConfig.Effective(entry, productId, scope, safe)` returns `entry.Value` unless the authority decision's source is `Locked`, in which case it returns `safe`. Jotunn writes the server's values into synchronized entries in memory, so reading `Effective` (or `entry.Value` for `Local`) at the point of use picks up server changes without a restart. **Unverified:** whether `ConfigEntry.SettingChanged` fires for every Jotunn-applied server value. Read at use time instead of caching only from the event.

### Changing values (durable)

`MeadworksConfig.Amend(entry, productId, scope, value)` routes a change through the authority gate (`AmendRoute`):

- **Own:** writes the player's own value and persists it with an atomic file replace and a readback check (`PersistOwnValue`).
- **Publish** (host or dedicated server, non-Local scope): writes and persists it, then sends the change through Jotunn's synchronization to connected clients.
- **Server** (remote administrator): sends the change through Jotunn. The server applies it and relays it.
- **Refused:** writes nothing and shows the localized blocked-option notice.

`AmendBatch` preflights several entries of one product and runs one write callback. A Crucible guard rewrites the server's value when a non-administrator client changes a server-bound entry directly, and server values are never written into the client's file. If Jotunn's synchronization methods are unavailable, a single `Amend` applies locally with a warning, and `AmendBatch` refuses.

## Logging

`MeadworksLog.For(Logger, "Product")` prefixes lines with `[Meadworks:Product]`, suppresses repeats and follows the suite-wide and per-product log level. By default only warnings and errors are written. `Info` needs the Normal level or higher.

## Kit UI (Crucible.UI)

All Kit calls are main-thread calls, and all return null, false or nothing on a headless process.

### Notices

```csharp
KitNotices.Show(registration, new KitNotice("Text or $token", KitNoticeKind.TopLeft));
```

Kinds: `TopLeft`, `Center`, `Unlock` (always logged, shown while the HUD is hidden), `Biome` (never logged). A provider registered as `meadworks.ui.notices` 1.0 (`IKitNoticeProvider`) may take over presentation. If it declines or throws, the native `MessageHud` fallback runs. With no provider registered, Crucible.UI presents `TopLeft` and `Center` on the notice stack (`KitNoticePanel`). Each `KitNotice` instance is delivered once. Showing it again returns false. A true result means handled or intentionally hidden, not displayed.

The stack shows several cards at once, newest at the top. A notice that carries an icon shows that icon. The same kind, raw text and title collapse into one card: item amounts add, and the card's timer restarts. The badge is the summed amount, or the repeat count when the notice has no amount. Cards past **Maximum Visible**, and cards that do not fit the open gap, wait in arrival order and keep a full timer until they are shown. Each card fades through its last 0.4 seconds. The stack hides while the inventory, map, pause menu, or a MeadWorks window is open, and the timers wait. It sits in the chosen corner and moves vertically until it is clear of the compass, status-effect icons, the hotbar, and other shown MeadWorks HUD panels. When the stack is taller than that gap, fewer cards are shown and the rest wait. A card wider than the screen shrinks to the side margins. `Unlock` and `Biome` stay on the game's own banners. Turning **Stacked Notices** off sends `TopLeft` and `Center` through `MessageHud`.

Settings live in Crucible UI's config, section `Notices`, local to this player. The server does not override them. Defaults: Stacked Notices on, Position `TopLeft`, Duration 4 seconds (1 to 30), Maximum Visible 5 (range 1 to 8). Changes apply without a restart. Other mods keep calling `KitNotices.Show`. Register `IKitNoticeProvider` only to replace this stack. The stack is a MeadWorks object and leaves `MessageHud` and other mods' UI alone.

### Dialogs

`KitDialogs.Confirm`, `Message`, `Input` and `Choose` take the source registration and return a `KitDialogRequest`. Each request accepts at most one answer, and only while the source is active and the scene has not changed. Callbacks run after presentation closes. `Dispose()` closes the dialog without running a gameplay callback. Scene changes dispose open requests. Revalidate game state inside the callback.

### Tooltips

- `KitTooltip.Set(control, topic, text)` attaches and styles the game's `UITooltip`. Empty text hides it.
- `KitTooltip.Show` and `KitTooltip.Hide` control the tooltip explicitly.
- `KitStructuredTip.Set(control, KitTipDocument, fallbackTopic, fallbackText)` renders typed sections and keeps the plain fallback.

### HUD panels

`KitHudPanel.Create(hudRoot, name, size, position, owner, id)` creates a movable HUD panel. `owner` and `id` must be stable: letters, digits, `.`, `_` or `-`, up to 128 characters each. They key the player's saved layout in Crucible.UI's own (local) config. The product keeps supplying the automatic position (`SetPosition`) and calls `Update(visible, cursorFree, scale, opacity)` each frame. A saved user placement takes priority until it is reset, and layout visibility cannot show a panel the product hides. Crucible.UI runs `KitHudLayout.Tick` itself. Call `Dispose()` when the owner ends.

### Windows and appearance

- `KitWindow.Create(id, size, parent, typing, round)` returns a closed window that behaves like a game window while open: the cursor is free, gameplay hotkeys are blocked (every hotkey when `typing`), and Esc closes it. It returns null on a headless host. For gamepad focus, add `Toggle(hotkey)` and `Focusable(control, x, y)`.
- `KitAppearance` exposes the theme by role. It covers fonts (`KitFontRole`), colors (`KitColorRole`), surface sprites (`KitSurfaceRole`) and `PixelsPerUnit`. `Sprite(role)` returns null when styling is off, so the caller keeps its native look. `KitAppearance.Changed` (forwarding `KitLook.Changed`) fires when the player switches look settings. Re-read roles in the handler.
- `KitLook` holds the player's look settings (Styled, Timber or Brown, and others).
- The role enums are declared in `KitAppearanceRules.cs`. New roles, such as `KitColorRole.Heading`, are appended so existing values keep their meaning. Use only role names that exist in the Crucible.UI you build against.

## Build hashes (diagnostic only)

Crucible publishes `Func<IReadOnlyList<BuildEntry>>` under `BuildComparison.Capability` (`meadworks.crucible.build_hashes`), version 1.0. Use the constant instead of the literal. The function returns the SHA256 of every loaded assembly named `Crucible*` and of every plugin assembly whose GUID starts with `com.meadworks.`. Hashes are read once from disk and cached. `BuildComparison.Compare(local, remote)` lists the assemblies that are the same, different, present only here and present only there, and `Summary()` gives one line.

This is a build-compatibility report only. A client can report any value, a matching hash proves nothing about the code actually running, and Crucible never refuses or disconnects a peer because of it. Do not use it for admission or anti-cheat decisions. Assemblies of plugins outside the `com.meadworks.` namespace are not included.

```csharp
static readonly CapabilityLink<Func<IReadOnlyList<BuildEntry>>> hashes =
    new CapabilityLink<Func<IReadOnlyList<BuildEntry>>>(BuildComparison.Capability, new Version(1, 0));
if (hashes.TryGet(CruciblePlugin.Services.Capabilities, out var local)) { var entries = local!(); }
```
