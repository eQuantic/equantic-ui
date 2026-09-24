using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// An assertion at the tail of a null-conditional asserts NOTHING when the value is null:
/// <c>host.TextTarget?.Placeholder.Should().Be("Search")</c> skips the whole chain, <c>Should</c>
/// included, and the test passes on exactly the null it was written to catch. Six of them stood in
/// the Photon keyboard tests (Tab order, autofocus), each reading as a check that the field had the
/// keyboard, and none of them could have failed for want of one. Found while writing the find bar's
/// tests, which made the same mistake three times.
/// <para>
/// The cure is one pair of parentheses, <c>(host.TextTarget?.Placeholder).Should()</c>: the null
/// reaches the assertion and fails it. So the rule has no exceptions and no baseline. Roslyn reads
/// the tests, for the reason <see cref="OneTypePerFileTests"/> gives: which call sits inside a
/// conditional access is a question only a parser answers honestly.
/// </para>
/// </summary>
public class NoAssertionBehindANullCheckTests
{
    [Fact]
    public void NoAssertionIsTheTailOfANullConditional()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "tests"), "*.cs",
                     SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file),
                new CSharpParseOptions(LanguageVersion.Preview));
            foreach (var call in tree.GetCompilationUnitRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!IsShould(call) || !IsSkippedOnNull(call)) continue;
                var line = tree.GetLineSpan(call.Span).StartLinePosition.Line + 1;
                offenders.Add($"{Path.GetRelativePath(RepoRoot(), file).Replace(Path.DirectorySeparatorChar, '/')}:{line}");
            }
        }

        Assert.True(offenders.Count == 0,
            "An assertion sits at the tail of a null-conditional, so a null skips it and the test "
            + "passes. Parenthesize the value, `(x?.Y).Should()`, so the null reaches the assertion:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>A call to FluentAssertions' entry point, by its name: <c>x.Should()</c> or the
    /// <c>?.Should()</c> binding.</summary>
    private static bool IsShould(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.Text == "Should",
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.Text == "Should",
        _ => false,
    };

    /// <summary>
    /// Whether the call is inside the part of a conditional access that runs only when the value is
    /// not null. The walk stops at the first expression that fences it: a parenthesized expression,
    /// an argument, a lambda, a statement. <c>(a?.B).Should()</c> is outside every such part.
    /// </summary>
    private static bool IsSkippedOnNull(InvocationExpressionSyntax call)
    {
        for (Microsoft.CodeAnalysis.SyntaxNode node = call; node.Parent is { } parent; node = parent)
        {
            switch (parent)
            {
                case ConditionalAccessExpressionSyntax conditional when conditional.WhenNotNull == node:
                    return true;
                case ParenthesizedExpressionSyntax or ArgumentSyntax or LambdaExpressionSyntax
                    or StatementSyntax:
                    return false;
            }
        }

        return false;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eQuantic.UI.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
