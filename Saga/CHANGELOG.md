# Changelog

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
