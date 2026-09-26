using System;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Objects.Pipes;

namespace SmartWasteCanister;

/// <summary>Decides the waste pressure at which a suit stops filtering, dumping and cooling into its waste canister.</summary>
internal abstract record WastePolicy
{
    /// <param name="wasteTank">The canister in the suit's Waste Tank slot, or null.</param>
    /// <param name="vanillaLimit">The suit prefab's own limit in kPa (4053 for every suit in the game).</param>
    public abstract float LimitFor(GasCanister wasteTank, float vanillaLimit);
}

/// <summary>The game's fixed limit, whatever the canister.</summary>
internal sealed record VanillaLimit : WastePolicy
{
    public static readonly VanillaLimit Instance = new();

    private VanillaLimit() { }

    public override float LimitFor(GasCanister wasteTank, float vanillaLimit) => vanillaLimit;
}

/// <summary>
/// An intact smart gas canister is filled to a share of its own rating; anything else keeps the game's limit.
/// The result never drops below the game's limit.
/// </summary>
internal sealed record SmartCanisterLimit(FillShare Share) : WastePolicy
{
    public override float LimitFor(GasCanister wasteTank, float vanillaLimit) =>
        IsIntactSmartGasCanister(wasteTank)
            ? Math.Max(vanillaLimit, Share.Of(wasteTank.MaxPressure))
            : vanillaLimit;

    private static bool IsIntactSmartGasCanister(GasCanister canister) =>
        canister is GasCanisterWithDisplay smart
        && smart != null
        && smart.CanisterContentType == Pipe.ContentType.Gas
        && !smart.IsBroken;
}
