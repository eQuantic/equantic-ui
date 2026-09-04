using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A field declared without an initializer takes its type's default, and C#'s default is decided by
/// the TYPE. The emitter answered from the SPELLED NAME, which knows the common primitives and
/// nothing else: an enum field, a char, and any type reached through an alias came out with no
/// initializer at all — <c>undefined</c>, where .NET has the zero member, '\0', or zero. An enum
/// field then rendered as nothing, silently, because a name lookup on undefined is undefined.
/// The symbol answers where the name cannot, through the same table the OrDefault family reads.
/// </summary>
public class FieldDefaultTests
{
    private const string Page = """
        using eQuantic.UI.Primitives;
        using Amount = System.Decimal;

        public enum Kind { Low, High }
        public enum Offset { A = 3, B = 4 }

        [Page("/d")]
        public sealed class Defaults : StatefulComponent
        {
            private Kind _kind;
            private Offset _offset;
            private char _c;
            private Amount _aliased;
            private long _n;
            private decimal _d;
            private bool _flag;
            private int _i;
            private string? _maybe;
            private Kind? _maybeKind;

            public override VisualNode Build(ComponentContext context)
                => new Text(_kind.ToString(), TypeRole.BodyM, context.Theme.TextPrimary);
        }
        """;

    [Fact]
    public void AFieldWithNoInitializerTakesItsTypesDefault()
    {
        var ts = new ComponentCompiler().CompileSource(Page, "Defaults.cs")
            .Single(r => r.ComponentName == "Defaults").TypeScript;

        // An enum is its member NAME at runtime, so the default is the member whose value is zero.
        ts.Should().Contain("_kind: string = 'low'");
        // No member is zero here: .NET still yields the numeric 0, and so does the twin.
        ts.Should().Contain("_offset: string = 0");
        ts.Should().Contain("_c: string = '\\0'");
        // Reached through an alias — invisible to a spelled-name table, plain to the symbol. The
        // VALUE is right and the annotation says `any`, which is an absence rather than a claim:
        // the default path reads the symbol, the annotation path still reads the spelling.
        ts.Should().Contain("_aliased: any = $eq.num.dec(0)");
        // And the annotations that DO carry a claim carry the true one: the literal beside each of
        // these is a bigint and a Decimal, so `number` was the file disagreeing with itself.
        ts.Should().Contain("_n: bigint = $eq.num.long(0)");
        ts.Should().Contain("_d: Decimal = $eq.num.dec(0)");
        ts.Should().Contain("_flag: boolean = false");
        ts.Should().Contain("_i: number = 0");
    }

    [Fact]
    public void ANullableFieldStaysUnset()
    {
        var ts = new ComponentCompiler().CompileSource(Page, "Defaults.cs")
            .Single(r => r.ComponentName == "Defaults").TypeScript;
        // `Kind?` and `string?` default to null in C#; neither takes the value type's zero.
        ts.Should().NotContain("_maybeKind: string = 'low'");
        ts.Should().NotContain("_maybe: string = '\\0'");
    }
}
