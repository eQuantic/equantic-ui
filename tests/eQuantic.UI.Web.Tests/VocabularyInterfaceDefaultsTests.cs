using System.Reflection;
using System.Runtime.CompilerServices;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Every default member of the vocabulary's interfaces has a copy in the runtime, cross-pinned.
/// <para>
/// An app compiles against the vocabulary's ASSEMBLIES, where an interface has its signature and
/// not its body, so the twin of an app's theme, language or completion provider that relies on a
/// default delegates to the runtime's copy in <c>interface-defaults.ts</c> (#414). A default that
/// copy lacks is a member the app's twin answers undefined for, which is how every plain-text
/// <c>CodeBlock</c> failed on 0.2.0-preview.58. This side lists the defaults by reflection into the
/// fixture; <c>interface-defaults.spec.ts</c> holds the runtime to a function for each line.
/// </para>
/// Regenerate with <c>EQ_UPDATE_INTERFACE_DEFAULTS=1</c>.
/// </summary>
public class VocabularyInterfaceDefaultsTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string FixturePath => Path.Combine(RepoRoot(),
        "src", "eQuantic.UI.Runtime", "src", "shared", "__fixtures__", "interface-defaults.txt");

    /// <summary>The assemblies the runtime provides the types of: the vocabulary, the code engine,
    /// the component library and the charts.</summary>
    private static readonly Assembly[] Vocabulary =
    [
        typeof(eQuantic.UI.Primitives.VisualNode).Assembly,
        typeof(eQuantic.UI.Code.ICodeLanguage).Assembly,
        typeof(eQuantic.UI.Components.CodeEditor).Assembly,
        typeof(eQuantic.UI.Charts.BarChart).Assembly,
    ];

    /// <summary>`Interface.member`, the member as the twin names it, for every default: an instance
    /// member of a public interface with a body, which is what a class may leave undeclared.</summary>
    private static IEnumerable<string> Defaults() => Vocabulary
        .SelectMany(assembly => assembly.GetExportedTypes())
        .Where(type => type.IsInterface && RuntimeProvidedTypeScanner.IsRuntimeProvidedNamespace(type.Namespace ?? ""))
        .SelectMany(type => type
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsAbstract)
            .Select(method => $"{type.Name}.{JsName(method)}"))
        .Distinct()
        .Order(StringComparer.Ordinal);

    private static string JsName(MethodInfo method)
    {
        var name = method.IsSpecialName && (method.Name.StartsWith("get_") || method.Name.StartsWith("set_"))
            ? method.Name[4..]
            : method.Name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    [Fact]
    public void EveryVocabularyDefault_IsInTheRuntimesList()
    {
        var expected = string.Join('\n', Defaults()) + '\n';
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_INTERFACE_DEFAULTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FixturePath)!);
            File.WriteAllText(FixturePath, expected);
            return;
        }

        File.Exists(FixturePath).Should().BeTrue("the runtime's list is generated once with EQ_UPDATE_INTERFACE_DEFAULTS=1");
        File.ReadAllText(FixturePath).ReplaceLineEndings("\n").Should().Be(expected,
            "a vocabulary interface gained or lost a default: regenerate the list with "
            + "EQ_UPDATE_INTERFACE_DEFAULTS=1 and give interface-defaults.ts its copy, or an app's twin "
            + "that relies on it answers undefined in the browser");
    }

    /// <summary>The list is not empty by accident: the defaults #414 is about are in it.</summary>
    [Fact]
    public void TheListHoldsTheDefaultsAppsRelyOn() =>
        Defaults().Should().Contain(["IAppTheme.data", "ICodeLanguage.rules", "ICodeCompletionProvider.triggerCharacters"]);
}
