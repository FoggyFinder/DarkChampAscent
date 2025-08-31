# Dark Champ Ascent

Dark Champ Ascent - discord game made by and for [Dark Coin](https://dark-coin.io/) community.
[Discord](https://discord.com/invite/xdZ6V5ybmq)

----

1. [Terms of Service](#tos-terms-of-service)
2. [Requirements](#Requirements)
3. [Basics](#Basics)
4. [Monsters](#Monsters)
5. [Tokenomics](#Tokenomics)
6. [Wallets](#Wallets)
7. [Shop](#Shop)
8. [Premium features](#Premium-features)
9. [Commands](#Commands)

## TOS (Terms of Service)

**The Application is provided "as is" without any warranties or guarantees.**

---

### Requirements

* Algorand wallet.
* At least 1 NFT from [Dark Coin Champions](https://www.asalytic.app/collection/dark-coin-champions) collection.

**Early stage of development. All numbers are subject to change.**

---

### Basics

There are 8 characteristics:

* Health
* Magic
* Accuracy - increases chance to take damage
* Luck - increases chance to hit critical
* Attack 
* MagicAttack
* Defense
* MagicDefense

In a round champ can perform one of 6 actions:

* Attack - takes damage
* MagicAttack - takes damage from the enemy but reduces magic as well
* Shield - increase defense for 1 round
* MagicShield - increase magic defense for 1 round but reduces magic
* Heal - increase health but takes magic
* Meditate - increases magic

### Monsters

Monsters are enemies in a battle. There is only one monster for each battle selected randomly.
Currently there are 3 class of monsters

* Zombie
* Demon
* Necromancer
    
and 3 subclasses:

* None
* Fire
* Frost

Main difference between them is different basic stats and logic to attack, level up, ..

### Tokenomics

All Darkcoins from donation or purchases (items or premium features like changing default Champ name to something else) go
to total rewards pool.

__Price for all items, premium features are settled in USDC. DarkCoin price updates periodically every few hours.__

Rewards for specific round is calculated based on total amount as:


```
Window = 90
RoundsInBattle = 96

BattleReward = InGameRewardsPool / Window
RoundReward = BattleReward / RoundsInBattle
```

Relatively big Window ensures that rewards are slowly reducing with each next battle if reward pool is not topped up.

Simplified example to illustrate approach:

Battle| Total RewardsPool|Battle Rewards | Round Rewards
-----|---|------|-----
1 | 9000 | 100 | 1,041667
2 | 8900 | 98,888889 | 1,030093
3 | 8801,111111 | 97,790123 | 1,018647
4 | 8703,320988 | 96,703567 | 1,007329
5 | 8606,617421 | 95,629082 | 0,996136
6 | 8510,988339 | 94,566537 | 0,985068
7 | 8416,421802 | 93,515798 | 0,974123
8 | 8322,906004 | 92,476733 | 0,963299
9 | 8230,429271 | 91,449214 | 0,952596
10 | 8138,980057 | 90,433112 | 0,942012
...| ... |  ... | ...
97 | 3078,947201 | 34,210524 | 0,356360
98 | 3044,736677 | 33,830408 | 0,352400
99 | 3010,906269 | 33,454514 | 0,348485
100 | 2977,451755 | 33,082797 | 0,344612

...


For each round rewards are splitted by following logic:


```
84% - champ's rewards
7% - dao
5% - to devs
3% - reserve
1% - burn
```

with rewards for players splitted as

```
12% - Move.Shield
12% - Move.MagicShield
12% - Move.Heal
12% - Move.Meditate
6%  - Move.Attack
6%  - Move.MagicAttack
24% - is splitted proportionally as (Damage / TotalDamage)
```

in case when no champs used a move those coins returned as rewards for next battles.

Earned DarkCoins are distributed in the end of the battle automatically.

### Wallets

For success of DarkCoin it's important that bits of revenue (if any) go to DAO so it could operate independently.

* `DAO` - main DarkCoin DAO wallet.
* `Dev` - obvious from the name. No intention to sell until (if ever) collected amount would worth something.
* `Reserve` - for future use in the game or for the game.
* `Burn` - to make DarkCoin deflationary.


### Shop

Shop contains items that one can buy with DarkCoins. Some of them have instant effect while other are active only for few next rounds.

* ElixirOfLife
* ElixirOfMagic
* ElixirOfLuck
* ElixirOfAccuracy
* ElixirOfDamage
* ElixirOfMagicalDamage
* ElixirOfDefense
* ElixirOfMagicalDefense
* RevivalSpell

For example, after using `ElixirOfLife` or `ElixirOfMagic` you would notice that value for `Health`/`Magic` is increased.

### Premium features

* Custom name for a champ.

### Commands

#### Wallet commands

* `/wallet register` `address:` - to register wallet in the game.

After registration user have to confirm it by sending 0-cost tx to the game wallet with specified code as note.

* `/wallet gamewallets` - shows "special" wallets that used in game.

* `/wallet wallets ` - prints registered user's wallets

#### User's commands

* `/balance` - returns user's in-game balance
* `/shop` - prints available items to buy, their price and duration (rounds)
* `/buy` `item:` `amount:` - to buy specific item from the shop
* `/storage` - returns user's storage
* `/champs` - returns list of user's champs. Shows *current* stats for a champ without boosted or leveled bonuses.
* `/donate` `amount:` - to donate to in-game reward pool from balance

#### Champ's commands

* `/champ show` `assetid:` - returns info on arbitrary champ from collection
* `/champ card` - prints details for selected user's champ
* `/champ rename` `name:` - changes champ's name. Not free function.
* `/champ top10` - prints top-10 champs sorted by xp

#### Monster's commands

* `/monster showrandom` - returns info on random monster that is alive
* `/monster show` `mtype:` `msubtype:` - - returns info on selected monster

#### Battle's commands

* `/battle action` `action:` - performs specific action for selected champ

#### Leaderboard's commands

* `/top donaters` - returns list of top-10 biggest anonymous donaters
* `/top ingamedonaters` - returns list of top-10 biggest donaters from registered users