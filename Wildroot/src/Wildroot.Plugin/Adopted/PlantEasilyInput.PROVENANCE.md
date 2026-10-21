# PlantEasily native input adoption

Source: [PlantEasily 2.2.2, Patches/InputPatches.cs](https://github.com/AdvizeGH/Advize_ValheimMods/blob/1deeb3962eb8681394961b0f35a4dfbc315c6f99/Advize_PlantEasily/Patches/InputPatches.cs).

Pinned commit: `1deeb3962eb8681394961b0f35a4dfbc315c6f99`.

Source file SHA256: `283A17F90916D3617DE29211FD3DB9868B2747A633BEE95D769F3840A9D22F26`.

The source repository's unmodified `LICENSE` declares GNU GPL version 3. Its SHA256 is `589ED823E9A84C56FEB95AC58E7CF384626B9CBF4FDA2A907BC36E103DE1BAD2`. The adopted file preserves that grant as `GPL-3.0-only`; no permission for a later GPL version is inferred. Wildroot's existing code license is GPL-3.0-or-later, and the full GPL version 3 text is already included at `licenses/GPL-3.0.txt`. Third-party source provenance and licensing remain separate from Wildroot product identity.

## Adopted behavior and changes

- `PlantEasilyInputPatches.cs` retains the source's two native Harmony targets, `HotkeyBar.Update` and `Player.StartGuardianPower`, and its `Prefix() => !OverrideGamepadInput` rule.
- The reference's cached placement ghost/configured modifier condition maps to Wildroot's actual native placement ghost, configured `Input.GamepadModifierKey`, existing cultivator/catalog availability and native `ZInput.GetKey` read. Existing master, session and UI gates remain in force.
- `InputController.cs` now reads configured key codes with the reference's `ZInput.GetKeyDown`/`GetKey` APIs instead of `UnityEngine.Input`. Config paths, chord modifiers, resize priority, farm/grid gestures and Crucible's master-setting amendment remain unchanged.
- Toggle actions use the pinned `Patches/InputPatches.cs` calls to `KeyboardShortcut.IsDown()`, retaining its exact chord check so an extra held key cannot trigger a shorter toggle shortcut. Grid resize/modifier reads remain native ZInput; the existing controller right-stick snapping gesture is preserved.
- Native target names are strings to support the existing raw game references without requiring publicized private methods.

This unit does not adopt the source's entire `UpdateBuildGuiInput` prefix, modifier latch, status messages, or hint generation. Existing Wildroot routing and extensions remain; full physical keyboard/controller parity requires native validation. Resource payment, authority and multiplayer execution are outside this input unit.

## Shared input admission (2026-10-08)

InputController now uses the existing Crucible C07 Gameplay admission before reading or applying owned controls. Registered Meadworks mod/typing windows, focused native input fields and native game windows block grid/toggle/harvest/gamepad override paths. The pre-existing TextViewer guard remains because C07 does not include that window. Actor/world/death cleanup still runs first; blocked input cancels pending planting/harvest through the existing transaction cancellation paths while retaining farm mode/equipment. Cultivator auto-entry is deferred until input is available. Handled native placement also refuses while blocked.

Native key/modifier reads, KeyboardShortcut.IsDown exact toggle matching, cultivator activation, resize priority and right-stick snapping remain as adopted above. Controller button reads/consumption reuse C07's native ZInput wrappers; only a resolved directional/right-stick action consumes its matching native press. No keyboard consumption ledger, replacement input framework, reference transport or material algorithm is introduced. The adopted Harmony patch file and pinned upstream source are unchanged.
