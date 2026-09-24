using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for the names the transpiler CHANGES on their way into a JavaScript scope: a reserved
/// word's, renamed (<c>ToJsIdentifier</c>). C# has no reserved words in common with JavaScript, so
/// each case is a program C# accepts where the changed name met one the scope already held.
/// </summary>
public class LocalNameConformanceTests
{
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
}
