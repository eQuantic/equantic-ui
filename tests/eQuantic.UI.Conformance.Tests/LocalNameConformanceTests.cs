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
    // A function called `Component` is that function: the name was read as the component's
    // inherited `_component` before anything asked what it bound (TypeError).
    [InlineData("int Component() => 4; Func<int> f = Component; return f();")] // 4
    // Nested: a local function is a statement, not a member, so the inner function's member is the
    // method's, the captured `d` is seen, and `D` keeps off it. Copilot's review of #399 asked.
    [InlineData("int d = 5; int Outer() { int D() => 1; return d + D(); } return Outer();")] // 6
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

    [SkippableTheory]
    // A pattern, a deconstruction or a loop declares its names in paths of their own, and each one
    // wrote the source text where every reference reads the renamed name: `is int @class` declared
    // `class`, `is int package` a reserved word, and a switch arm `@default` kept its `@`.
    [InlineData("object o = 5; if (o is int @class) return @class; return 0;")] // 5
    [InlineData("object o = 7; if (o is int package) return package; return 0;")] // 7
    [InlineData("object o = 3; return o switch { int @default => @default * 2, _ => 0 };")] // 6
    [InlineData("var (@class, b) = (1, 2); return @class + b;")] // 3
    [InlineData("var xs = new[] { (1, 2) }; int s = 0; foreach (var (@class, b) in xs) s += @class + b; return s;")] // 3
    public void APatternOrDeconstructionName_IsRenamedAsItsReferencesRead(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // Every other kind of declaration that writes its own name, each measured with `@class` after the
    // review of #399 named a few: each wrote the source text, `@` included, and the module did not
    // parse. A for loop, a catch, a using, an anonymous method, a record's deconstruction, and the
    // selector parameter Sum and Average write into their own arrows.
    [InlineData("", "int s = 0; for (int @class = 0; @class < 3; @class++) s += @class; return s;")] // 3
    [InlineData("", "try { throw new Exception(\"x\"); } catch (Exception @class) { return @class.Message; }")] // "x"
    [InlineData("", "using (System.IDisposable @class = null) { return 1; }")] // 1
    [InlineData("", "Func<int, int> f = delegate (int @class) { return @class * 2; }; return f(4);")] // 8
    [InlineData("public record Point(int X, int Y);", "var (@class, y) = new Point(1, 2); return @class + y;")] // 3
    [InlineData("", "var xs = new[] { 1, 2 }; return xs.Sum(@class => @class * 2);")] // 6
    [InlineData("", "var xs = new[] { 1, 3 }; return xs.Average(@class => @class * 1.0);")] // 2
    // A query's range variable already took the rename, and stays as a guard.
    [InlineData("", "var xs = new[] { 1, 2, 3 }; return (from @class in xs where @class > 1 select @class).Count();")] // 2
    public void ADeclarationOfAnyKind_IsRenamedAsItsReferencesRead(string prelude, string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, prelude);
    }
}
