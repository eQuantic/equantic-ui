using eQuantic.UI.Compiler.CodeGen.Ir;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

/// <summary>
/// Single evaluation as the writer's decision. A template names what it computes; which parts
/// get bound, in what order, and which are simply inlined is decided here — the same way for
/// every table strategy, instead of by an arrow each one spelled out by hand.
/// </summary>
public class JsTemplateTests
{
    private static JsExpr Call(string text) => JsExpr.Callish(text);
    private static string Write(JsExpr e) => JsExprWriter.Write(e);

    [Fact]
    public void APartUsedOnce_IsSubstituted()
    {
        Write(JsExpr.Template("{0}.normalize()", Call("f()"))).Should().Be("f().normalize()");
        Write(JsExpr.Template("{0}.replace(/x/g, {1})", Call("f()"), Call("g()")))
            .Should().Be("f().replace(/x/g, g())");
    }

    /// <summary>A hole is its part's index in any number of digits: the eleventh part is <c>{10}</c>,
    /// which a hole of one digit left in the code as text.</summary>
    [Fact]
    public void AHoleOfTwoDigits_IsFilled()
    {
        var parts = Enumerable.Range(0, 11).Select(index => (JsExpr)JsExpr.Identifier("p" + index)).ToArray();
        Write(JsExpr.Template("f({0}, {9}, {10})", parts)).Should().Be("f(p0, p9, p10)");
    }

    [Fact]
    public void APartUsedTwice_IsBoundOnce()
    {
        Write(JsExpr.Template("({0} === {0}.normalize())", Call("f()")))
            .Should().Be("(($0) => ($0 === $0.normalize()))(f())");
    }

    [Fact]
    public void APlainNameOrLiteral_IsInlined_NobodyCanSeeItReadTwice()
    {
        Write(JsExpr.Template("({0} === {0}.normalize())", JsExpr.Identifier("s")))
            .Should().Be("(s === s.normalize())");
        Write(JsExpr.Template("({0} < 0n ? -{0} : {0})", JsExpr.Literal("5n")))
            .Should().Be("(5n < 0n ? -5n : 5n)");
        Write(JsExpr.Template("({0} === {0})", JsExpr.This)).Should().Be("(this === this)");
    }

    [Fact]
    public void AMemberRead_IsNotInlined_AGetterMayCount()
    {
        Write(JsExpr.Template("({0} === {0}.normalize())", JsExpr.ThisMember("text")))
            .Should().Be("(($0) => ($0 === $0.normalize()))(this.text)");
    }

    /// <summary>
    /// A part inside a function the template defines ran once per call of that function, where C#
    /// evaluated it once: <c>Intersect</c>'s second sequence ran once per element (#657). It is bound,
    /// and the earlier parts with it, so the order holds.
    /// </summary>
    [Fact]
    public void APartInsideTheTemplatesOwnFunction_IsBoundOnce()
    {
        Write(JsExpr.Template("[...new Set({0})].filter(($x) => {1}.includes($x))", Call("f()"), Call("g()")))
            .Should().Be("(($0, $1) => [...new Set($0)].filter(($x) => $1.includes($x)))(f(), g())");
        Write(JsExpr.Template("(function($arr) { return $arr.filter({1}); })({0})", Call("f()"), Call("g()")))
            .Should().StartWith("(($0, $1) => ");
    }

    /// <summary>
    /// Two parts stay inside the function: a lambda written in place, whose making again nobody can
    /// tell, and which keeps there the parameter types a call around it gives; and a part that reads a
    /// name the function declares, which exists nowhere else (<c>Convert.ToBoolean(v, provider)</c>
    /// converts the function's own <c>$v</c>).
    /// </summary>
    [Fact]
    public void ALambdaOrAPartThatReadsTheFunctionsOwnName_StaysInsideIt()
    {
        Write(JsExpr.Template("{0}.filter(($x) => ({1})($x))", Call("f()"), JsExpr.Arrow("v", JsExpr.Identifier("v"))))
            .Should().Be("f().filter(($x) => ((v) => v)($x))");
        Write(JsExpr.Template("(($v, _provider) => {0})({1}, {2})", Call("$eq.bool.convert($v)"), Call("f()"), Call("g()")))
            .Should().Be("(($v, _provider) => $eq.bool.convert($v))(f(), g())");
    }

