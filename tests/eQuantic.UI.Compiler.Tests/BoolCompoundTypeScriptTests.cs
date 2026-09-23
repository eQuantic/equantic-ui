using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A bool compound on a target with a receiver (<c>Get().Flag |= next</c>) binds the receiver once,
/// as a parameter of the writer's arrow, and a parameter must be TYPED where the output is
/// TypeScript: a strict tsc refuses an implicit any (TS7006), which is what the runtime's own
/// modules are checked with. The lowering left it untyped; the conformance suite could not see it,
/// because it runs the module rather than type-checking it (found beside the review of #359).
/// </summary>
public class BoolCompoundTypeScriptTests
{
    private const string Source = """
        public sealed class Holder { public bool Flag { get; set; } }

        public sealed class Probe
        {
            private readonly Holder _holder = new();

            private Holder Get() => _holder;

            public void Run(bool next) { Get().Flag |= next; }
        }
        """;

    [Fact]
    public void ABoundReceiverIsTypedWhereTheOutputIsTypeScript()
    {
        var compiler = new ComponentCompiler();
        var ts = compiler.CompileSource(Source, "Probe.cs").Single(r => r.ComponentName == "Probe").TypeScript;

        ts.Should().Contain("($0: any) =>");
    }

    [Fact]
    public void AndUntypedWhereItIsJavaScript()
    {
        var compiler = new ComponentCompiler { TypeAnnotations = false };
        var js = compiler.CompileSource(Source, "Probe.cs").Single(r => r.ComponentName == "Probe").TypeScript;

        js.Should().Contain("($0) =>");
        js.Should().NotContain(": any");
    }
}
