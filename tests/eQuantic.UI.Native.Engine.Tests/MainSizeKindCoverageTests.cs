using System.Reflection;
using System.Runtime.CompilerServices;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Every node type of the vocabulary that declares a size of its own is read as SIZED by the flex
/// pass's main-axis classifier, <c>MeasureVisitor.MainSizeKind</c>. That answer decides whether a
/// Flexible's child keeps its size inside the slot, as the web keeps it, or is pinned to the slot.
/// <para>
/// The classifier is a list of arms, and a list goes incomplete without a word: a camera preview, a
/// stack, a scroller, a canvas and a web frame all declared a width it never read, so a 320-wide
/// preview in a share of 300 was drawn at 300 where the web keeps 320. So the node types are taken
/// from the ASSEMBLY, never listed here: a node type that declares a width or a height, as a
/// <see cref="SizeValue"/> of its own, through a style it carries, or as a number its constructor
/// demands, is asked about, and the next one cannot be missed silently.
/// </para>
/// <para>
/// The classifier is looked up by name rather than called, so this same test also runs against the
/// classifier as it stood before, which is how it was shown to fail there.
/// </para>
/// </summary>
public class MainSizeKindCoverageTests
{
    /// <summary>Each node type that declares a main size, with the axis it declares it on.</summary>
    public static IEnumerable<object[]> SizedNodes() =>
        typeof(VisualNode).Assembly.GetExportedTypes()
            .Where(t => t is { IsAbstract: false, IsPublic: true } && typeof(VisualNode).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .SelectMany(t => new[] { true, false }
                .Where(horizontal => Declaration(t, horizontal) is not null)
                .Select(horizontal => new object[] { t.Name, horizontal }));

    /// <summary>
    /// The enumeration itself is the instrument, so it is checked first: an empty or shrunken set
    /// would make every case below pass by asking about nothing.
    /// </summary>
    [Fact]
    public void TheEnumeration_FindsEveryNodeTypeKnownToDeclareASize()
    {
        var found = SizedNodes().Select(row => (string)row[0]).ToHashSet();

        found.Should().Contain(
            ["Box", "Row", "Column", "Grid", "Stack", "ScrollView", "Canvas", "WebFrame",
             "Image", "Icon", "Vector", "Drawing", "Spinner", "CameraPreview"]);
    }

    /// <summary>
    /// A declared size is read as declared. A <see cref="SizeValue"/> is read for its KIND, so a
    /// fixed one is Fixed and a Fill one is Fill (an arm that answered Fixed for the type would lie
    /// about the second); a size the constructor demands is always Fixed.
    /// </summary>
    [Theory]
    [MemberData(nameof(SizedNodes))]
    public void ADeclaredMainSize_IsReadAsSized(string typeName, bool horizontal)
    {
        var type = typeof(VisualNode).Assembly.GetExportedTypes().Single(t => t.Name == typeName);
        var axis = horizontal ? "width" : "height";

        if (Declaration(type, horizontal)!.Value.BySizeValue)
        {
            MainSizeKind(Declared(type, horizontal, 320), horizontal).Should()
                .Be(SizeKind.Fixed, $"a {typeName} with a fixed {axis} declares that {axis}");
            MainSizeKind(Declared(type, horizontal, SizeValue.Fill), horizontal).Should()
                .Be(SizeKind.Fill, $"a {typeName} that fills declares no size of its own");
        }
        else
        {
            MainSizeKind(Declared(type, horizontal, 320), horizontal).Should()
                .Be(SizeKind.Fixed, $"a {typeName}'s constructor demands its {axis}");
        }
    }

    /// <summary>Where a node type declares its main size: a member of its own named for the axis (or
    /// <c>Size</c>, for a square one), or a style it carries that declares it.</summary>
    private static (PropertyInfo Member, PropertyInfo? Style, bool BySizeValue)? Declaration(Type type, bool horizontal)
    {
        var axis = horizontal ? "Width" : "Height";
        var own = type.GetProperty(axis, BindingFlags.Public | BindingFlags.Instance)
                  ?? type.GetProperty("Size", BindingFlags.Public | BindingFlags.Instance);
        if (own is not null && (own.PropertyType == typeof(SizeValue) || IsNumber(own.PropertyType)))
            return (own, null, own.PropertyType == typeof(SizeValue));

        foreach (var style in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (typeof(VisualNode).IsAssignableFrom(style.PropertyType)) continue;
            if (style.PropertyType.GetProperty(axis, BindingFlags.Public | BindingFlags.Instance) is { } inner
                && inner.PropertyType == typeof(SizeValue))
                return (inner, style, true);
        }

        return null;
    }

    private static bool IsNumber(Type type) => type == typeof(float) || type == typeof(double) || type == typeof(int);

    /// <summary>
    /// An instance whose only state is the size it declares. No constructor runs, which is the
    /// honest sample: the classifier reads a node's TYPE and its declared size and nothing else, so
    /// building a valid one would be ceremony that could only make the test pass for the wrong
    /// reason.
    /// </summary>
    private static VisualNode Declared(Type type, bool horizontal, SizeValue size)
    {
        var node = (VisualNode)RuntimeHelpers.GetUninitializedObject(type);
        var (member, style, bySizeValue) = Declaration(type, horizontal)!.Value;
        if (!bySizeValue) return node;

        if (style is null)
        {
            Set(node, member, size);
        }
        else
        {
            var carrier = Activator.CreateInstance(style.PropertyType)!;
            Set(carrier, member, size);
            Set(node, style, carrier);
        }

        return node;
    }

    private static void Set(object target, PropertyInfo property, object value)
    {
        if (property.SetMethod is { } setter)
        {
            setter.Invoke(target, [value]);
            return;
        }

        var field = target.GetType().GetField($"<{property.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new InvalidOperationException(
                        $"{target.GetType().Name}.{property.Name} has neither a setter nor an auto-property field to declare it through.");
        field.SetValue(target, value);
    }

    private static SizeKind MainSizeKind(VisualNode node, bool horizontal)
    {
        var visitor = typeof(LayoutEngine).Assembly.GetType("eQuantic.UI.Native.Framework.MeasureVisitor", throwOnError: true)!;
        var method = visitor.GetMethod("MainSizeKind",
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                     ?? throw new InvalidOperationException("MeasureVisitor.MainSizeKind is gone; point this test at what replaced it.");
        var target = method.IsStatic ? null : RuntimeHelpers.GetUninitializedObject(visitor);
        return (SizeKind)method.Invoke(target, [node, horizontal])!;
    }
}
