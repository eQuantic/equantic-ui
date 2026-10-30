using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A primitive's own <c>Equals(object)</c> is true only for a value of its own type that is equal, and
/// builds for every primitive (#956, #957). The browser holds an int and a double as one kind of number
/// and a char and a text of one as one kind of string, so the type is read where the bound tree still shows
/// it, before the boxing. A value already held as <c>object</c> has lost it (#552): such a pair of an int
/// and a double of one value, or a char and a text, cannot be told apart.
/// </summary>
public class PrimitiveEqualsConformanceTests
{
    [SkippableTheory]
    // The rows of #956 and #957.
    [InlineData("return 1.Equals((object)1.0);")]
    [InlineData("return 'a'.Equals((object)\"a\");")]
    [InlineData("return ((byte)1).Equals((object)(byte)1);")]
    [InlineData("return 1m.Equals((object)1m);")]
    // Of its own type, equal or not, and of another, for each primitive.
    [InlineData("return 1.Equals((object)1) + \"|\" + 1.Equals((object)2) + \"|\" + 1.Equals((object)1L) + \"|\" + 1.Equals((object)(byte)1);")]
    [InlineData("return 1L.Equals((object)1L) + \"|\" + 1L.Equals((object)1);")]
    [InlineData("return ((byte)1).Equals((object)1) + \"|\" + ((byte)1).Equals((object)(byte)2);")]
    [InlineData("return ((sbyte)1).Equals((object)(sbyte)1) + \"|\" + ((short)1).Equals((object)(short)1) + \"|\" + ((short)1).Equals((object)1);")]
    [InlineData("return ((ushort)1).Equals((object)(ushort)1) + \"|\" + 1u.Equals((object)1u) + \"|\" + 1u.Equals((object)1) + \"|\" + 1ul.Equals((object)1ul) + \"|\" + 1ul.Equals((object)1L);")]
    [InlineData("return 1.5.Equals((object)1.5) + \"|\" + 1.5.Equals((object)1.5f) + \"|\" + 1.5f.Equals((object)1.5f) + \"|\" + 1.5f.Equals((object)1.5);")]
    [InlineData("return double.NaN.Equals((object)double.NaN) + \"|\" + float.NaN.Equals((object)float.NaN);")]
    [InlineData("return 1m.Equals((object)1) + \"|\" + 1m.Equals((object)2m) + \"|\" + 1.0m.Equals((object)1.00m);")]
    [InlineData("return 'a'.Equals((object)'a') + \"|\" + 'a'.Equals((object)'b') + \"|\" + 'a'.Equals((object)97);")]
    [InlineData("return true.Equals((object)true) + \"|\" + true.Equals((object)1) + \"|\" + true.Equals((object)\"True\");")]
    // A null, and a value held as object whose type is the receiver's.
    [InlineData("return 1.Equals((object)null) + \"|\" + 'a'.Equals((object)null) + \"|\" + 1m.Equals((object)null);")]
    [InlineData("object o = 1; object t = \"x\"; object m = 1m; object b = true; return 1.Equals(o) + \"|\" + 1.Equals(t) + \"|\" + 1.Equals(m) + \"|\" + 1.Equals(b) + \"|\" + 1m.Equals(m) + \"|\" + 1m.Equals(o) + \"|\" + true.Equals(b);")]
    // The typed overload C# binds where it can, which widens.
    [InlineData("return 1.Equals((byte)1) + \"|\" + 1.0.Equals(1) + \"|\" + 1L.Equals(1) + \"|\" + 1m.Equals(1);")]
    // Both are evaluated, in their order, whatever the answer.
    [InlineData("var log = \"\"; int A() { log += \"a\"; return 1; } object B() { log += \"b\"; return 1.0; } var r = A().Equals((object)(double)B()); return log + \"|\" + r;")]
    [InlineData("var log = \"\"; int A() { log += \"a\"; return 1; } double B() { log += \"b\"; return 1.0; } var r = A().Equals((object)B()); return log + \"|\" + r;")]
    public void APrimitivesEqualsOfObject_AnswersByItsOwnType(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
