# Changelog

## 0.2.9 (2026-10-28)

- Taking runes out keeps the table balance when an inventory add fails. Destroying a Rune Table drops every stored stack it can create; a stack that cannot spawn is not reported as dropped.
- Creature loot validates its saved list before the ragdoll clears it. An unknown item is skipped without stopping the other drops. An item the game refuses to spawn stays on the ragdoll for another try; an item whose spawn call fails partway is treated as dropped, so it is never duplicated.
- A creature without a loot-carrying ragdoll drops each item separately, so one failed drop no longer loses the rest.

## 0.2.8 (2026-10-26)

- The Wet, Cold and Freezing filter for the local player now always switches off, even if the game's status update fails partway, so it cannot stay on for other characters.

## Unreleased

- The Rune Table can focus one new Recasting inscription on Offense, Defense or Utility, with a choice of prefix or suffix. Other inscriptions roll normally at the same rune cost. Any category keeps the unrestricted roll; an impossible result spends nothing.
- Recasting category and side choices are saved locally and restored when the table opens again.
- Recasting controls and help are available in all seven supported languages.

- The identification check that runs in a normal game now follows Common, Uncommon, Rare, Epic and Legendary, including the Legendary ground glint. It no longer checks enhancement levels, the +N badge, enhancement scrolls or the Enhance tab, which Inscribe 0.2.0 removed.

## 0.2.5 (2026-10-21)

- Thunderstore republish of the release 8 build; no code changes.

## 0.2.4 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- Thrifty Hands gives back the quality of ingredient that was used. A free craft that spent quality 2 or 3 eggs returns those eggs at that quality.
- Rune stones that do not fit when a payment is returned are dropped at the player's feet.
- Dedicated servers register runes, gems, the Dvergr Chisel and legacy scrolls without loading original item art or materials. Missing client art logs a warning while keeping the item registered.
- Rune Table labels, item classes, inscriptions, gems and the other adopted words now ship in Spanish, French, German, Russian, Chinese and Korean. A line that is missing from a language list still shows the English word. A personal `Inscribe.translations.<Language>.yml` still overrides the shipped words.
- Rune Table sacrifice banks essence only for trophies that left the inventory. A refused removal leaves the trophies and the essence pool as they were, with no sacrifice message.
- A rune, gem, or chisel writes the item only after the full cost has left the carried stack or the Rune Table. A short payment leaves the item unchanged. A refused item write puts that cost back. A recovered gem is given only after the write.
- Thrifty Hands returns each inventory's free share to that inventory. Materials taken from a shared chest go back to the chest, and the crafter is refunded only what left their own inventory.

## 0.2.3 (2026-10-20)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- The admin `inscribe give` command no longer drops extra runes when an add stops part-way (for example the inventory filling up while topping up a partial stack, or an inventory layout mod keeping cells for itself). It measures what actually arrived and drops only the rest at your feet; the reply lists the counts that went into the inventory and that were dropped.

## 0.2.2 (2026-10-20)

- Requires Crucible 0.3.24 / Crucible.UI 0.3.17.
- An `Inscribe.economy.yml` written before 0.2.0 no longer stops the Dvergr Chisel, gems, Cleansing and the Serpent Rune on Epic and Legendary items. On first load the main economy file is updated once: values still at the old defaults (those runes' rarity lists, the old Awakening row and drop weights, Recasting's weights, boss and creature bonuses that paid Awakening) move to the 0.2.x defaults, and the removed scroll drop section is dropped. Values you changed are kept. The original is saved next to it as `Inscribe.economy.pre-0.2.0.bak`, one log line lists the update, and a marker comment stops it from running again. Only the file on the machine that owns it is updated (the host or dedicated server for a hosted world); rules received from a server are never written to disk.
- The game's blue "equipped" fill is hidden by making it transparent instead of switching it off each frame, so the inventory no longer rebuilds its canvas every frame while a worn item is visible.
- While Kindle's Salvage tab is shown, Inscribe leaves the detail icon's rarity backdrop to Kindle instead of clearing it every frame.

