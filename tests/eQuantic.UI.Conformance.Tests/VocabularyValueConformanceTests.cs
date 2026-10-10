using System.Reflection;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A member of a vocabulary value type the browser holds as plain data (<c>[TwinIsData]</c>,
/// <c>Color</c> and <c>Curve</c>) answers in the browser what it answers in .NET (#494, #518). eqc emitted an instance
/// method as a method of the value, which a plain <c>{ r, g, b, a }</c> does not have, so
/// <c>Color.FromRgb(…).WithOpacity(0.8f)</c> rendered on the server and threw in the browser, and
/// the value's text was <c>[object Object]</c>.
/// <para>
/// The members are enumerated by reflection, so a method added to such a type without its
/// companion static fails here, by name, before a page meets it.
/// </para>
/// </summary>
public class VocabularyValueConformanceTests
{
    [SkippableTheory]
    // ---- an instance method goes to the companion, value first ----
    [InlineData("return Color.FromRgb(0xF8, 0x71, 0x71).WithOpacity(0.8f);")]
    [InlineData("return Color.FromRgb(0, 0, 0).MidpointWith(Color.White);")]
    [InlineData("var brand = Color.FromRgb(0xF8, 0x71, 0x71); return brand.WithOpacity(0.5f).MidpointWith(brand);")]
    [InlineData("Color? maybe = Color.White; return maybe?.WithOpacity(0.25f);")]
    [InlineData("Func<float, Color> fade = Color.White.WithOpacity; return fade(0.5f);")]
    [InlineData("var brand = Color.FromRgb(1, 2, 3); Func<Color, Color> mix = brand.MidpointWith; return mix(Color.White);")]
    // ---- its text is the record text .NET writes ----
    [InlineData("return Color.FromRgba(1, 2, 3, 4).ToString();")]
    [InlineData("return $\"{Color.FromRgba(1, 2, 3, 4)}\";")]
    [InlineData("return \"c=\" + Color.FromRgba(1, 2, 3, 4);")]
    [InlineData("Color? none = null; return $\"[{none}]\";")]
    // ---- a construction and a default build the data ----
    [InlineData("return new Color(1, 2, 3, 4);")]
    [InlineData("return new Color(1, 2, 3, 4) { A = 9 };")]
    [InlineData("Color c = new(5, 6, 7, 8); return c;")]
    [InlineData("return new Color();")]
    [InlineData("return default(Color);")]
    [InlineData("return new Color(B: 3, A: 4, R: 1, G: 2);")]
    // ---- the arguments run in the order they are written, every one of them ----
    [InlineData("int n = 0; byte Next() => (byte)++n; var c = new Color(B: Next(), R: Next(), G: 0, A: 0); return c.R * 10 + c.B;")]
    [InlineData("int n = 0; byte Next() => (byte)++n; var c = new Color(1, 2, 3, Next()) { A = 9 }; return n * 100 + c.A;")]
    // ---- what already answered keeps answering ----
    [InlineData("return new Color(1, 2, 3, 4) == Color.FromRgba(1, 2, 3, 4);")]
    [InlineData("return new Color(1, 2, 3, 4) with { A = 9 };")]
    [InlineData("var (r, g, b, a) = Color.FromRgba(1, 2, 3, 4); return r + g + b + a;")]
    public void AColorAnswersAsInDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// A <c>Curve</c> is the record C# reads in browser code too (#518). The generated design system
    /// exported a preset as an ARRAY and the runtime's <c>MotionSpec</c> declared its curve a preset
    /// NAME, so <c>Curve.Standard.X1</c> and <c>Motion.Press.Curve.X1</c> read undefined, and
    /// <c>new Curve(…)</c>, a transition's easing among them, threw "is not a constructor".
    /// </summary>
    [SkippableTheory]
    // ---- a preset is the record, wherever the browser reads it ----
    [InlineData("return Curve.Standard.X1;")]
    [InlineData("return Curve.Accelerate;")]
    [InlineData("return Motion.Press.Curve.X1;")]
    [InlineData("return Motion.Exit;")]
    [InlineData("object role = Motion.Exit; return role is MotionSpec spec ? spec.Curve.X1 : -1f;")]
    // ---- a construction builds the record's data ----
    [InlineData("return new Curve(0.2f, 0.9f, 0.3f, 1.25f).Y2;")]
    [InlineData("return new Curve(0.2f, 0.9f, 0.3f, 1.25f);")]
    [InlineData("Curve c = new(0.4f, 0f, 0.2f, 1f); return c.X1 + c.X2;")]
    [InlineData("return default(Curve);")]
    // ---- a curve held in a transition, and read back ----
    [InlineData("var spec = new TransitionSpec(StyleChannels.Colors, 150) { Easing = new Curve(0.2f, 0.9f, 0.3f, 1.25f) }; return spec.Easing.X2;")]
    [InlineData("return TransitionSpec.Of(StyleChannels.Opacity, Motion.Enter).Easing;")]
    [InlineData("return new TransitionSpec(StyleChannels.All).Easing == Curve.Standard;")]
    // ---- equality, text, a copy and a deconstruction ----
    [InlineData("return new Curve(0.2f, 0f, 0f, 1f) == Curve.Standard;")]
    [InlineData("return Curve.Standard != Curve.Decelerate;")]
    [InlineData("return Motion.State.Curve.Equals(Curve.Standard);")]
    [InlineData("return Curve.Standard.ToString();")]
    [InlineData("return $\"{Curve.Accelerate}\";")]
    [InlineData("return Curve.Standard with { Y2 = 1.5f };")]
    [InlineData("var (x1, y1, x2, y2) = Curve.Accelerate; return x1 + y1 + x2 + y2;")]
    public void ACurveAnswersAsInDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// A METHOD GROUP of a value the browser holds as data is the delegate its call is: <c>Equals</c>,
    /// <c>ToString</c> and <c>GetHashCode</c> answer through the helpers a direct call uses, and the
    /// receiver is read once, when the delegate is made, as C# copies it into the delegate. The
    /// companion carries the type's own methods and its presets, never these three, so
    /// <c>Curve.Standard.Equals</c> bound <c>Curve.equals</c>, which is missing, and threw making the
    /// delegate, and <c>ToString</c> bound the object's own <c>toString</c>, which answered
    /// <c>[object Object]</c>, on a <c>Color</c> as on a <c>Curve</c> (#518's review).
    /// </summary>
    [SkippableTheory]
    // ---- Equals, ToString and GetHashCode as delegates ----
    [InlineData("Func<Curve, bool> same = Curve.Standard.Equals; return same(new Curve(0.2f, 0f, 0f, 1f));")]
    [InlineData("Func<object, bool> same = Curve.Standard.Equals; return same(Curve.Standard) && !same(\"standard\");")]
    [InlineData("Func<string> text = Curve.Standard.ToString; return text();")]
    [InlineData("Func<int> hash = Curve.Standard.GetHashCode; return hash() == Curve.Standard.GetHashCode();")]
    [InlineData("Func<Color, bool> same = Color.White.Equals; return same(Color.FromRgb(255, 255, 255));")]
    [InlineData("Func<object, bool> same = Color.White.Equals; return same(Color.White) && !same(\"white\");")]
    [InlineData("Func<string> text = Color.FromRgba(1, 2, 3, 4).ToString; return text();")]
    [InlineData("Func<int> hash = Color.White.GetHashCode; return hash() == Color.White.GetHashCode();")]
    // ---- the receiver is read once, when the delegate is made ----
    [InlineData("int n = 0; Curve Make() { n++; return Curve.Accelerate; } Func<string> text = Make().ToString; var twice = text() + text(); return n + \" \" + twice;")]
    [InlineData("int n = 0; Color Make() { n++; return Color.White; } Func<Color, bool> same = Make().Equals; var both = same(Color.White) && same(Color.White); return n + \" \" + both;")]
    [InlineData("var c = Curve.Standard; Func<string> text = c.ToString; c = Curve.Decelerate; return text();")]
    [InlineData("var c = Color.White; Func<Color, bool> same = c.Equals; c = Color.FromRgb(0, 0, 0); return same(Color.White);")]
    public void AMethodGroupOfAValueHeldAsData_IsTheDelegateItsCallIs(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNet(statements);
    }

    [SkippableFact]
    public void AnAppsOwnColor_IsTheAppsOwn()
    {
        // It was recognized by NAME, so a three-argument construction became the vocabulary's.
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            "return new Color(\"brand\", 10, 50).Name;",
            "public sealed record Color(string Name, int Hue, int Light);");
    }

    [SkippableTheory]
    [MemberData(nameof(EveryMemberOfEveryDataTwin))]
    public void EveryMemberOfEveryValueTheBrowserHoldsAsData_AnswersAsInDotNet(string member, string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        member.Should().NotBeNullOrEmpty();
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNet(statements);
    }

    [SkippableFact]
    public void EveryValueTypeOfTheVocabulary_IsDataInTheBrowser_ExactlyWhenItSaysSo()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        var exports = ConformanceRunner.RuntimeExports.Value.ToHashSet(StringComparer.Ordinal);
        var values = typeof(VisualNode).Assembly.GetTypes()
            .Where(type => type is { IsPublic: true, IsValueType: true, IsEnum: false, Namespace: { } space }
                && (space == "eQuantic.UI.Primitives" || space.StartsWith("eQuantic.UI.Primitives.", StringComparison.Ordinal)))
            .ToList();
        var marked = values.Where(IsData).ToList();
        marked.Should().NotBeEmpty("Color is plain data in the browser");
        marked.Select(type => type.Name).Should().BeSubsetOf(exports, "a value the browser holds as data has a companion to call");

        // Every one of them, with no exception left to list: `Curve` was the last value type whose
        // twin was neither a class nor the record's data (#518), and the list that excused it could
        // only shrink.
        var shipped = values.Where(type => exports.Contains(type.Name)).ToList();
        var kinds = ConformanceRunner.RuntimeExportKinds(shipped.Select(type => type.Name));
        foreach (var type in shipped)
            (kinds[type.Name] == "object").Should().Be(IsData(type),
                $"{type.Name}'s twin is a runtime {(kinds[type.Name] == "object" ? "object" : "class")}, "
                + "and [TwinIsData] has to say exactly that, or eqc lowers its members to the wrong shape");
    }

    public static TheoryData<string, string> EveryMemberOfEveryDataTwin()
    {
        var cases = new TheoryData<string, string>();
        foreach (var type in typeof(VisualNode).Assembly.GetTypes().Where(IsData))
        {
            var sample = Sample(type, 0);
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                         .Where(method => !method.IsSpecialName && !RecordMembers.Contains(method.Name)))
            {
                var arguments = method.GetParameters().Select((parameter, i) => SampleOf(parameter.ParameterType, type, i + 1));
                cases.Add($"{type.Name}.{method.Name}", $"return {sample}.{method.Name}({string.Join(", ", arguments)});");
            }
            cases.Add($"{type.Name}.ToString", $"return {sample}.ToString();");
            // A property is data the value stores or a value it computes, and both are read here: a
            // computed one is never written into the data, so it has to answer through the companion.
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                         .Where(property => property.GetIndexParameters().Length == 0 && property.Name != "EqualityContract"))
                cases.Add($"{type.Name}.{property.Name}", $"return {sample}.{property.Name};");
        }
        return cases;
    }

    /// <summary>What the record writes for itself: equality and deconstruction answer through the
    /// runtime's own helpers, its text is a case of its own above, and GetHashCode has no lowering
    /// for any type yet.</summary>
    private static readonly HashSet<string> RecordMembers = ["ToString", "GetHashCode", "Equals", "Deconstruct", "PrintMembers"];

    private static bool IsData(Type type) => type.GetCustomAttribute<TwinIsDataAttribute>() is not null;

    /// <summary>A value of a data twin, from its widest public constructor.</summary>
    private static string Sample(Type type, int seed)
    {
        var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var arguments = constructor.GetParameters().Select((parameter, i) => SampleOf(parameter.ParameterType, type, seed * 7 + i + 1));
        return $"new {type.Name}({string.Join(", ", arguments)})";
    }

    private static string Fraction(int seed) =>
        (seed % 9 / 10.0 + 0.05).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A sample argument of a parameter type, or a failure naming it, so a member nobody
    /// can call here is never skipped in silence.</summary>
    private static string SampleOf(Type parameter, Type owner, int seed) => parameter switch
    {
        _ when parameter == typeof(float) => Fraction(seed) + "f",
        _ when parameter == typeof(double) => Fraction(seed),
        _ when parameter == typeof(byte) => $"(byte){(seed * 53) % 256}",
        _ when parameter == typeof(int) => $"{seed}",
        _ when parameter == typeof(bool) => seed % 2 == 0 ? "true" : "false",
        _ when parameter == typeof(string) => $"\"s{seed}\"",
        _ when parameter == owner => Sample(owner, seed),
        _ => throw new InvalidOperationException(
            $"{owner.Name} has a member taking a {parameter.Name}, which this suite has no sample of. "
            + "Add one, so the member is executed on both sides."),
    };
}
