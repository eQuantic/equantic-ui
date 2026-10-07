using System;

namespace eQuantic.UI.Primitives;

/// <summary>
/// What the server publishes, heard by a component: the capability a component takes in its
/// constructor, as it takes <see cref="INetworkStatus"/>, to subscribe to a <see cref="ServerTopic{T}"/>.
/// <para>
/// The page keeps one connection to the server for every topic its components subscribe to, opened
/// by the first subscription and closed with the last, and the server authorizes each topic before it
/// binds it to the connection. When the connection drops, it is opened again by itself and every
/// live subscription is made again; <see cref="OnConnectionChanged"/> tells the component, with the
/// id of the last event received, so it can ask the server for what it missed.
/// </para>
/// <para>
/// A subscription is an <see cref="IDisposable"/>, as the network's is: a component subscribes in
/// <c>OnMount</c> and disposes in <c>OnUnmount</c>. A target with no server to hear (server rendering)
/// answers with a capability that never connects, so a page that subscribes still renders there.
/// </para>
/// </summary>
public interface IServerEvents
{
    /// <summary>Where the connection stands now. Never blocks.</summary>
    ServerConnection Connection { get; }

    /// <summary>
    /// Subscribes to <paramref name="topic"/>: every payload the server publishes to it from now on
    /// reaches <paramref name="onEvent"/>, as a <typeparamref name="T"/>. Disposing stops it, and
    /// releases the topic on the server when nothing else on the page holds it; disposing twice is
    /// fine.
    /// </summary>
    /// <param name="topic">The topic.</param>
    /// <param name="onEvent">Called with each payload.</param>
    /// <param name="onRefused">Called once if the server does not bind the topic, with why; the
    /// subscription then receives nothing.</param>
    IDisposable Subscribe<T>(ServerTopic<T> topic, Action<T> onEvent, Action<ServerTopicRefusal>? onRefused = null);

    /// <summary>
    /// Listens to the connection's changes: every change from now on, not the current state, which
    /// <see cref="Connection"/> answers. Disposing stops it.
    /// </summary>
    IDisposable OnConnectionChanged(Action<ServerConnection> onChanged);
}
