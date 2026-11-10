using eQuantic.UI.Primitives;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using eQuantic.UI.Web;

namespace eQuantic.UI.Server;

/// <summary>
/// Default implementation of IServerActionRegistry.
/// </summary>
public class ServerActionRegistry : IServerActionRegistry
{
    private readonly Dictionary<string, ServerActionDescriptor> _actions = new();
    
    /// <summary>
    /// Registers an action under its id. A second method under an id already held stops the app at start,
    /// naming both: the registry kept the last one without a word, and a call ran the other method under
    /// the other method's authorization (#1290). The same method registered twice is one action.
    /// </summary>
    public void RegisterAction(string actionId, ServerActionDescriptor descriptor)
    {
        if (_actions.TryGetValue(actionId, out var held) && held.Method != descriptor.Method)
            throw new InvalidOperationException(
                $"Two Server Actions answer to '{actionId}': {Describe(held)} and {Describe(descriptor)}. " +
                "Name one of them apart with [ServerAction(Name = \"...\")].");
        _actions[actionId] = descriptor;
    }

    private static string Describe(ServerActionDescriptor descriptor) =>
        $"{descriptor.ComponentType.FullName}.{descriptor.Method.Name}({string.Join(", ", descriptor.Method.GetParameters().Select(parameter => parameter.ParameterType.Name))})";
    
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
                // The component's CLR full name, which eqc reads off the same declaration
                // (ComponentDefinition.TypeIdentity): by its simple name, two components of one name in
                // two namespaces shared one id, as two pages shared one route before #514 (#1290).
                var actionId = $"{ComponentIdentity.Of(type)}/{attr.Name ?? method.Name}";
                
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
