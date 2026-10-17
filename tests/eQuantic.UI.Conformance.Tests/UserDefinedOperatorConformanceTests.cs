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
    [InlineData("var xs = new[] { 1, 2 }; int s = 0; foreach (Money m in xs) s += m.Amount; return s;")] // 3 — the element converts
    [InlineData("int? n = null; Money? m = n; return m is null;")]               // True — the lifted implicit lets null through
    [InlineData("int? n = 5; Money? m = n; return m?.Amount;")]                   // 5
    [InlineData("Money? m = null; return ((int?)m) is null;")]                    // True — the lifted explicit
    [InlineData("Money? m = new Money(4); return (int?)m;")]                      // 4
    public void UserDefinedOperators_MatchDotNet(string statements) =>
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Money);

    private const string Factors = """
        public record struct Factor(double V) { public static implicit operator double(Factor f) => f.V; }
        public record struct Weight(double V) { public static implicit operator double(Weight? w) => w?.V ?? -1; }
        public record Ratio(double V) { public static implicit operator double(Ratio r) => r.V; }
        """;

    /// <summary>
    /// A conversion from one struct to another is LIFTED over their nullables: a null stays null and only a
    /// present value is converted, at every site the bound tree shows it. An operator that takes the nullable
    /// ITSELF is not lifted and receives the null, and a class's operator has no lifted form (Copilot's second
    /// review of #964).
    /// </summary>
    [Theory]
    [InlineData("Factor? f = null; double? d = f; return d is null;")]            // True
    [InlineData("Factor? f = new Factor(2); double? d = f; return d;")]           // 2
    [InlineData("Factor? f = null; var d = (double?)f; return d is null;")]       // True — spelled as a cast
    [InlineData("Factor? f = new Factor(2); var d = (double?)f; return d;")]      // 2
    [InlineData("var fs = new Factor?[] { new Factor(2), null }; var s = \"\"; foreach (double? d in fs) s += (d is null ? \"null\" : d.ToString()) + \",\"; return s;")] // 2,null, — the element converts
    [InlineData("Weight? w = null; double d = w; return d;")]                     // -1 — the operator receives the null
    [InlineData("Weight? w = new Weight(4); double d = w; return d;")]            // 4
    [InlineData("Ratio r = new Ratio(2); double d = r; return d;")]               // 2 — a class's operator, unchanged
    public void ALiftedConversion_LetsNullThrough(string statements) =>
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Factors);
}
