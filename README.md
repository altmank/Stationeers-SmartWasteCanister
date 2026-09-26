# Smart Waste Canister

A Stationeers mod: with a Smart Gas Canister in the suit's Waste Tank slot, the suit keeps filtering, dumping
exhaled gas and cooling until the canister reaches a share of its own rated pressure (default 90%, 18239 kPa),
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

## Why 90% by default

A canister takes damage once |outside - inside| pressure reaches its rating for more than five ticks, and then
explodes, scaled by how full it is. In vacuum the whole inside pressure counts, so the fill must stay under the
rating. Canisters in suit slots do not exchange heat, so a canister stays at the pressure the suit leaves it at: 90%
keeps about 2 MPa under the burst point even in vacuum. 95% is the ceiling.

## Settings (BepInEx config `net.xceled.stationeers.smartwastecanister.cfg`)

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | true | Off gives every canister the game's 4053 kPa. |
| `SmartCanisterLimitPercent` | 90 | Share of a smart canister's rating the suit fills it to, 40 to 95. |

Both apply at once.

## Multiplayer

**The host, or the dedicated server, must have the mod, and everyone should have it with the same settings.** There
is no version check between players: the mod sends nothing, so any mix can join.

- **The host decides how full every suit fills.** Filtering, dumping exhaled gas, cooling and the suit's status run
  only on the host's game, for every player's suit, using the host's `Enabled` and `SmartCanisterLimitPercent`.
- **Each player's game draws their own HUD.** The waste warnings and percentage are worked out on the player's own
  machine from their own copy of the suit, so a player with the mod gets them from their own settings.
- **A player without the mod** fills as far as the host allows, but their HUD measures against the game's 4053 kPa:
  over 100% and the full warning once a smart canister passes it. Nothing goes wrong beyond the warning.
- **A host without the mod** stops every suit at 4053 kPa. A player who has the mod then sees a HUD that is too
  calm: about 22% full (at the default 90%) at the point the suit stops filtering and cooling, with no warning.
  Switch `Enabled` off when joining a host that does not run the mod.
- **Different settings** on host and player make the player's HUD disagree with the host in the same way. Keep
  `Enabled` and `SmartCanisterLimitPercent` the same for everyone.
- Nothing is saved or sent, so joining a game in progress needs nothing special: the host sets the limit on its next
  tick and the player's HUD on its next frame.
- Worked out from the game's code rather than from a multiplayer session.

## Build

Needs Stationeers with BepInEx 5.4 and [StationeersLaunchPad](https://github.com/StationeersLaunchPad/StationeersLaunchPad).
`.\build.ps1` builds and stages `package\`; `.\build.ps1 -Deploy` also copies it into the local mods folder
(close the game first).