## 0.2.1 (2026-10-20)

- Requires Crucible 0.3.23 / Crucible.UI 0.3.16.
- Pickaxes take runes all the way up: a rune on a pickaxe, tool or fishing rod moves the prefix slots its few prefixes cannot fill to suffixes, so the Ascension Rune reaches Epic and Legendary (it stopped at Rare). Dropped gear keeps its limits.
- Pickaxes take sockets: the Dvergr Chisel cuts them and gems give their weapon stat. Other tools, fishing rods and torches still take none.
- Worn items are marked by a thin parchment ring at the cell's edge in the inventory and hotbar, for every item, Normal or magic. The game's blue "equipped" fill is off, so a worn magic item keeps its rarity color and a worn Normal item no longer looks Rare. With Inscribe's display turned off, the game's blue fill comes back.
- Enhancement conversion no longer loses Honing Runes when another mod refuses the inventory write (Burrow's inventory hold) or a layout mod stops an add part-way. An enhanced item is marked converted only after its Honing Runes are confirmed: when the inventory takes none, its raised tiers are put back and it tries again later; when it takes some, the rest are dropped at the player's feet with a message. Old scrolls convert only for the stacks that actually left the inventory, and their replacement is dropped at the player's feet if the inventory refuses it.
- Rune Table: storing runes, gems or essence (Store all, Ctrl + click) credits the table only with what actually left the inventory, so a refused removal can no longer copy the stack.
- Volley and Thrifty Quiver: a volley fires extra arrows only for ammunition actually taken from the stack, and a refunded shot the inventory refuses is dropped at the shooter's feet instead of lost.
- No HarmonyX "Could not find method ... AddItem" warning at start: the legacy rune patch looks for the game's current five-argument positional `Inventory.AddItem` (it gained a `skipValidPositionCheck` flag) and patches it, so a legacy rune moved into a chosen slot converts too.

## 0.2.0 (2026-10-19)

- Update required to join servers running this version: players on older versions must update first.
- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.
- Simpler in-game settings: Inscribe's page in the F8 MeadWorks menu shows only the essential settings. Every setting is still in Almanac and the config file, and no keys or values change. The `inscribe ecr` console command is removed because the Elite Creatures Reborn synergy it reported cannot turn on.

### Enhancement removed

- Enhancement levels (+1 to +20), enhancement scrolls, the Enhance tab and button, the enhancement window key and pad chord, scroll drops and fusion, and enhancement stat effects are gone. Inscribe's upgrades are now runes, gems, rarity and the game's own quality levels.
- Existing enhanced items convert once, when carried or when their chest is opened: each level is worth the scroll its attempt used (a tier 1 scroll is 1/9 of a Honing Rune, tier 2 1/3, tier 3 one, tier 4 three), rounded up. Those Honing steps raise the item's inscriptions one tier at a time, up to what the item's level allows, keeping each value's place in its tier; the rest become Honing Runes in the same inventory. Rarity and inscription count never change. Examples: +5 = 1 step, +10 = 3, +15 = 8, +20 = 23.
- Old enhancement scrolls convert the same way: every nine ninths become a Honing Rune and each leftover ninth 2 Essence (nine tier 1 scrolls = 1 Honing Rune; one tier 4 scroll = 3).
- Nothing is deleted: the old level and "Enhanced by" keys stay on the item, and a marker (`ecf_enhancement_converted`) stops a second conversion. When the replacement does not fit, nothing changes and the conversion tries again later. Do not downgrade: an older Inscribe would apply the old level on top of the raised tiers.
- With ItemEnhancements itself installed, Inscribe leaves enhanced items and scrolls to it and converts nothing.
- Retired settings (old lines stay in the cfg file and are ignored): Enhancement Enabled, Enhancement Effects, all of 3 - Enhancement, Scroll Drops, Enhancement Badges, Enhancement Window Key, Enhancement Pad Chord, Guaranteed Upgrades, Guaranteed Upgrades Max Level and Enhancement Power Abilities. `Inscribe.enhancement.yml` is no longer read; an old `drops.scrolls` block in the economy file is ignored. The ItemEnhancements settings import and the `inscribe enhance` and scroll `give` commands are gone.
- The ivory +N icon badge and the +N on item names are gone. The game's own quality number (top right of each slot) is the only quality display.
- The small bronze and ivory ground glint now marks Legendary drops only (new 7 - Ground Glow / Legendary Glint, default on).
- Inscribe no longer contains ItemEnhancements code; its notice is kept for earlier versions.

### Deep North

- Kall Fimbulbringer (`FrozenKing_p3`, his last phase) is a boss for Inscribe drops: tier 8, six rune rolls, one gear roll, two Ascension Runes and a 75% Serpent Rune; the boss gems and Dvergr Chisel.
- Rune Table: Deep North trophies give 16 essence (Seal, Moose, Krigen, Hexen, Barka, Elaking, Eyeless One, Writhan, Frost Blob, Pulp). Kall Fimbulbringer has no trophy item in Valheim 1.0.20.
- Item level 8 for Frostcore, Liquid Frost, Writhan Roots, Dead Pulp, Ice and Sacrificial Blood.
- Existing economy files keep their edits: the built-in defaults are layered under them, so the new entries apply unless the file sets `use_defaults: false` or overrides the same keys.

### Batch 3 item variety

- Eleven new prefixes for the thinnest pools (helmets, legs, capes, shields, trinkets, belts, torches, tools, fishing rods, pickaxes), built from existing effects and ladders. Tools, fishing rods and pickaxes can now roll Uncommon and Rare.
- Five rarity tiers: Common, Uncommon and Rare are the old Normal, Magic and Rare (stored ids unchanged); new Epic (purple, four inscriptions, stronger tiers) and Legendary (gold, five inscriptions, strongest tiers, at least one socket).
- Legendary items come only from Wildblood boss kills and won raids, through `meadworks.inscribe.legendary_reward`; never from other drops, chests, crafting or runes.
- New host settings in 4 - Drops: Uncommon, Rare and Epic drop weights; Legendary from Wildblood bosses and raids, with their chances.
- Honing Rune (one inscription you pick rises a tier, up to what the item's level allows, and its value is rolled again in that tier), in a tinted version of the Ascension art. It drops from creatures and chests, lives in the Rune Table, and can be turned off in the economy YAML.
- Do not downgrade: an older Inscribe shows Epic and Legendary items as an unknown rarity in grey (their inscriptions stay and apply, runes refuse them), and the game removes Honing Runes it does not know from inventories and chests.

### Five runes

- The Ascension Rune raises an item one rarity step at any tier: Common to Uncommon (two inscriptions), Uncommon to Rare and Rare to Epic (one more each, within the new tier's prefix and suffix limits). It never makes a Legendary; on an Epic or Legendary item it refuses and says why. At the Rune Table an essence still chooses what it adds.
- The Awakening Rune is folded into Ascension and the Tempering Rune into Honing. Runes are now Ascension, Recasting, Honing, Cleansing and Sealed.
- Owned Awakening and Tempering Runes become the same number of Ascension and Honing Runes when an inventory, chest or tombstone loads, when picked up, and in a Rune Table's store. Their old prefabs stay registered so nothing is lost; converting again changes nothing.
- Drop weights: Ascension takes the old Awakening and Ascension shares (T1-T8: 37, 36, 33, 31, 28, 26, 24, 21) and Honing the old Honing and Tempering shares (2, 5, 7, 8, 10, 10, 12, 12); every other rune's odds are unchanged. Eikthyr's two Awakening Runes are two Ascension Runes.
- Existing economy files read the same way without being rewritten: Awakening and Tempering rows are ignored, their drop weights are added into Ascension and Honing, bonus rows pay the successor, and an Ascension row still limited to Uncommon (the old default) raises Common, Uncommon and Rare. A customized Ascension rarity list is kept.
- Salvage (Kindle) gives back an Ascension Rune from Uncommon, Rare and Epic items, one time in four.
- New text in English, Spanish and French (language freeze); other languages fall back to English for it.
- Portable pool coverage report (`--pool-report`).
- Rings and belts (Mantle) and trinkets roll rarities and inscriptions and take runes; Mantle registers the ring and belt classes and their pools. New host setting 1 - General / Accessory Rarity (default on) turns this off.
- Tier names everywhere players read them: Common, Uncommon, Rare, Epic, Legendary. "Magic Item Drops" shows as "Uncommon Or Better Gear Drops" (its config key is unchanged); rune and setting descriptions use the new names.
- Gem recovery: a replaced gem, or a gem in a salvaged item (Kindle), comes back by a host chance per tier: Common 100%, Uncommon 90%, Rare 75%, Epic 50%, Legendary 25% (2 - Runes / Gem Recovery). Otherwise it is lost, with a message. Salvaging products read it through `meadworks.crafting.salvage_gems`.
- Biome essences at the Rune Table: Mist (Mistlands; movement, stealth and exploration), Ash (Ashlands; fury, finishers, bosses, ammunition and thrown weapons) and Frost (Deep North, Frostcore icon; warmth, food, meads, breath and swimming), steering the Ascension Rune like the first five and paid from the same pool. They steer only existing inscriptions no other essence covers. New host setting 2 - Runes / Biome Essences (default on). Nothing new is saved.
- Edge secondary attacks: Quickened and Swift Casting on the weapon also shorten Edge's secondary cooldowns (at most by half; Heavy Hand never lengthens them), through `meadworks.inscribe.secondary_cooldown`. Damage, hit, leech, reach, projectile and cost inscriptions already applied to Edge secondaries.

### Batch 4D settings text

- General / Rarity Enabled explains what turning it off does.

## 0.1.15 - batch 4E gameplay fixes (2026-10-19)

- Workbench upgrades keep existing enhancements, rarity, sockets and crafter data.
- Runes work on all single equippable gear, tools and weapons; consumables and ammo stay excluded.
- Enhancement can pay scrolls from the open chest.
- Enhance and Salvage views show only their own list when switching tabs.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.1.14 (2026-10-18)

- Use shared structured cost tips and typed rune/gem confirmations; preserve fresh inventory checks and existing enhancement behavior.

- Present existing item tooltip details through shared structured sections while preserving native item text and optional details. Item rolls and enhancement outcomes remain unchanged.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12 for the shared UI/API integration. Broader 4D qualification is pending.

## 0.1.13 (2026-10-18)

- Use the shared framed control hover tips for Rune Table and enhancement controls.
- Coordinate enhancement-tab presentation with the shared crafting-tab selection.

## 0.1.12 local art candidate (2026-10-17)

- Original rune, gem, chisel and scroll models with matching icons, alongside native item identification cues.
- Native visual and fit qualification is pending; this entry does not record a publication.

- Distinguish item rarity, quality, enhancement and sockets with shared item markers and a tooltip identification summary, without changing the inventory layout.
- Use a neutral ivory enhancement badge across inventory, containers, hotbar, crafting, drag previews and pickup icons; keep rarity colors and add etched tier marks.
- Identify magical and enhanced drops in hover text; give enhanced ordinary equipment a small ground glint using the existing native particle shader pipeline.
- Scroll tooltips taller than the screen with the mouse wheel or right stick, keeping the current tooltip panel and text style.

## 0.1.11 (2026-10-15)

- First release on Thunderstore.
