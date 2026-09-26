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
  `StatusUpdates.HandleIconUpdates` for the local player's suit. On a multiplayer client that one uses the policy the
  host sent (see *Multiplayer*), so the client's HUD agrees with the host.
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

**Every player needs the mod, and the same version.** The mod registers with StationeersLaunchPad's multiplayer
check as required: a player without it cannot join a game hosted with it, a player with it cannot join a game hosted
without it, and a different version is refused as well. The refusal names the mod and the versions involved.

- **The host decides how full every suit fills.** Filtering, dumping exhaled gas, cooling and the suit's status run
  only on the host's game, or the dedicated server, for every player's suit, using the host's `Enabled` and
  `SmartCanisterLimitPercent`.
- **Every player's HUD shows the host's real limit.** The host sends its setting to each player as they join, and to
  everyone again whenever it changes: 9 bytes, nothing per tick. A joining player's waste warnings and percentage then
  measure against the host's limit, whatever that player's own settings say. Until the host's setting has arrived,
  the HUD measures against the game's 4053 kPa, so it never reads calmer than the game's own.
- A player's own `Enabled` and `SmartCanisterLimitPercent` matter only in games they host or play alone.
- Worked out from the game's and StationeersLaunchPad's code rather than from a multiplayer session.

## Build

Needs Stationeers with BepInEx 5.4 and [StationeersLaunchPad](https://github.com/StationeersLaunchPad/StationeersLaunchPad).
`.\build.ps1` builds and stages `package\`; `.\build.ps1 -Deploy` also copies it into the local mods folder
(close the game first).
