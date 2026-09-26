using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SmartWasteCanister;

/// <summary>
/// A suit with a smart gas canister in its Waste Tank slot keeps filtering, dumping and cooling until the canister
/// reaches a share of its own rating, instead of stopping at the game's fixed 4053 kPa.
/// </summary>
[BepInPlugin(pluginGuid, pluginName, pluginVersion)]
public class SmartWasteCanisterPlugin : BaseUnityPlugin
{
    public const string pluginGuid = "net.xceled.stationeers.smartwastecanister";
    public const string pluginName = "SmartWasteCanister";
    public const string pluginVersion = "1.1.0";

    internal static ManualLogSource Log { get; private set; }

    private ConfigEntry<bool> _enabled;
    private ConfigEntry<int> _limitPercent;

    private void Awake()
    {
        Log = Logger;
        _enabled = Config.Bind("General", "Enabled", true,
            "Let a smart gas canister in the suit's Waste Tank slot fill past the game's 4053 kPa. " +
            "Off gives every canister the game's limit again.");
        _limitPercent = Config.Bind("General", "SmartCanisterLimitPercent", FillShare.DefaultPercent,
            new ConfigDescription(
                "The suit stops filling a smart waste canister at this percentage of the canister's rated pressure " +
                "(20265 kPa, where it starts to burst). 90 stops at 18239 kPa, about 2 MPa under the burst point even in " +
                "vacuum. The game's own 4053 kPa is 40% of a plain canister.",
                new AcceptableValueRange<int>(FillShare.MinPercent, FillShare.MaxPercent)));

        _enabled.SettingChanged += OnSettingChanged;
        _limitPercent.SettingChanged += OnSettingChanged;
        PublishPolicy();
        PolicySync.Register(pluginName, pluginVersion);

        Harmony harmony = new(pluginGuid);
        if (!TryPatch(harmony, typeof(SuitTickPatch)))
        {
            _enabled.SettingChanged -= OnSettingChanged;
            _limitPercent.SettingChanged -= OnSettingChanged;
            WasteLimit.Local = VanillaLimit.Instance;
            PolicySync.LocalPolicyChanged();
            Logger.LogError($"{pluginName} {pluginVersion}: the suit patch failed, so the mod does nothing.");
            return;
        }

        if (!TryPatch(harmony, typeof(HudPatch)))
        {
            Logger.LogWarning("The HUD patch failed: suits still fill as set, but on a multiplayer client the waste " +
                "warnings and percentage measure against the game's limit.");
        }

        Logger.LogInfo($"{pluginName} {pluginVersion} loaded: {Describe(WasteLimit.Local)}.");
    }

    private void OnSettingChanged(object sender, EventArgs e)
    {
        PublishPolicy();
        Logger.LogInfo($"Now {Describe(WasteLimit.Local)}.");
    }

    private void PublishPolicy()
    {
        WasteLimit.Local = _enabled.Value
            ? new SmartCanisterLimit(FillShare.FromPercent(_limitPercent.Value))
            : VanillaLimit.Instance;
        PolicySync.LocalPolicyChanged();
    }

    private bool TryPatch(Harmony harmony, Type patch)
    {
        try
        {
            harmony.CreateClassProcessor(patch).Patch();
            return true;
        }
        catch (Exception e)
        {
            Logger.LogError($"Patch {patch.Name} failed: {e}");
            return false;
        }
    }

    internal static string Describe(WastePolicy policy) => policy switch
    {
        SmartCanisterLimit smart => $"smart waste canisters fill to {smart.Share.Percent}% of their rating",
        VanillaLimit => "off, every waste canister keeps the game's limit",
        _ => policy.ToString(),
    };
}
