using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for the names the transpiler CHANGES on their way into a JavaScript scope: a local
/// function's, camel-cased like a method's (<c>LocalFunctionName</c>), and a reserved word's, renamed
/// (<c>ToJsIdentifier</c>). C# is case-sensitive and has no reserved words in common with JavaScript,
/// so each case is a program C# accepts where the changed name met one the scope already held.
/// </summary>
public class LocalNameConformanceTests
{
    [SkippableTheory]
    // Beside a local of the cased name: the module did not parse ("d" has already been declared).
    [InlineData("string log = \"\"; var d = new Dictionary<string, int> { [\"a\"] = 1 }; int D() { log += \"d\"; return 7; } d.GetValueOrDefault(\"a\", D()); return log;")] // "d"
    // Under a parameter of the cased name, the call reached the number (TypeError)...
    [InlineData("int D() => 7; int Use(int d) => d + D(); return Use(1);")] // 8
    // ...its own parameter included, when it recurses.
    [InlineData("int D(int d) => d <= 0 ? 0 : d + D(d - 1); return D(3);")] // 6
    // In an inner block the outer local read the function: a wrong answer, and no error.
    [InlineData("int d = 5; int r; { int D() => 1; r = d + D(); } return r;")] // 6
    // Two local functions that differ by case: the module did not parse.
    [InlineData("int Foo() => 1; int foo() => 2; return Foo() * 10 + foo();")] // 12
    // A cased name that is a reserved word: the direct call skipped the rename its declaration
    // took, and called `delete()`...
    [InlineData("int Delete() => 3; return Delete();")] // 3
    // ...where a method group already agreed with the declaration.
    [InlineData("int Delete() => 3; Func<int> f = Delete; return f();")] // 3
    // A cased name that is a global the translation itself reads: Convert.ToInt32 over text is
    // `parseInt`, so the function called itself (RangeError).
    [InlineData("int ParseInt(string s) => Convert.ToInt32(s) * 2; return ParseInt(\"21\");")] // 42
    // A generic one, called with its type argument...
    [InlineData("T Id<T>(T x) => x; return Id<int>(5);")] // 5
    // ...and as a method group with it, which the generic-name path wrote as its source text:
    // `Id` beside a declared `id`, and `Delete` beside `delete$` (ReferenceError).
    [InlineData("T Id<T>(T x) => x; Func<int, int> f = Id<int>; return f(5);")] // 5
    [InlineData("T Delete<T>(T x) => x; Func<int, int> f = Delete<int>; return f(6);")] // 6
    public void ALocalFunction_TakesANameItsScopeDoesNotHold(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // The rename landed on a name C# can write: both locals became `package_`, and the module did
    // not parse.
    [InlineData("int package = 1; int package_ = 2; return package * 10 + package_;")] // 12
    // A delegate local called by its source text: `package()` is a reserved word in a module.
    [InlineData("Func<int> package = () => 4; return package();")] // 4
    public void AReservedWordsRename_LandsOnNoOtherName(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // A C# keyword reaches JavaScript through the verbatim escape, and each of these is reserved
    // there too: the rename's list held only the words that are not C# keywords, so each one
    // arrived as written and the module did not parse.
    [InlineData("int @class = 3; return @class;")] // 3
    [InlineData("int @default = 2; return @default * 10;")] // 20
    [InlineData("int @this = 4; return @this + 1;")] // 5
    [InlineData("int F(int @new) => @new * 2; return F(5);")] // 10
    public void AVerbatimKeyword_IsRenamedLikeAnyReservedWord(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
