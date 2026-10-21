# Changelog

## 0.2.11 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.

### Simpler in-game settings

- Simpler in-game settings: the MeadWorks menu shows Larder's eight essential settings (Food Slots, Allow Auto Eat, Auto Eat, Disable Food Degradation, Food Timers, Food Can Burn, Fermenter Timer, Safe Area Stamina). Almanac still lists every setting.
- The Import tools, Safe Area Ignored Creatures and the generated per-food, per-mead and per-station values are marked as normally edited in the config file. Keys, defaults, saved values and server authority are unchanged; Larder has no hotkeys.

### Settings text

- The per-food, per-mead and per-station settings show their names and help in English, Spanish and French, using the item's in-game name. Cook-time settings name the station and the food in plain words.
- Clearer help for Duration Modifier, Rest Delay and the stamina and eitr regeneration settings. They now point to the right section by name, and at 1 the regeneration delays leave another mod's value in place.
- Keys, defaults and values are unchanged.
- Setting descriptions name the real sections (2 - Foods, 3 - Eating, 4 - Recovery, 5 - Exertion, 6 - Kitchen), drop source-mod and "native" wording, give each Safe Area action its own text, and describe per-food and cook-time entries with the item's displayed name. Keys, sections, defaults and saved values are unchanged.

## 0.2.10 - batch 4E gameplay fixes (2026-10-19)

- Players automatically eat from their Mantle Food slots using normal eating rules, including at spawn, after respawn and after recovering their tombstone.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.2.9 (2026-10-18)

- Explain setting availability through the shared dependency metadata while preserving saved values and existing gameplay rules.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

## 0.2.8 local art candidate (2026-10-17)

- Original Cooking skill icon and an editable library of food, recovery and kitchen feature scenes. No new inventory items or recipes.
- Native visual and fit qualification is pending; this entry does not record a publication.

## 0.2.7 (2026-10-17)

- Skip destroyed or missing item prefabs while restoring food values during shutdown; continue restoring valid prefabs.
- Include the failing teardown method and full exception in shutdown diagnostics.

- Recovery Rested Delay can change the natural wait before Rested is granted, from zero to sixty seconds. The default follows the native Resting asset; fire and resting conditions still apply.
- Safe Stamina can ignore creatures by prefab or displayed name, with an empty default list.
- Optional Safe Area Encumbered makes safe encumbered walking free and permits stamina regeneration. It is off by default and respects other safe-action switches.

## 0.2.6 (2026-10-15)

- First release on Thunderstore.
