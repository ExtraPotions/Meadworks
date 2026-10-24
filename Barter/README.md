# Barter

Traders you can shape: per-trader buy and sell lists, custom currencies, prices that follow the trader's purse, and a sell list built on the Meadworks interface kit.

Version 0.1.3. Requires Crucible 0.3.28 / Crucible.UI 0.3.20. Changes are in [CHANGELOG.md](CHANGELOG.md).

## Features

- Trade lists: write what each trader sells (Buy lists) and what each trader pays for (Sell lists) in YAML, JSON or CSV. Lists can name a lot size, a price, a quality, a currency, and the world keys or player keys that decide when an entry is offered. A `common` list applies to every trader.
- Currencies: pay in any item. A trader can have its own currency, and an entry can name another one.
- Trader purse: Haldor, Hildir and the Bog Witch (and traders you add) have a balance that falls when they pay you, rises when you buy, and refills each morning. A poor trader charges more and pays less; a rich one the opposite. The purse and what it does to prices show beside the trader's window.
- Sell list: a list beside the trader's window of what you carry that the trader pays for, with a filter, the lot size and quality, the price and its currency, and a Sell button. Worn and hotbar gear stays out of the list by default. With the sell panel turned off in the settings, the game's own Sell button works as in vanilla.
- Discovery: a trader does not offer items you have not discovered yet, unless you list them as always offered.
- The lists are checked as a whole: a file with a mistake is rejected with a message and the previous lists stay in force.

The mechanics follow shudnal's TradersExtended (public domain), so lists written for it read here unchanged. Barter's panels, settings, server authority and text come from Crucible.

## How trades are settled

