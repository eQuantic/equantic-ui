using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A generic record's twin compares the closed type a value was built as, and the build marks each
/// value where C# names its type arguments (#651). A record that declares no type parameter keeps the
/// equality it had: the mark is the generic one's alone.
/// </summary>
public class GenericRecordClosureTests
{
    private const string Source = """
        using eQuantic.UI.Primitives;

        public sealed record Box<T>(T Value);
        public sealed record Plain(int Value);

        [Page("/boxes")]
        public sealed class BoxesPage : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
                => new Text(new Box<int>(1).Equals(new Box<int>(2)) ? "same" : "other", TypeRole.BodyM);
        }
        """;

    private static string Twin(string name) => new ComponentCompiler().CompileSource(Source, "Boxes.cs")
        .Single(result => result.ComponentName == name).TypeScript;

    [Fact]
    public void AGenericRecordsEquals_ComparesItsClosedType_AndAPlainRecordsDoesNot()
    {
        Twin("Box").Should().Contain("$eq.sameClosure(this, o)");
        Twin("Plain").Should().NotContain("sameClosure");
    }

    [Fact]
    public void AClosedGenericRecord_IsMarkedWhereItIsBuilt()
    {
        Twin("BoxesPage").Should().Contain("$eq.closing(new Box(1), 'int')").And.Contain("$eq.closing(new Box(2), 'int')");
    }
}
