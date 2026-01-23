using System;
using System.Collections.Generic;

namespace eQuantic.UI.Server;

/// <summary>
/// Registry for server actions - discovers and stores action metadata.
/// </summary>
public interface IServerActionRegistry
{
    /// <summary>
    /// Register an action from a component type.
    /// </summary>
    void RegisterAction(string actionId, ServerActionDescriptor descriptor);
    
    /// <summary>
    /// Get an action descriptor by ID.
    /// </summary>
    ServerActionDescriptor? GetAction(string actionId);
    
    /// <summary>
    /// Get all registered actions.
    /// </summary>
    IEnumerable<ServerActionDescriptor> GetAllActions();
    
    /// <summary>
    /// Scan an assembly for components with [ServerAction] methods.
    /// </summary>
    void ScanAssembly(System.Reflection.Assembly assembly);
}

/// <summary>
/// Describes a server action.
/// </summary>
public class ServerActionDescriptor
{
    /// <summary>
    /// Unique action identifier (e.g., "Counter/Increment").
    /// </summary>
    public string ActionId { get; set; } = string.Empty;
    
    /// <summary>
    /// The component type that contains this action.
    /// </summary>
    public Type ComponentType { get; set; } = null!;
    
    /// <summary>
    /// The method info for invoking the action.
    /// </summary>
    public System.Reflection.MethodInfo Method { get; set; } = null!;
    
    /// <summary>
    /// Parameter types for the action.
    /// </summary>
    public Type[] ParameterTypes { get; set; } = Array.Empty<Type>();
    
    /// <summary>
    /// Whether the action is async.
    /// </summary>
    public bool IsAsync { get; set; }
}
