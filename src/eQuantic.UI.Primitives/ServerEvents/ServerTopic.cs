using System;

namespace eQuantic.UI.Primitives;

/// <summary>
/// A topic the server publishes to and a component subscribes to: its name, and the type of what it
/// carries. The app declares its topics once, in its own C#, and the component that subscribes and
/// the server code that publishes use the same value, so the name and the payload's type cannot
/// drift apart: <c>static ServerTopic&lt;RoomEvent&gt; Room(string id) =&gt; new($"room:{id}")</c>.
/// <para>
/// The name is the app's own, with no convention imposed: the server authorizes it against the
/// templates the app configures, and two topics are the same topic when their names are equal.
/// </para>
/// </summary>
/// <typeparam name="T">What the topic carries. Its values cross as JSON and are revived in the
/// browser as a Server Action's result is, so a record, an enum, a decimal or a long read as they
/// read in C#.</typeparam>
public sealed record ServerTopic<[HydratesTypeArgument] T>
{
    /// <summary>A topic named <paramref name="name"/>.</summary>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    public ServerTopic(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <summary>The topic's name, as the server authorizes and routes it.</summary>
    public string Name { get; }

    /// <inheritdoc />
    public override string ToString() => Name;
}
