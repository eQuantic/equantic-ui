using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A twin says the type C# declared where TypeScript would otherwise guess, so the module the runtime's
/// <c>tsc</c> reads holds what the C# held. A local function kept its parameters' types and lost its return,
/// so TypeScript inferred the DERIVED type its body returned, and a variable it initialized refused the
/// sibling C# lets it take (#793). The temporary an object initializer lowers through was <c>any</c>, so a
/// local built by one was <c>any</c> too, and a lambda subscribed to its event had nothing to infer from
/// (TS7006, #1106).
/// </summary>
public sealed class ATwinSaysItsDeclaredTypeTests
{
    private static string Compile(string body) =>
        new ComponentCompiler().CompileSource($$"""
            #nullable enable
            using System;
            public class Shape { }
            public class Circle : Shape { }
            public class Square : Shape { }
            public class Pad { public int Size { get; set; } public event Action<int>? Pressed; public void Press() => Pressed?.Invoke(Size); }
            public class Host
            {
                public int Seen;
                public void Run()
                {
                    {{body}}
                }
            }
            """).Single(result => result.ComponentName == "Host").TypeScript;

    [Fact]
    public void ALocalFunction_SaysTheTypeItDeclares_NotTheOneItsBodyReturns()
    {
        var host = Compile("""
            Shape Make() => new Circle();
            Shape shape = Make();
            shape = new Square();
            """);

        host.Should().Contain("const make = (): Shape => {");
    }

    [Theory]
    [InlineData("int Twice(int n) => n * 2; Seen = Twice(1);", "(n: number) =>")]
    [InlineData("System.Collections.Generic.List<int> Items() => new(); Seen = Items().Count;", "() =>")]
    [InlineData("async System.Threading.Tasks.Task Wait() { await System.Threading.Tasks.Task.Delay(1); } _ = Wait();", "async () =>")]
    [InlineData("void Touch() { Seen = 1; } Touch();", "() =>")]
    // A .NET type is named as the mapper spells it where that names something, and left alone where not:
    // a Guid is a string, and a Task held as a value a promise (found by the review of #793).
    [InlineData("Guid Next() => Guid.NewGuid(); _ = Next();", "const next = (): string =>")]
    [InlineData("System.Threading.Tasks.Task Wait() { return System.Threading.Tasks.Task.CompletedTask; } _ = Wait();", "const wait = (): Promise<void> =>")]
    public void ATypeWithNoNameToSay_LeavesItToTypeScript(string body, string head) =>
        Compile(body).Should().Contain(head);

    /// <summary>Where C# lets the return be null, the annotation does not refuse it: written with
    /// <c>?</c>, or with nullable annotations off, where <c>Shape Find() { return null; }</c> was written
    /// <c>(): Shape</c> (found by the review of #793).</summary>
    [Theory]
    [InlineData("Shape? Find() { return null; } _ = Find();")]
    [InlineData("""
        #nullable disable
        Shape Find() { return null; } _ = Find();
        #nullable restore
        """)]
    // A null the body forgives with `!` is one C# compiles under a non-nullable return, and the twin
    // writes it as null, which `(): Shape` refused (found by the review of the pull request).
    [InlineData("Shape Find() => null!; _ = Find();")]
    [InlineData("Shape Find(bool b) { if (b) return new Circle(); return default!; } _ = Find(true);")]
    public void AReturnCSharpLetsBeNull_IsNotRefusedIt(string body) =>
        Compile(body).Should().Contain("const find = (").And.NotContain("): Shape =>");

    /// <summary>An async local function returns a promise of what it declared, where TypeScript inferred
    /// a promise of the derived type its body returned (found by the review of the pull request).</summary>
    [Fact]
    public void AnAsyncLocalFunction_SaysThePromiseOfTheTypeItDeclares()
    {
        var host = Compile("""
            async System.Threading.Tasks.Task<Shape> Load() { await System.Threading.Tasks.Task.Delay(1); return new Circle(); }
            _ = Load();
            """);

        host.Should().Contain("const load = async (): Promise<Shape> =>");
    }

    [Fact]
    public void AnObjectInitializersTemporary_IsTheTypeItHolds()
    {
        var host = Compile("""
            var pad = new Pad { Size = 3 };
            pad.Pressed += size => Seen = size;
            pad.Press();
            """);

        host.Should().Contain("let $n0: Pad;").And.NotContain("$n0: any");
    }
}
