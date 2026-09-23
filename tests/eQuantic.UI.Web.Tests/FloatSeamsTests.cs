using System.Reflection;
using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// WHERE C# SAYS FLOAT, THE RUNTIME HANDS C# A SINGLE (#146). eqc rounds a float where C# code
/// PRODUCES one, and a value the browser produces enters C# code through a seam the hand-written
/// runtime owns: a scroll offset handed to <c>ScrollView.OnScrolled</c>, a drag's travel, a
/// pointer's position in a <c>CanvasPointer</c>. A raw double there is a value .NET could not hold.
/// <para>
/// The seams are DERIVED, never listed: every vocabulary callback whose delegate takes a float, and
/// every vocabulary type with a float member that the runtime's lowering constructs itself. Each
/// must name a test in <c>float-seams.spec.ts</c>, which proves the rounding against a raw double,
/// so a callback added tomorrow fails here until the browser's side of it is proven.
/// </para>
/// </summary>
public class FloatSeamsTests
{
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string Runtime => Path.Combine(RepoRoot(), "src", "eQuantic.UI.Runtime", "src");

    private static readonly Assembly Vocabulary = typeof(VisualNode).Assembly;

    private static bool IsFloat(Type type) => (Nullable.GetUnderlyingType(type) ?? type) == typeof(float);

    /// <summary>A callback the runtime invokes: a delegate-typed property taking a float.</summary>
    private static IEnumerable<string> Callbacks() =>
        Vocabulary.GetExportedTypes().SelectMany(type => type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => typeof(Delegate).IsAssignableFrom(property.PropertyType)
                && property.PropertyType.GetMethod("Invoke")!.GetParameters().Any(p => IsFloat(p.ParameterType)))
            .Select(property => $"{type.Name}.{property.Name}"));

    private static bool HasFloatMember(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(p => IsFloat(p.PropertyType))
        || type.GetFields(BindingFlags.Public | BindingFlags.Instance).Any(f => IsFloat(f.FieldType))
        || type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(m => !m.IsSpecialName && IsFloat(m.ReturnType));

    /// <summary>A value the runtime BUILDS from what the browser measured: a vocabulary type it
    /// constructs in its lowering or its DOM controllers (<c>new X(</c>, or the <c>XCtor</c> it
    /// was handed), and that has a float member.</summary>
    private static IEnumerable<string> ConstructedTwins()
    {
        var sources = Directory.EnumerateFiles(Path.Combine(Runtime, "dom"), "*.ts")
            .Append(Path.Combine(Runtime, "shared", "lowering.ts"))
            .Where(file => !file.EndsWith(".spec.ts", StringComparison.Ordinal));
        var constructed = sources
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"\bnew\s+([A-Z]\w*?)(?:Ctor)?\s*\(")
                .Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        return Vocabulary.GetExportedTypes()
            .Where(type => constructed.Contains(type.Name) && HasFloatMember(type))
            .Select(type => type.Name);
    }

    [Fact]
    public void EveryFloatSeam_IsProvenToHandCSharpASingle()
    {
        var seams = Callbacks().Concat(ConstructedTwins()).Distinct().Order(StringComparer.Ordinal).ToList();
        seams.Should().Contain(["ScrollView.OnScrolled", "CanvasPointer"],
            "the scan must reach the seams it exists for, or the check below holds on nothing");

        var spec = File.ReadAllText(Path.Combine(Runtime, "shared", "float-seams.spec.ts"));
        var proven = Regex.Matches(spec, @"\bit\('([^']+)'").Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        seams.Where(seam => !proven.Contains(seam)).Should().BeEmpty(
            "each of these hands a value the browser measured to C# code that declares it a float — "
            + "round it where the runtime hands it over, and prove it with an `it('<seam>')` in "
            + "src/eQuantic.UI.Runtime/src/shared/float-seams.spec.ts");
        proven.Where(test => !seams.Contains(test)).Should().BeEmpty(
            "a test in float-seams.spec.ts names a seam the vocabulary no longer has");
    }
}
