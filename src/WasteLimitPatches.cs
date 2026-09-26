using Assets.Scripts.Objects.Clothing;
using Assets.Scripts.Objects.Entities;
using Assets.Scripts.UI;
using HarmonyLib;

namespace SmartWasteCanister;

/// <summary>
/// Where the simulation runs (single player, host, dedicated server): before the suit runs its filters, cooling and
/// state check. <c>AdvancedSuit</c> (the Hardsuit) reaches it through <c>base.OnAtmosphericTick()</c>.
/// </summary>
[HarmonyPatch(typeof(Suit), nameof(Suit.OnAtmosphericTick))]
internal static class SuitTickPatch
{
    [HarmonyPrefix]
    private static void Prefix(Suit __instance) => WasteLimit.Apply(__instance);
}

/// <summary>
/// On every machine, for the local player's suit: before the HUD reads the limit for the waste warnings and
/// percentage. A multiplayer client does not run the suit's tick, so its HUD would otherwise use the game's limit.
/// </summary>
[HarmonyPatch(typeof(StatusUpdates), "HandleIconUpdates")]
internal static class HudPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        if (StatusUpdates.Parent is Human { Suit: Suit suit })
        {
            WasteLimit.Apply(suit);
        }
    }
}