- The server checks every trade: price, currency, lot, quality, world keys and how close you stand to the trader. Two players trading with the same trader at once are served one at a time, and a repeated request is refused.
- Trader purses live in authenticated files on the server. Remote trades use trusted cooperative accounting: the server verifies the price and trusts the connected client's reported inventory outcome, like Valheim's client-owned inventories. A successful purchase credits the purse, a sale debits it, and a reported failure refunds a reserved sale debit, each once. A modified client can lie about its outcome; authenticated receipts do not prove inventory changes. Duplicate reports cannot settle again. If no report arrives within 120 seconds, the reservation unlocks with its debit retained; later reports cannot credit or refund it.
- Before your inventory changes, Barter writes the trade down. If the game or the connection drops in the middle of a trade, your next visit settles it without repeating the item change. A trade whose outcome is not known stays blocked with its record kept; use **Resolve pending trade** in Trader Services to ask the server to settle it.
- A purchase needs free inventory slots after paying, and a sale needs room for the payment; the Sell button is disabled when the payment would not fit.
- Equipped gear and protected extra-slot items are never taken as payment or sold. Hide Equipped Items also controls the hotbar filter; turn it off to let unequipped hotbar gear appear.
- Trader Services (from the purse card, or the controller's Y button) holds buyback, paid repairs and pending-trade recovery. Buyback and paid repairs work only for the host's own character, because a server cannot see another player's inventory. Hosts and admins can edit the lists in game; an edit made against an older copy of a list is refused.
- Purchases that pay in keys or inventory rows, and offers gated on another player's personal keys or discoveries, are refused where the server cannot check them.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.28 or a later 0.3.x, which includes Crucible.UI 0.3.20 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Barter with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Barter` folder from this package into `BepInEx/plugins/`.
- Install Barter on the server too, and on every client.

## Trade lists

The first time Barter runs (when no Barter list file exists) it writes seven files to `BepInEx/config/`: `Barter.common.Buy.yml`, an explained example with every entry commented out, and a buy and sell list each for Haldor, Hildir and the Bog Witch. They keep the game's own stock of all three traders. Haldor's buy list adds a few raw materials (some behind boss keys); Hildir's and the Bog Witch's buy lists are empty for you to fill; each trader's sell list pays a modest price per lot for materials (hides, metals, swamp ingredients). Trophies and valuables are not listed because they already sell at the game's own value through Common Valuables. The shipped prices are Barter's own modest scale. Edit or delete any file; nothing is written again while a Barter list file exists. Barter reads these files, and reads them again within a couple of seconds whenever one changes:

- `BepInEx/config/Barter.<trader>.<Buy|Sell>[.<anything>].<yml|yaml|json|csv>`, for example `Barter.Haldor.Buy.yml`, `Barter.common.Sell.csv` or `Barter.Hildir.Buy.food.yml`.
- Any file in the folder `BepInEx/config/Barter/` named `<trader>.<buy|sell>[.<anything>].<extension>` without the `Barter.` prefix, for example `haldor.buy.json`. This is how TradersExtended names its files, so you can copy its lists there.

All files of the same trader and kind are added together in file name order. `common` lists apply to every trader first; a trader's own entries follow, and in a Sell list a trader's entry replaces a common one for the same item, quality and lot size. Trader names are the trader's prefab name without case: `Haldor`, `Hildir`, `BogWitch`.

```yaml
# Barter.Haldor.Buy.yml
- prefab: DragonEgg
  price: 500
  requiredGlobalKey: defeated_dragon
- prefab: RawMeat
  stack: 10
  price: 2
  currency: Ruby
```

Every field except `prefab` is optional: `stack` (items per lot, default 1), `price` (per lot, default 1), `quality`, `currency`, `requiredGlobalKey` and `notRequiredGlobalKey` (comma lists of world keys), `requiredPlayerKey` and `notRequiredPlayerKey` (comma lists of player keys). In a Buy list `quality` is the quality you get; in a Sell list it is the quality the price is for (0 for any). An entry with a price or a lot below one is skipped with a warning. A JSON list is read as YAML, which is a superset of JSON. A CSV list has a header row with at least `prefab`.

## Configuration

- Settings file: `BepInEx/config/com.meadworks.barter.cfg` (Barter 0.1.1 and earlier used `com.extrapotions.barter.cfg`; its values are carried over on the first start and the old file is kept as `com.extrapotions.barter.cfg.bak`). Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- Trader Purse: on by default for every trader that has a Barter list (Haldor, Hildir and the Bog Witch with the shipped lists, the Custom Traders setting, and any trader you write a list for); traders from other mods without a list keep the game's prices. Settings: on or off, flexible pricing, minimum and maximum balance, daily increase and decrease, discount and markup, refill interval and the morning message.
- Trade Lists: sell only discovered items, items always offered, remove the game's items or other mods' items from buy lists, quality price multiplier for sold gear, common valuables (every valued item sells at its value), custom traders and trader currencies (`Haldor:Coins,Hildir:SilverCoin`).
- Interface: the sell panel, the purse card and hiding worn gear are your own choices.
- In multiplayer the server's values win for everything that shapes a trade, and those settings show as read-only on your side.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version. Trader lists and currencies change shared rules, so the server's lists and settings are the ones in force for everybody; lists on a client's own disk are ignored while it is on a Barter server.
- Trader purses are shared and stored in authenticated files alongside the world: `Barter.Authority/<world UID>/` inside a chunked world directory, or `<world>.db.Barter.Authority/<world UID>/` beside a legacy world database. Cloud worlds use the game's cloud storage backend. Include the entire authority directory and its key when backing up or transferring a world; native backup/conversion tools can omit additional directories. Existing records under `BepInEx/config/Barter.Authority/` are authenticated and copied without deleting the original. Trader ZDO values are display mirrors. The server refills purses each morning.
- A missing authority key on an existing world stops trading rather than creating a fresh purse. Restore the matching authority directory from backup. To deliberately initialize an otherwise empty authority directory, place a file named `allow-new-authority-key` containing the exact world UID in its UID directory. Existing records without their matching key still require restoration. Interrupted writes recover valid pending records or backups and retain damaged files as quarantine evidence.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Barter turns off for that session.
- Cheats: Barter 0.1.0 has none. A cheat added later would be controlled by the server's admins like the other Meadworks products.

## Replaces / compatibility

- Step-aside: with TradersExtended installed, Barter stays off and says so once. Barter reads TradersExtended list files, but not its per-trader settings files or its editor.
- Existing unsigned trader balances are preserved as migration evidence, but cannot become trusted balances automatically. A fresh authority ledger uses the configured initial balance.
- Still deferred: changing the coin item and buying several lots at once.
- Version 0.1.0 was tested in a hosted game and on a dedicated server with two clients. Not tested side by side with other trader mods.

## Compatibility & notes

- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean, including setting names and help. List-file comments stay English; translations await a native review.
- Listed under Thunderstore's AI Generated category; tested in-game.
- Based on: TradersExtended 2.0.5 by shudnal (Unlicense).
- Source: https://github.com/ExtraPotions/Meadworks/tree/main/Barter

## Changelog

See the Changelog tab (CHANGELOG.md).

## License

Code: GPL-3.0-or-later (`LICENSE-CODE.md`, `licenses/GPL-3.0.txt`). Original documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). Adopted public-domain source and its notice: `licenses/Unlicense-TradersExtended.txt`, `SOURCE_NOTICE.md`, `LICENSE_SCOPE.md`. Product author: expDARE.
