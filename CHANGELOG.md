# Changelog

## 1.1.0

- **Multiplayer: every player now needs the mod, and the same version.** A player without it can no longer join a
  game hosted with it, and a player with it cannot join a game hosted without it. StationeersLaunchPad refuses the
  join and names the mod and the versions involved. In 1.0.0 a host without the mod stopped every suit at 4053 kPa while a
  player with the mod saw about 22% and no warning, at the point their suit had stopped filtering and cooling.
- **Every player's HUD shows the host's real limit.** The host sends its `Enabled` and `SmartCanisterLimitPercent` to
  each player as they join and again whenever it changes, so the waste warnings and percentage no longer depend on
  each player's own settings matching the host's. A player's own settings count only in games they host or play
  alone.
- Needs a StationeersLaunchPad with mod networking. Without it the mod still works in single player and logs that
  multiplayer is off.

## 1.0.0

- First release. A smart gas canister in the suit's Waste Tank slot fills to `SmartCanisterLimitPercent` (default
  90) of its rating instead of the game's 4053 kPa; filtering, exhaled-gas dump, cooling, the suit's `Activate`
  state and the HUD waste warnings and percentage all follow. Any other canister keeps the game's limit.
  `Enabled` turns it off at once.
