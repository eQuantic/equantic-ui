using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A type the app names like the union the runtime declares for a vocabulary enum (<c>DensityValue</c>
/// beside <c>Density</c>) is the app's own type, and a parameter of the enum is the enum, on both sides,
/// in TypeScript and in plain JavaScript. The union was named <c>&lt;Enum&gt;Value</c>, a name C# can
/// spell, so the app's record and the union met under one name in the module (#1427).
/// </summary>
public class EnumUnionNameConformanceTests
{
    private const string Source = """
        using eQuantic.UI.Primitives;
        public record DensityValue(int Rows);
        public sealed class Grid : StatelessComponent
        {
            private readonly Density _density;
            private readonly DensityValue _value;
            public Grid(Density density, DensityValue value) { _density = density; _value = value; }
            public string Says() => _density + "|" + _value.Rows + "|" + _value;
            public override VisualNode Build(ComponentContext context) => new Text(Says(), TypeRole.BodyM);
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("a parameter of the enum beside the app's record named like its union",
            "return new Grid(Density.Compact, new DensityValue(3)).Says();"),
        ("the app's record on its own", "return new DensityValue(4).ToString();"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnAppTypeNamedLikeAnEnumsUnion_IsTheAppsOwn(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);
}