    /// <summary>
    /// A plain name inside the template's function is read again on each call: a selector that
    /// reassigns its own variable made the later elements call the new delegate, where C# read the
    /// argument once (Copilot's review of #661).
    /// </summary>
    [Fact]
    public void APlainNameInsideTheTemplatesOwnFunction_IsReadOnce()
    {
        Write(JsExpr.Template("{0}.reduce(($sum, $x) => $sum + {1}($x), 0)", JsExpr.Identifier("xs"), JsExpr.Identifier("selector")))
            .Should().Be("(($0, $1) => $0.reduce(($sum, $x) => $sum + $1($x), 0))(xs, selector)");
    }

    /// <summary>
    /// A part holding a template of its own declares that template's names, and its <c>$x</c> is not
    /// the <c>$x</c> of the function around it: <c>Intersect(Other().DistinctBy(x => x))</c> stayed in
    /// the filter and called <c>Other()</c> once per element (Copilot's review of #661).
    /// </summary>
    [Fact]
    public void APartThatDeclaresTheFunctionsNameItself_IsBoundOnce()
    {
        Write(JsExpr.Template("[...new Set({0})].filter(($x) => {1}.includes($x))",
                Call("f()"), Call("(($arr) => $arr.filter(($x) => $x > 0))(g())")))
            .Should().Be("(($0, $1) => [...new Set($0)].filter(($x) => $1.includes($x)))(f(), (($arr) => $arr.filter(($x) => $x > 0))(g()))");
    }

    /// <summary>
    /// A plain name read twice is inlined only while nothing else in the template runs code between
    /// the reads. A lambda the template calls, or a part that is not a name or a literal, can reassign
    /// it: <c>xs.Average(x => { xs = new[] { 1 }; return x; })</c> divided the first array's total by
    /// the second array's length (Copilot's second review of #661).
    /// </summary>
    [Fact]
    public void APlainNameReadTwice_IsBound_WhenTheTemplateRunsCodeBetweenTheReads()
    {
        Write(JsExpr.Template("({0}.reduce(($sum, $x) => $sum + ({1})($x), 0) / {0}.length)",
                JsExpr.Identifier("xs"), JsExpr.Arrow("v", JsExpr.Identifier("v"))))
            .Should().Be("(($0) => ($0.reduce(($sum, $x) => $sum + ((v) => v)($x), 0) / $0.length))(xs)");
        Write(JsExpr.Template("({0}.indexOf({1}) + {0}.length)", JsExpr.Identifier("xs"), Call("f()")))
            .Should().Be("(($0) => ($0.indexOf(f()) + $0.length))(xs)");
        Write(JsExpr.Template("{0}[{0}.length - {1}]", JsExpr.Identifier("xs"), JsExpr.Literal("1")))
            .Should().Be("xs[xs.length - 1]");
        // A call that runs after the last read cannot change what the reads saw.
        Write(JsExpr.Template("({0}.length > 0 ? {0} : [{1}])", JsExpr.Identifier("xs"), Call("f()")))
            .Should().Be("(xs.length > 0 ? xs : [f()])");
    }

    /// <summary>
    /// A name a string quotes is not a read of it: <c>Intersect(Other("$x"))</c> kept <c>Other</c> inside
    /// the filter, which declares <c>$x</c>, and called it once per element (Copilot's second review of
    /// #661). An interpolation is code, and a string that does not close leaves the text as it is, so
    /// a real read is never missed.
    /// </summary>
    [Fact]
    public void ANameAStringQuotes_IsNotARead()
    {
        const string filter = "[...new Set({0})].filter(($x) => {1}.includes($x))";
        Write(JsExpr.Template(filter, Call("f()"), Call("other(\"$x\")")))
            .Should().Be("(($0, $1) => [...new Set($0)].filter(($x) => $1.includes($x)))(f(), other(\"$x\"))");
        Write(JsExpr.Template(filter, Call("f()"), Call("other('$x')")))
            .Should().Be("(($0, $1) => [...new Set($0)].filter(($x) => $1.includes($x)))(f(), other('$x'))");
        Write(JsExpr.Template(filter, Call("f()"), Call("other(`a${$x}`)")))
            .Should().Be("[...new Set(f())].filter(($x) => other(`a${$x}`).includes($x))");
        Write(JsExpr.Template(filter, Call("f()"), Call("split(/\"/).concat($x)")))
            .Should().Be("[...new Set(f())].filter(($x) => split(/\"/).concat($x).includes($x))");
    }

