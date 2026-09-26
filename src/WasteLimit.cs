using System;
using Assets.Scripts.Objects.Clothing;

namespace SmartWasteCanister;

/// <summary>
/// Writes the active policy's limit into the suit's own <c>wasteMaxPressure</c> field. Every consumer in the game
/// (filtering and CO2 dump, cooling, the suit's Activate state, the HUD waste warnings and percentage) reads that
/// field, so one write covers them all. The game neither saves the field nor sends it over the network.
/// </summary>
internal static class WasteLimit
{
    private static volatile WastePolicy _policy = VanillaLimit.Instance;
    private static bool _faultLogged;

    public static WastePolicy Policy
    {
        get => _policy;
        set => _policy = value;
    }

    /// <summary>Runs every atmospherics tick per worn suit and every HUD frame: no allocation.</summary>
    public static void Apply(Suit suit)
    {
        try
        {
            suit.wasteMaxPressure = _policy.LimitFor(suit.WasteTank, VanillaLimitOf(suit));
        }
        catch (Exception e)
        {
            // The field keeps its last value; the game's own tick still runs.
            if (!_faultLogged)
            {
                _faultLogged = true;
                SmartWasteCanisterPlugin.Log?.LogError($"Could not set a suit's waste limit; it keeps its current one. {e}");
            }
        }
    }

    private static float VanillaLimitOf(Suit suit) =>
        suit.SourcePrefab is Suit prefab && !ReferenceEquals(prefab, suit)
            ? prefab.wasteMaxPressure
            : Suit.DEFAULT_MAX_WASTE_PRESSURE;
}
