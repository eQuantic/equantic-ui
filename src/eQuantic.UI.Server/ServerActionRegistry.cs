using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using eQuantic.UI.Core;

namespace eQuantic.UI.Server;

/// <summary>
/// Default implementation of IServerActionRegistry.
/// </summary>
public class ServerActionRegistry : IServerActionRegistry
{
    private readonly Dictionary<string, ServerActionDescriptor> _actions = new();
    
    public void RegisterAction(string actionId, ServerActionDescriptor descriptor)
    {
        _actions[actionId] = descriptor;
    }
    
    public ServerActionDescriptor? GetAction(string actionId)
    {
        return _actions.TryGetValue(actionId, out var descriptor) ? descriptor : null;
    }
    
    public IEnumerable<ServerActionDescriptor> GetAllActions()
    {
        return _actions.Values;
    }
    
    public void ScanAssembly(Assembly assembly)
    {
        // Every component is a write-once one, so one base answers for all of them. This used to
        // test the Core split shape too, and the first real action on a write-once page answered
        // "Action not found" because the scan only knew those.
        var componentTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                   typeof(Primitives.UiComponent).IsAssignableFrom(t));
        
        foreach (var type in componentTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<ServerActionAttribute>() != null);
            
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<ServerActionAttribute>()!;
                var actionId = $"{type.Name}/{attr.Name ?? method.Name}";
                
                var descriptor = new ServerActionDescriptor
                {
                    ActionId = actionId,
                    ComponentType = type,
                    Method = method,
                    ParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray(),
                    IsAsync = method.ReturnType.IsAssignableTo(typeof(Task))
                };
                
                RegisterAction(actionId, descriptor);
            }
        }
    }
}