    /// <summary>A hole past nine: a comparator with eleven keys held `{10}` as text (Copilot's review of #661).</summary>
    [Fact]
    public void AHoleNumberedPastNine_IsFilled()
    {
        var parts = Enumerable.Range(0, 12).Select(i => (JsExpr)JsExpr.Literal(i.ToString())).ToArray();
        Write(JsExpr.Template(string.Join(", ", Enumerable.Range(0, 12).Select(i => $"{{{i}}}")), parts))
            .Should().Be(string.Join(", ", Enumerable.Range(0, 12)));
    }

    [Fact]
    public void BindingALaterPart_BindsTheEarlierOnes_ToKeepEvaluationOrder()
    {
        // {1} needs binding; {0} is a call that C# evaluates FIRST — passing it as the earlier
        // argument keeps that order.
        Write(JsExpr.Template("{0}.has({1}) ? {1} : null", Call("f()"), Call("g()")))
            .Should().Be("(($0, $1) => $0.has($1) ? $1 : null)(f(), g())");
        // A NAME is bound too. The bound call runs first, as the arrow's argument, and it can
        // reassign the name C# had already read: `d.TryGetValue(Swap(), out var v)`, where Swap()
        // sets d, looked the key up in the dictionary Swap() swapped in.
        Write(JsExpr.Template("{0}.has({1}) ? {1} : null", JsExpr.Identifier("m"), Call("g()")))
            .Should().Be("(($0, $1) => $0.has($1) ? $1 : null)(m, g())");
        // Only what nothing can reassign stays inline: a literal, `this`.
        Write(JsExpr.Template("{0}.has({1}) ? {1} : null", JsExpr.This, Call("g()")))
            .Should().Be("(($1) => this.has($1) ? $1 : null)(g())");
    }

    [Fact]
    public void InlineParts_SwapOnlyWhereNoEffectCrossesARead()
    {
        // Two names read in either order read the same values.
        Write(JsExpr.Template("f({1}, {0})", JsExpr.Identifier("a"), JsExpr.Identifier("b")))
            .Should().Be("f(b, a)");
        // A call C# evaluates AFTER the name must not run before the name is read.
        Write(JsExpr.Template("f({1}, {0})", JsExpr.Identifier("a"), Call("g()")))
            .Should().Be("(($0, $1) => f($1, $0))(a, g())");
        // Nor may a name be read a second time after a call that runs between its two reads: the name
        // is bound, read once before the call, which then stays where C# runs it.
        Write(JsExpr.Template("({0} === {1} ? {0} : 0)", JsExpr.Identifier("a"), Call("g()")))
            .Should().Be("(($0) => ($0 === g() ? $0 : 0))(a)");
    }

    [Fact]
    public void TheFillIsOnePass_APartIsNeverScannedForHoles()
    {
        Write(JsExpr.Template("{0}.pad({1})", Call("f()"), JsExpr.Literal("'{0}'")))
            .Should().Be("f().pad('{0}')");
    }

    [Fact]
    public void NestedTemplates_KeepTheirOwnBindings()
    {
        var inner = JsExpr.Template("({0} === {0}.trim())", Call("g()"));
        Write(JsExpr.Template("({0} && {0})", inner))
            .Should().Be("(($0) => ($0 && $0))((($0) => ($0 === $0.trim()))(g()))");
    }

    [Fact]
    public void BoundParameters_AreBare_TheArgumentsTypeThem()
    {
        // TypeScript types an immediately-invoked arrow's parameters from the arguments it is called
        // with, so a bare `$0` keeps the receiver's type, and a callback the template hands it is
        // typed from there. Annotated `any`, the receiver lost its type and the template's own
        // callbacks fell to TS7006 under a strict tsc (Copilot's third review of #661). That is
        // proved by type-checking, not by this text: the BoundReceivers fixture the runtime's tsc reads.
        Write(JsExpr.Template("({0}.reduce(($a, $b) => $a + $b, 0) / {0}.length)", Call("f()")))
            .Should().Be("(($0) => ($0.reduce(($a, $b) => $a + $b, 0) / $0.length))(f())");
    }

    [Fact]
    public void ATemplate_IsSafeAsAnOperand_ByConvention()
    {
        // Self-delimiting text is the template author's contract; the node declares call-level
        // binding and the writer never fences it.
        Write(JsExpr.Binary(JsExpr.Template("({0} === {1})", Call("f()"), Call("g()")), "&&", JsExpr.Identifier("x")))
            .Should().Be("(f() === g()) && x");
    }
}
