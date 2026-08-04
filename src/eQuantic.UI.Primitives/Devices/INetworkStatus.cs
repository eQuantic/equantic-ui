namespace eQuantic.UI.Primitives;

/// <summary>What the device is talking through right now.</summary>
public enum NetworkKind
{
    /// <summary>No route out at all.</summary>
    None,
    Wifi,
    /// <summary>The metered one — an app that syncs a gigabyte should ask first.</summary>
    Cellular,
    /// <summary>Ethernet. Desktops mostly, and docks.</summary>
    Wired,
    /// <summary>Connected through something the platform did not name (VPN-only paths, exotic
    /// interfaces). Online is still true — the kind is just not worth guessing at.</summary>
    Other,
}

/// <summary>One reading of the network: reachable or not, and through what.</summary>
/// <param name="Online">Whether anything can be reached AT ALL. This is reachability as the
/// platform reports it, not a promise a specific host will answer.</param>
/// <param name="Kind">What carries the traffic — the input to "should I download this here".</param>
public readonly record struct NetworkState(bool Online, NetworkKind Kind)
{
    public static readonly NetworkState Offline = new(false, NetworkKind.None);
}

/// <summary>
/// The network's CURRENT state, and the stream of its changes — the first capability whose answer
/// does not hold still. A photo picker answers once; the network answers now and then corrects
/// itself when the train enters the tunnel, and an app showing a stale "online" is worse than one
/// showing none.
/// <para>
/// The subscription is an <see cref="IDisposable"/> rather than a .NET event, deliberately: a
/// component is torn down and rebuilt all the time, and an event subscription has no end you can
/// see — it keeps the dead component alive from the event's side. A disposable ends where the
/// component ends, which is the same contract every realizer's own resources follow.
/// </para>
/// <para>
/// The callback may arrive on ANY thread — the platform's monitor owns that. Realizations hand it
/// to the app as-is; a component's SetState already marshals through the host's invalidation.
/// </para>
/// </summary>
public interface INetworkStatus
{
    /// <summary>The latest reading. Never blocks: it answers from the monitor's cache, which is
    /// what makes it safe to read in a Build.</summary>
    NetworkState Current { get; }

    /// <summary>
    /// Starts listening. The callback fires for every CHANGE from then on — not for the current
    /// state, which <see cref="Current"/> already answers without waiting. Disposing stops it;
    /// disposing twice is fine.
    /// </summary>
    IDisposable Subscribe(Action<NetworkState> onChanged);
}
