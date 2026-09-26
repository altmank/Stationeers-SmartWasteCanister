# Changelog

## 1.0.0

- First release. A smart gas canister in the suit's Waste Tank slot fills to `SmartCanisterLimitPercent` (default
  90) of its rating instead of the game's 4053 kPa; filtering, exhaled-gas dump, cooling, the suit's `Activate`
  state and the HUD waste warnings and percentage all follow. Any other canister keeps the game's limit.
  `Enabled` turns it off at once.
