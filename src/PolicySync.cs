using System;
using System.Runtime.CompilerServices;
using Assets.Scripts.Networking;
using LaunchPadBooster;
using LaunchPadBooster.Networking;

namespace SmartWasteCanister;

/// <summary>
/// Multiplayer: the host fills every suit, so every client's HUD must measure against the host's policy. The host
/// announces it in the join data of each joining player and as a message to every connected player whenever its
/// setting changes. Nothing is sent per tick.
///
/// Registered with StationeersLaunchPad's LaunchPadBooster as required, with LaunchPadBooster's exact version check:
/// a player whose copy is missing or a different version is refused at join ("Incompatible mods"), in both
/// directions, so every peer reads these bytes the same way.
/// </summary>
internal static class PolicySync
{
    private static readonly object Sync = new();

    // Tells this host's announcements apart from those of a host this machine joined earlier.
    private static readonly int Session = new Random().Next(1, int.MaxValue);

    private static int _revision;
    private static bool _received;
    private static int _receivedSession;
    private static int _receivedRevision;

    /// <summary>True once registered with LaunchPadBooster; nothing is sent or read otherwise.</summary>
    public static bool Active { get; private set; }

    public static void Register(string name, string version)
    {
        try
        {
            RegisterWithBooster(name, version);
            Active = true;
        }
        catch (Exception e)
        {
            SmartWasteCanisterPlugin.Log.LogWarning("Multiplayer sync is off: this StationeersLaunchPad has no mod " +
                "networking. Single player is unaffected; players running the mod will refuse this game at join. " +
                e.Message);
        }
    }

    /// <summary>The host's current announcement, from its own settings.</summary>
    public static PolicyAnnouncement Current()
    {
        lock (Sync)
        {
            return new PolicyAnnouncement(Session, _revision, WasteLimit.Local);
        }
    }

    /// <summary>Called whenever this machine's own policy changes; sends it on when hosting a multiplayer game.</summary>
    public static void LocalPolicyChanged()
    {
        lock (Sync)
        {
            _revision++;
        }

        if (!Active || !NetworkManager.IsServer)
        {
            return;
        }

        try
        {
            SendToAll(Current());
        }
        catch (Exception e)
        {
            SmartWasteCanisterPlugin.Log.LogWarning($"Could not send the new setting to the other players: {e.Message}");
        }
    }

    /// <summary>Reads an announcement; a payload that cannot be read is logged and ignored.</summary>
    public static bool TryRead(RocketBinaryReader reader, out PolicyAnnouncement announcement)
    {
        try
        {
            announcement = PolicyAnnouncement.Read(reader);
            return true;
        }
        catch (Exception e)
        {
            SmartWasteCanisterPlugin.Log.LogWarning($"Could not read the host's setting; the HUD keeps its limit. {e.Message}");
            announcement = default;
            return false;
        }
    }

    /// <summary>
    /// A client takes the host's announcement. The join data and a change message can arrive in either order, so an
    /// older revision from the same host never replaces a newer one.
    /// </summary>
    public static void Receive(PolicyAnnouncement announcement)
    {
        if (!NetworkManager.IsClient)
        {
            return;
        }

        lock (Sync)
        {
            if (_received && announcement.Session == _receivedSession && announcement.Revision < _receivedRevision)
            {
                return;
            }

            _received = true;
            _receivedSession = announcement.Session;
            _receivedRevision = announcement.Revision;
        }

        WasteLimit.Host = announcement.Policy;
        SmartWasteCanisterPlugin.Log.LogInfo(
            $"The host's setting: {SmartWasteCanisterPlugin.Describe(announcement.Policy)}.");
    }

    // Its own method, never inlined, so a missing or older LaunchPadBooster fails inside Register's try.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegisterWithBooster(string name, string version)
    {
        Mod mod = new(name, version);
        mod.Networking.Required = true;
        mod.Networking.JoinSuffixSerializer = new PolicyJoinSection();
        mod.Networking.RegisterMessage<PolicyMessage>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SendToAll(PolicyAnnouncement announcement) =>
        new PolicyMessage(announcement).SendAll(-1L);
}

/// <summary>What the host sends: 9 bytes.</summary>
internal readonly struct PolicyAnnouncement(int session, int revision, WastePolicy policy)
{
    private const byte Off = 0;

    public int Session { get; } = session;

    public int Revision { get; } = revision;

    public WastePolicy Policy { get; } = policy;

    public void Write(RocketBinaryWriter writer)
    {
        writer.WriteInt32(Session);
        writer.WriteInt32(Revision);
        writer.WriteByte(PercentOf(Policy));
    }

    public static PolicyAnnouncement Read(RocketBinaryReader reader)
    {
        int session = reader.ReadInt32();
        int revision = reader.ReadInt32();
        return new PolicyAnnouncement(session, revision, PolicyOf(reader.ReadByte()));
    }

    private static byte PercentOf(WastePolicy policy) => policy switch
    {
        SmartCanisterLimit smart => (byte)smart.Share.Percent,
        VanillaLimit => Off,
        _ => throw new InvalidOperationException($"No wire form for {policy}"),
    };

    // FillShare's percentages start at 40, so 0 is free to mean "off".
    private static WastePolicy PolicyOf(byte percent) =>
        percent == Off ? VanillaLimit.Instance : new SmartCanisterLimit(FillShare.FromPercent(percent));
}

/// <summary>The host's policy in a joining player's join data.</summary>
public sealed class PolicyJoinSection : IJoinSuffixSerializer
{
    public void SerializeJoinSuffix(RocketBinaryWriter writer) => PolicySync.Current().Write(writer);

    public void DeserializeJoinSuffix(RocketBinaryReader reader)
    {
        if (PolicySync.TryRead(reader, out PolicyAnnouncement announcement))
        {
            PolicySync.Receive(announcement);
        }
    }
}

/// <summary>The host's policy after a change, to every connected player.</summary>
public sealed class PolicyMessage : INetworkMessage
{
    private PolicyAnnouncement _announcement;
    private bool _readable = true;

    public PolicyMessage()
    {
    }

    internal PolicyMessage(PolicyAnnouncement announcement) => _announcement = announcement;

    public void Serialize(RocketBinaryWriter writer) => _announcement.Write(writer);

    public void Deserialize(RocketBinaryReader reader) => _readable = PolicySync.TryRead(reader, out _announcement);

    public void Process(long clientId)
    {
        if (_readable)
        {
            PolicySync.Receive(_announcement);
        }
    }
}
