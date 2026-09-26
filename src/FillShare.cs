using System;
using Assets.Scripts.Atmospherics;

namespace SmartWasteCanister;

/// <summary>How full the suit may fill a smart canister, as a share of the canister's rated (burst) pressure.</summary>
internal readonly record struct FillShare
{
    public const int MinPercent = 40;
    public const int MaxPercent = 95;
    public const int DefaultPercent = 90;

    private readonly float _ratio;

    private FillShare(float ratio) => _ratio = ratio;

    public int Percent => (int)Math.Round(_ratio * 100f);

    /// <summary>Clamped to <see cref="MinPercent"/>..<see cref="MaxPercent"/>: the suit never fills to the burst point.</summary>
    public static FillShare FromPercent(int percent) =>
        new(Math.Min(Math.Max(percent, MinPercent), MaxPercent) / 100f);

    public float Of(PressurekPa rated) => rated.ToFloat() * _ratio;
}
