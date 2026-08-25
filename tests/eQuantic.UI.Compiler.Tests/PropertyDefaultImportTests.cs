using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A value-typed property with no initializer takes its type's default in the CONSTRUCTOR guard —
/// and that default can be an <c>$eq.*</c> expression. It rode in as parser-made text, past the
/// converter's helper tracking, so <c>$eq.num.long(0)</c> landed in a module that never imported
/// <c>$eq</c>: "ReferenceError: $eq is not defined" at <c>new</c>, containing the component on a
/// page the server had rendered perfectly — before hydration could even coerce the payload, which
/// made the typed frontier LOOK broken when it was never reached.
/// </summary>
public class PropertyDefaultImportTests
{
    private const string Page = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        public sealed class Hero : StatefulComponent
        {
            public long Downloads { get; init; }

            public override VisualNode Build(ComponentContext context)
                => new Text($"{Downloads}", TypeRole.BodyM);
        }

        [Page("/long-prop")]
        public sealed class LongPropPage : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
                => new Hero { Downloads = 5 };
        }
        """;

    [Fact]
    public void AValueTypedPropertyDefaultImportsTheHelperItUses()
    {
        var twin = new ComponentCompiler().CompileSource(Page, "Hero.cs")
            .Single(r => r.ComponentName == "Hero").TypeScript;

        twin.Should().Contain("$eq.num.long(0)", "the guard gives an unset long property C#'s zero");
        twin.Should().MatchRegex(@"import \{[^}]*\$eq[^}]*\} from .@equantic/runtime.",
            "an emitted $eq without its import is a ReferenceError at construction");
    }
}
