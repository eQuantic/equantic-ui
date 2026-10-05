using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// <c>x.Invoke()</c> is a CALL of <c>x</c> only when <c>x</c> is a delegate. A method of that name on
/// a class is a method like any other, and went out as a call of the object it lives on: the code
/// engine's <c>Completion.Invoke()</c>, ⌃Space, became <c>completion()</c>, which TypeScript refused
/// as "this expression is not callable" (#296). Decided by the bound symbol.
/// </summary>
public class InvokeMethodTests
{
    private static string Emit(string members) => TestHelper.ConvertClass(members);

    [Fact]
    public void AMethodNamedInvoke_IsCalledAsAMethod()
    {
        var js = Emit("""
                public int Invoke() => 7;
                public int Call(Sample other) => other.Invoke() + this.Invoke();
            """);

        js.Should().Contain("other.invoke()");
        js.Should().Contain("this.invoke()");
        js.Should().NotContain("other()");
    }

    [Fact]
    public void ADelegatesInvoke_IsACallOfTheDelegate()
    {
        var js = Emit("""
                public int Call(Func<int> make, Action<int> tell) { tell.Invoke(1); return make.Invoke(); }
            """);

        js.Should().Contain("tell(1)");
        js.Should().Contain("make()");
    }

    [Fact]
    public void Through_AConditionalAccess_TheSameRuleHolds()
    {
        var js = Emit("""
                public int Invoke() => 7;
                public int? Call(Sample? other, Func<int>? make) => other?.Invoke() ?? make?.Invoke();
            """);

        js.Should().Contain(".invoke()");
        js.Should().NotContain(".invoke().invoke");
        js.Should().MatchRegex(@"make\?\.\(\)|make\)\(\)|\$r\(\)",
            "the delegate is still called, however the guard is written");
    }
}
