using System.Text.RegularExpressions;
using eQuantic.UI.Compiler.CodeGen.Strategies;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Every user-defined conversion a VOCABULARY type declares either crosses into the twin as a call
/// — eqc calls the static the runtime's twin carries, <c>IconGlyph.fromIcons('search')</c> — or says
/// why its twin takes the operand as it is, with <c>[ConversionPassesThrough]</c> (#281). Neither
/// half may be implicit: a crossing conversion whose static the twin lacks is a call to nothing, and
/// a pass-through without a reason is the silent default this replaced.
/// <para>
/// The set is DERIVED, never listed: every conversion operator on every type in a namespace the
/// runtime provides, read by Roslyn from the assemblies this test loads, named by the compiler's
/// own rule (<see cref="UserDefinedOperators.ConversionName"/>). The runtime's <c>tsc</c> already
/// fails on a conversion a shared component USES; this one covers the conversion nothing uses yet.
/// </para>
/// </summary>
public class VocabularyConversionTests
{
    [Fact]
    public void EveryVocabularyConversion_CrossesToAStaticOfItsTwin_OrSaysWhyItPassesThrough()
    {
        var conversions = VocabularyConversions().ToList();
        conversions.Should().Contain(c => c.Type.Name == "IconGlyph",
            "the scan must reach the vocabulary at all, or every check below passes on nothing");

        var twins = RuntimeTwinSources();
        var failures = new List<string>();
        foreach (var (type, conversion) in conversions)
        {
            if (!UserDefinedOperators.ConversionCrosses(conversion))
            {
                var reason = conversion.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "ConversionPassesThroughAttribute")
                    ?.ConstructorArguments.FirstOrDefault().Value as string;
                if (string.IsNullOrWhiteSpace(reason))
                    failures.Add($"{type.Name}: {conversion} passes through without saying why");
                continue;
            }

            var name = UserDefinedOperators.ConversionName(conversion);
            if (!TwinDeclaresStatic(twins, type.Name, name))
                failures.Add($"{type.Name}: {conversion} crosses as `{type.Name}.{name}(…)`, and no twin of {type.Name} declares `static {name}(`");
        }

        failures.Should().BeEmpty();
    }

    /// <summary>Every conversion operator of every public type in a runtime-provided namespace, from
    /// the vocabulary assemblies in this test's output — the assemblies that reference Primitives.</summary>
    private static IEnumerable<(INamedTypeSymbol Type, IMethodSymbol Conversion)> VocabularyConversions()
    {
        var directory = AppContext.BaseDirectory;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Concat(Directory.GetFiles(directory, "eQuantic.UI.*.dll"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        var compilation = CSharpCompilation.Create("VocabularyConversions", references: references);

        foreach (var assembly in compilation.References
                     .Select(compilation.GetAssemblyOrModuleSymbol)
                     .OfType<IAssemblySymbol>()
                     .Where(a => a.Name.StartsWith("eQuantic.UI.", StringComparison.Ordinal)))
        {
            foreach (var type in Types(assembly.GlobalNamespace))
            {
                if (type.DeclaredAccessibility != Accessibility.Public) continue;
                if (!RuntimeProvidedTypeScanner.IsRuntimeProvidedNamespace(type.ContainingNamespace.ToDisplayString())) continue;
                foreach (var conversion in type.GetMembers().OfType<IMethodSymbol>()
                             .Where(m => m.MethodKind == MethodKind.Conversion))
                    yield return (type, conversion);
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> Types(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers()) yield return type;
        foreach (var child in ns.GetNamespaceMembers())
            foreach (var type in Types(child)) yield return type;
    }

    /// <summary>The runtime's hand-written and transpiled TypeScript, specs excluded.</summary>
    private static List<string> RuntimeTwinSources()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var src = Path.Combine(root, "src", "eQuantic.UI.Runtime", "src");
        return Directory.GetFiles(src, "*.ts", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".spec.ts", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToList();
    }

    /// <summary>Whether some twin's <c>class {type}</c> body declares <c>static {name}(</c> — the body
    /// running from the class to the next top-level declaration.</summary>
    private static bool TwinDeclaresStatic(IEnumerable<string> sources, string type, string name)
    {
        var head = new Regex($@"^export (?:abstract )?class {Regex.Escape(type)}\b", RegexOptions.Multiline);
        var next = new Regex(@"^(?:export |class |const |function )", RegexOptions.Multiline);
        foreach (var source in sources)
        {
            foreach (Match match in head.Matches(source))
            {
                var bodyStart = match.Index + match.Length;
                var end = next.Match(source, bodyStart);
                var body = source[bodyStart..(end.Success ? end.Index : source.Length)];
                if (body.Contains($"static {name}(", StringComparison.Ordinal)) return true;
            }
        }
        return false;
    }
}
