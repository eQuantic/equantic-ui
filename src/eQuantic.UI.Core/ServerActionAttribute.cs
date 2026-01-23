using System;

namespace eQuantic.UI.Core;

/// <summary>
/// Marks a method as a Server Action that executes on the server.
/// The method will be invocable from the client via the Server Actions bridge.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ServerActionAttribute : Attribute
{
    /// <summary>
    /// Optional custom action name. Defaults to method name.
    /// </summary>
    public string? Name { get; set; }
}
