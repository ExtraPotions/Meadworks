# Changelog

## Unreleased

- Hide the ignored Tamed Stars setting while retaining its saved value and local scope. Wildblood continues to control creature stars.

## 0.1.18 (2026-10-22)

- Version 0.1.18.
- Requires Crucible 0.3.28 / Crucible.UI 0.3.20.
- Preserve the game's newborn level and egg quality when Wildblood declines star inheritance. Wildblood remains the owner of creature stars and mutations.
- Import legacy EliteCreaturesReborn egg metadata without lowering saved egg quality to Shepherd's offspring cap.
- Review the six adopted breeding files against EliteCreaturesReborn 3.30.0 and refresh their GPL provenance. No new star or mutation settings.

## 0.1.17 (2026-10-21)

- Thunderstore republish of the release 8 build; no code changes.
- Requires Crucible 0.3.28 / Crucible.UI 0.3.20 (Crucible 0.3.27 on Thunderstore is a source archive without DLLs).

## 0.1.16 (2026-10-21)

- Requires Crucible 0.3.27 / Crucible.UI 0.3.20.
- Due births wait for Wildblood's server rules before the offspring is created or inherits stars and mutations. The pregnancy and its saved parent/twin data remain available to retry after the rules arrive. Breeding without Wildblood is unchanged.

## 0.1.15 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- Dedicated servers register troughs without loading original art or reading materials. Trough recipes, containers and saved food continue to load.
- Seasonal Breeding (on by default, set by the host or server): while Solstice seasons are on, tame breeding uses that season's published rate. Spring is faster, winter is slower, and a rate of zero starts no new pregnancies. A pregnancy already underway still finishes. Off, or without Solstice, breeding stays as it is. Feeding and troughs are unchanged.
- Tamed Stars in Shepherd's config is ignored. Wildblood decides whether a new birth inherits stars. Without Wildblood, or when Wildblood's Tamed Stars is off, a newborn keeps its mother's level, as the game does, and Better Offspring and Birth Star Chance add nothing. The old key stays in the config file and does not change this.
- A trough keeps its food when the animal cannot be fed. A lost reply is asked again with the same request, so a second food is not taken.

## 0.1.14 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.

### Settings

- Simpler in-game settings: Shepherd now marks which settings most players need. The MeadWorks menu can show just six (Enabled, Container Feeding, Show Food On Troughs, Lore Detail, Show Callouts, Herd Book Key); the rest stay in Almanac. Recipes, feed and animal deny lists, taming levels, Mutation Chance, Breeding Commands and the import marker are normally edited in the config file. No setting was renamed or removed, and saved values are kept.

### Trough safety

- Feed Barrel, Plank Trough and Stone Trough always load. When the game chest a trough is built from is missing, or another mod changed it, the trough is built from another game chest or a plain container, so saved troughs and the food in them still load. The log says which one was used.
- If a trough's art fails to load, the trough keeps a plain look instead of disappearing.
- Troughs keep their own size (Plank and Stone Trough 6x1, Feed Barrel 4x2). Burrow does not resize them.
- Clearer help for Star Inheritance, Pen Mode and Land Pen Capacity. Setting text in English, Spanish and French.
- Troughs always load: when the game's wood chest or barrel is missing or changed by another mod, troughs are built from another vanilla chest, or a minimal container as a last resort, and the log names the replacement. Saved troughs keep their prefab names and their food.

## 0.1.13 - batch 4E follow-up (2026-10-19)

- 6 - Breeding / Mutation Chance now changes how often offspring mutate.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.1.12 (2026-10-18)

- Explain setting availability through the shared dependency metadata while preserving saved values and existing gameplay rules.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

## 0.1.11 local art candidate (2026-10-17)

- Original Feed Barrel, Plank Trough and Stone Trough models with matching icons and hollow feeding basins.
- Native visual and fit qualification is pending; this entry does not record a publication.

- Optional Honey Per Day and Honey Per Player Online adjust native hive production while preserving native/off defaults, capacity and existing harvest rewards.

- Native-eating young such as asksvin hatchlings can eat their own foods from troughs and enabled feed containers. Their meal timer survives world reloads and uses the adult's feeding interval, with a ten-minute fallback.
- Reach Through Walls controls feeding through nearby walls: one meter by default, up to two meters, or zero for clear reach only. Floors, terrain and thicker barriers block approach-mode meals.
- With Require Move To Feed on, animals cannot eat from a source more than one meter above or below their footing.
- Follow Command leaves tameables without native follow AI unchanged, preserving their ordinary interaction.

## 0.1.10 (2026-10-15)

- First release on Thunderstore.
