using System.Text.Json;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The runtime keeps a table of the .NET exceptions it throws itself, each with the type it derives
/// from (<c>$eq.exceptions.bases</c>), and a typed catch over one of them reads its chain from there.
/// A base written wrong is a clause that takes what .NET's would not, or lets through what it would,
/// with nothing to say so; this reads the table out of the served bundle and compares every entry
/// with .NET's own type.
/// </summary>
public class RuntimeExceptionHierarchyTests
{
    [SkippableFact]
    public void EveryExceptionTheRuntimeThrows_DerivesFromWhatDotNetsDoes()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        var url = ConformanceRunner.RuntimeJsUrl()
            ?? throw new InvalidOperationException("Could not locate the bundled runtime.js.");
        var json = JsExecutor.Run($"import {{ $eq }} from '{url}';\nconsole.log(JSON.stringify($eq.exceptions.bases));");
        var table = JsonSerializer.Deserialize<Dictionary<string, string?>>(json)!;

        table.Should().ContainKey("System.Exception", "every chain ends at the root");
        foreach (var (name, declaredBase) in table)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(name))
                .FirstOrDefault(found => found is not null);
            type.Should().NotBeNull($"the runtime throws {name}, which must be a .NET type");
            typeof(Exception).IsAssignableFrom(type).Should().BeTrue($"{name} must be an exception");
            var dotNetBase = type == typeof(Exception) ? null : type!.BaseType!.FullName;
            declaredBase.Should().Be(dotNetBase, $"the runtime's table says what {name} derives from");
        }
    }
}
