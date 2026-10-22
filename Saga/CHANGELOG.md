# Changelog

## 0.1.16 (2026-10-21)

- Thunderstore republish of the release 8 build; no code changes.
- Requires Crucible 0.3.28 / Crucible.UI 0.3.20 (Crucible 0.3.27 on Thunderstore is a source archive without DLLs).

## 0.1.15 (2026-10-21)

- Requires Crucible 0.3.27 / Crucible.UI 0.3.20.
- Power Strength Relaxed moves the numbers and resistances and leaves each boss-power switch as it is. Vanilla still turns those switches off.
- The power wheel draws its cards on a ring around the center hint, oldest at the top then clockwise, without the square wood plate.
- The guardian status plate grows so each attuned name, including the equipped power, stays inside the wood. The equipped name is not drawn again across the plate edge.

## 0.1.14 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- Hold a key or a gamepad button to choose one attuned power. The choice calls the game's SetGuardianPower and keeps the recharge timer. Host setting Activate All Attuned Powers stays on by default, so one activation still runs every attuned power and the wheel stays hidden. Turn it off to choose a single power. The key and the gamepad button are unbound by default. Almanac warns when the key is already used by the game or another MeadWorks mod.
- Grouped number presets in the MeadWorks menu: Experience, Perk Thresholds, Perk Strength, Death, Finds, Fishing Fight and Power Strength. Each is Vanilla, MeadWorks default, Relaxed or Custom. Choosing one writes that group's values immediately. Editing a member selects Custom. Loading never replaces saved values; a group that does not match its label becomes Custom. Host or server owns every group. Joining clients keep their own saved preferences and do not expand a server preset locally. Planted Pickable Foraging XP, Activate All Attuned Powers and the power wheel keys are unchanged. The overlapping callout, sneak, weapon and power settings still work; none of those keys were removed.
- Boss kill credit is granted when the boss dies, by the server, to players inside Kill Credit Range. A living boss and a packet from another player do not grant a kill. The count is still one per boss, saved on the character as before.
- Woodcutting fell and split experience is granted by the server, once per tree or log, and only up to the configured fell or split amount for the hardest wood. Another player cannot raise your Woodcutting skill.
- A clean-strike mark counts only when the player who sent it owns that character, so a mark cannot force an extra ore roll.
- While the power wheel is open, the gamepad buttons it uses do not also use, build, dodge, or start the guardian power. Escape closes the wheel without opening the pause menu.
- The open wheel updates its labels when the text or the highlight changes.

## 0.1.13 (2026-10-21)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- New host setting 19 - Foraging / Planted Pickable Foraging XP (0–100%, default 10%): picking a pickable a player planted, such as cultivator stones, berries or mushrooms, earns that share of the normal Foraging experience, so a planted patch is no longer a fast way to level. Wild plants give full experience as before. Yields, Extra Yield Chance and sweep picking are unchanged, and crops still train Farming. A pickable counts as planted when it carries the shared `meadworks.pickable.planted` marker (written by Wildroot 0.1.17 and later) or the game's own builder record, which pickables planted with Wildroot before this change already have; pickables with neither count as wild. The setting is server-owned and applies on the next pick.

## 0.1.12 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.
- New host setting Progression / Skill Loss Overrides (empty by default): `Skill=Percent` entries, such as `Swords=0, Run=10`, give listed skills their own death loss; every other skill keeps Skill Loss On Death.
- Kall Fimbulbringer (Deep North, Valheim 1.0.20) is the eighth power, `4 - Power 8 - Kall Fimbulbringer`: Frost resistance (Very resistant), Frost damage +15%, Health regeneration +15%, Movement speed +5%, Armor +10, Stamina regeneration +20%, All damage +5%. Only the final phase (FrozenKing_p3) counts as a kill; the first two phases never do. Kill credit uses the same range rules as the other bosses. Levels 1–7 need 1–7 Kall kills; the seven older powers keep their requirements.
- A world where Kall is already defeated (`defeated_frozenking_p3`) seeds one Kall kill, once, also for characters seeded by an earlier Saga.
- `Saga.PowerLevels.yml` files written before Kall give Kall the default requirements. An earlier Saga loading a character keeps the Kall kill count but drops a Kall attunement; it rejects a power file that mentions Kall and keeps its defaults.
- Valheim 1.0.20 has no Kall boss stone or Deep North power effect. Until it does, attune or release Kall at his summoning altar in his boss room with the alternate use (AltPlace + Use, Shift+E by default) once you have a Kall kill; the same attunement limit and replace rules apply, and offering Hateful Blood still summons him. A future `GP_DeepNorth` stone works as well. The power shows the game's Frost status icon.
- Settings help: the 49 boss-power features say what their value does and in which unit; Hit Size, Hardened Max Stacks, Show Callouts, Dodging and Riding descriptions explain their effect. Keys, defaults and values are unchanged.
- Read existing text and indicator colors from shared Crucible.UI color roles; colors are unchanged.
- Simpler in-game settings: Saga ships a settings tier list. The MeadWorks menu shows its 7 Essential settings (Enabled, Powers Enabled, Skill Loss On Death, Skill Book, Skill Gain Notices, Max Attuned Powers and Power Display Mode); everything else stays in Almanac, and 22 modder-level entries (cheats, hidden data carriers, data lists, Advanced Power Config and formula internals) are marked as normally edited in the config file. No key, default or value changes. The `fishlog` console and chat command now just opens the Angler's Log window instead of also printing the log.

## 0.1.11 (2026-10-18)

- Bind existing recipe-row buttons to the shared look while preserving row geometry and navigation.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

## 0.1.10 (2026-10-15)

- First release on Thunderstore.
