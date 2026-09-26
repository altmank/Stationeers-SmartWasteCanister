# Smart Waste Canister

A Stationeers mod: with a Smart Gas Canister in the suit's Waste Tank slot, the suit keeps filtering, dumping
exhaled gas and cooling until the canister reaches a share of its own rated pressure (default 75%, 15199 kPa),
instead of stopping at the game's fixed 4053 kPa. Any other canister keeps the game's limit.

## What the game does

The suits in play (Hardsuit, Eva Suit, Emergency Eva Suit, Icarus) are class `Suit`, whose waste limit is one float
per suit, `Suit.wasteMaxPressure`, 4053 kPa from the prefab. It ignores the canister, so a smart canister (rated
20265 kPa) is treated like a plain one (rated 10132.5 kPa). Everything that goes by the limit reads that field:

| Consumer | Effect at the limit |
| --- | --- |
| `InternalAtmosphereConditioner.SetGasToTank` | no filtering, exhaled gas stays in the suit |
| `InternalAtmosphereConditioner.AirConditioning` | no cooling |
| `Suit.CheckActivateState` | suit state `Activate` drops to "cannot filter" (IC-readable) |
| `StatusUpdates.IsWasteCaution` / `IsWasteCritical` | HUD waste warnings at 75% / 95% of the limit |
| `StatusUpdates.HandleIconUpdates` | HUD waste percentage = canister pressure / limit |

The field is neither saved nor synced. The suit tick runs only where the simulation runs (single player, host,
dedicated server); the HUD reads the field on each player's own machine.

## What the mod does

- Two Harmony prefixes write the limit into `wasteMaxPressure` from the canister now in the slot: one on
  `Suit.OnAtmosphericTick` (the Hardsuit's `AdvancedSuit` reaches it through `base.OnAtmosphericTick()`), one on
  `StatusUpdates.HandleIconUpdates` for the local player's suit, so a multiplayer client's HUD agrees with the host.
  Writing the field rather than patching the `WasteMaxPressure` getter matters: the getter is a tiny non-virtual
  method the JIT can inline into its callers, where a patch would not reach.
- The limit is `max(game limit, share x canister rating)` for an intact smart gas canister (`GasCanisterWithDisplay`
  holding gas), and the game limit (read from the suit's prefab) for anything else. Removing a canister, swapping it,
  or turning the mod off takes effect on the next tick.
- Allocation-free on the tick and HUD paths. If writing the limit ever throws, the suit keeps its last limit and the
  error is logged once. If the suit patch cannot be applied the mod stays off.
- The `SuitBase` suit family in the code (`ItemSuitHard`, `ItemSuitNormal`, ... ) has no recipe and a different waste
  path that does not stop at the limit at all; it is left alone.

## Why 75% by default

A canister takes damage once |outside - inside| pressure reaches its rating for more than five ticks, and then
explodes, scaled by how full it is. In vacuum the whole inside pressure counts. The suit stops adding at the limit,
but a canister that warms afterwards keeps rising, so the default leaves a third of headroom. 95% is the ceiling.

## Settings (BepInEx config `net.xceled.stationeers.smartwastecanister.cfg`)

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | true | Off gives every canister the game's 4053 kPa. |
| `SmartCanisterLimitPercent` | 75 | Share of a smart canister's rating the suit fills it to, 40 to 95. |

Both apply at once.

## Multiplayer

The host or dedicated server decides the fill, so it needs the mod. Players with the mod see matching HUD warnings;
a player without it sees the game's, i.e. over 100% once a smart canister passes 4053 kPa.

## Build

Needs Stationeers with BepInEx 5.4 and [StationeersLaunchPad](https://github.com/StationeersLaunchPad/StationeersLaunchPad).
`.\build.ps1` builds and stages `package\`; `.\build.ps1 -Deploy` also copies it into the local mods folder
(close the game first).
