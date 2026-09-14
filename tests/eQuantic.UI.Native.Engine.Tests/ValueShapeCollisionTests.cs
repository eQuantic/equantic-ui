using System.Reflection;
using System.Runtime.CompilerServices;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Two value types in the vocabulary that carry the SAME components are a question, and this is
/// where it gets asked.
///
/// <para>
/// It is a question rather than a defect: four floats are four floats, and `CornerRadii`, `Rect`,
/// `EdgeInsets` and `Curve` are all of them and all different. What makes it worth asking is the
/// record of the two that were not. `VectorPoint` was `(float, float)` beside `Point`, and
/// `VectorTransform` was six floats beside `Matrix2D` — the same numbers in the same order, proven
/// bit-identical over 200,000 random compositions before they were collapsed. BOTH would have
/// appeared here on the day they were written: one joining an existing group, one forming a new
/// one. Neither was noticed for months, because nothing asked.
/// </para>
///
/// <para>
/// So this measures SHAPE, not meaning — it cannot tell `Size` from `Point` and does not try. It
/// holds a baseline that may only shrink: a new group, or a new member of one, fails and asks for a
/// sentence saying which thing this is that the others are not. Most answers will be "a different
/// thing", and writing that down costs a line. The two answers that were not cost a year of two
/// implementations of the same arithmetic.
/// </para>
/// </summary>
public class ValueShapeCollisionTests
{
    /// <summary>
    /// The assemblies a duplicate can hide ACROSS, which is where the two real ones did: the
    /// vocabulary, the library written against it, and the native track under it. `VectorTransform`
    /// sat in the first and `Matrix2D` in the engine, so a scan of ONE assembly would have reported
    /// nothing — which is the mistake this list exists to not make. The web realizer is not here
    /// because it declares no value type of its own; `AssemblyLayeringTests` is what would notice
    /// if that changed.
    /// </summary>
    private static readonly Assembly[] Scanned =
    [
        typeof(VisualNode).Assembly,                    // Primitives — the vocabulary
        typeof(eQuantic.UI.Components.Button).Assembly, // the component library
        typeof(DisplayList).Assembly,                   // Native.Engine — the display list
        typeof(LayoutNode).Assembly,                    // Native.Framework — layout
        typeof(PhotonHost).Assembly,                    // Native.Components — the native realizer
    ];

    /// <summary>
    /// Each shape that more than one public value type carries, and the types that carry it —
    /// with the sentence that says they are different things.
    /// </summary>
    private static readonly Dictionary<string, (string[] Types, string Reason)> Known = new()
    {
        ["Single,Single,Single,Single"] = (
            ["CornerRadii", "Curve", "EdgeInsets", "LinearColor", "Rect"],
            "four floats, five unrelated meanings: four corners, a cubic-bezier's two control "
            + "points, four insets, premultiplied linear RGBA, and a box's origin and extent. "
            + "Nothing here composes with anything else here."),
        ["Single,Single"] = (
            ["FrameTick", "Point", "Size"],
            "a position, an extent, and a clock reading (seconds and delta). Point and Size are "
            + "deliberately NOT one type: adding two sizes is meaningless and adding two points is "
            + "not, which is the distinction dart:ui draws with Offset and Size and we draw here."),
        ["Int32,Int32"] = (
            ["CellRef", "CodePosition"],
            "a spreadsheet cell is (row, column) and a caret is (line, character). They look alike "
            + "and are never interchanged: one indexes a grid, the other a text buffer, and one of "
            + "them is 1-based to its user."),
        ["Single,Single,Single"] = (
            ["MotionVector", "SpringSpec"],
            "a device's three axes of acceleration, against a spring's stiffness, damping and mass."),
        ["SizeKind,Single"] = (
            ["GridTrack", "SizeValue"],
            "how wide a thing wants to be, and how wide a grid COLUMN wants to be. The second is "
            + "the first plus what a track may do that a box may not (fractional units), and it "
            + "stays its own type because a track is authored in a place a SizeValue is not."),
    };

    /// <summary>
    /// The components a record struct carries: its compiler-generated positional properties, in
    /// declaration order. Hand-written properties are excluded deliberately — a derived edge
    /// (<c>Rect.Right</c>) is not a component, and counting it would make two identical records
    /// look different because one of them is more helpful.
    /// </summary>
    private static string Shape(Type type) => string.Join(",", type
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.GetIndexParameters().Length == 0 && p.CanRead)
        .Where(p => p.GetMethod!.GetCustomAttribute<CompilerGeneratedAttribute>() is not null)
        .Select(p => p.PropertyType.Name));

    [Fact]
    public void NoTwoValueTypesShareAShapeWithoutSayingWhy()
    {
        var groups = Scanned.SelectMany(a => a.GetExportedTypes())
            .Where(t => t is { IsValueType: true, IsEnum: false })
            .Select(t => (t.Name, Shape: Shape(t)))
            .Where(x => x.Shape.Length > 0)
            .GroupBy(x => x.Shape)
            .Where(g => g.Count() > 1)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());

        // A shape nobody has ruled on. The new type is named, because that is the one to look at.
        var unexplained = groups.Keys.Except(Known.Keys).ToList();
        unexplained.Should().BeEmpty(
            "these shapes are carried by more than one value type and nothing says they are "
            + "different things: "
            + string.Join("; ", unexplained.Select(s => $"({s}) → {string.Join(", ", groups[s])}"))
            + ". If they are, add the shape to Known with a sentence. If they are not, that is the "
            + "duplicate this test exists to find — VectorPoint and VectorTransform were both here.");

        // …and a type that JOINED a known group, which is the half a key-only check misses: the
        // shape was already excused, so the new arrival would ride in on somebody else's reason.
        foreach (var (shape, (types, _)) in Known)
        {
            groups.Should().ContainKey(shape,
                $"the baseline says {string.Join(", ", types)} share ({shape}) and no shape is "
                + "shared by two types any more — a group that emptied is a reason to delete, not "
                + "to keep");
            groups[shape].Should().BeEquivalentTo(types,
                $"({shape}) is excused for {types.Length} named types. A type that JOINS the group "
                + "rides in on their reason without ever being asked its own, which is exactly how "
                + "VectorPoint arrived beside Point and Size.");
        }
    }
}
