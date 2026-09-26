using System;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects.Clothing;

namespace SmartWasteCanister;

/// <summary>
/// Writes the active policy's limit into the suit's own <c>wasteMaxPressure</c> field. Every consumer in the game
/// (filtering and CO2 dump, cooling, the suit's Activate state, the HUD waste warnings and percentage) reads that
/// field, so one write covers them all. The game neither saves the field nor sends it over the network.
/// </summary>
internal static class WasteLimit
{
    private static volatile WastePolicy _local = VanillaLimit.Instance;
    private static volatile WastePolicy _host = VanillaLimit.Instance;
    private static bool _faultLogged;

    /// <summary>This machine's own settings: what single player, a host and a dedicated server fill to.</summary>
    public static WastePolicy Local
    {
        get => _local;
        set => _local = value;
    }

    /// <summary>
    /// The policy the host announced. The game's limit until an announcement arrives, so a client's HUD never reads
    /// calmer than the game's own.
    /// </summary>
    public static WastePolicy Host
    {
        get => _host;
        set => _host = value;
    }

    /// <summary>A multiplayer client fills nothing itself: its suit stops where the host's policy says.</summary>
    public static WastePolicy Effective => NetworkManager.IsClient ? _host : _local;

    /// <summary>Runs every atmospherics tick per worn suit and every HUD frame: no allocation.</summary>
    public static void Apply(Suit suit)
    {
        try
        {
            suit.wasteMaxPressure = Effective.LimitFor(suit.WasteTank, VanillaLimitOf(suit));
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
