using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A type's OWN operators — arithmetic, unary, implicit and explicit conversions — written as
/// C# lets you write them and read back through the bound tree: JavaScript cannot overload an
/// operator, so the twin carries each one as a static method and every site the bound tree
/// shows an operator or a user-defined conversion at calls it. Executed on both sides.
/// </summary>
public class UserDefinedOperatorConformanceTests
{
    private const string Money = """
        public record struct Money(int Amount)
        {
            public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
            public static Money operator -(Money a, Money b) => new(a.Amount - b.Amount);
            public static Money operator -(Money m) => new(-m.Amount);
            public static implicit operator Money(int v) => new(v);
            public static explicit operator int(Money m) => m.Amount;
        }
        """;

    [Theory]
    [InlineData("Money a = new(1), b = new(2); return (a + b).Amount;")]          // 3 — binary
    [InlineData("Money a = new(5), b = new(2); return (a - b).Amount;")]          // 3 — same token, binary arity
    [InlineData("Money m = new(3); return (-m).Amount;")]                         // -3 — same token, unary arity
    [InlineData("Money m = 5; return m.Amount;")]                                 // 5 — implicit at a declaration
    [InlineData("int Total(Money m) => m.Amount; return Total(7);")]              // 7 — implicit at an argument
    [InlineData("Money m = new(4); return (int)m;")]                              // 4 — explicit
    [InlineData("Money m = new(1); m += new Money(2); return m.Amount;")]         // 3 — compound
    [InlineData("Money m = new(1); m += 2; return m.Amount;")]                    // 3 — compound with an implicit operand
    [InlineData("Money m = new(2); m -= 5; return m.Amount;")]                    // -3
    [InlineData("var xs = new[] { new Money(1), new Money(2) }; int s = 0; foreach (var x in xs) s += (int)x; return s;")] // 3
    public void UserDefinedOperators_MatchDotNet(string statements) =>
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Money);
}
