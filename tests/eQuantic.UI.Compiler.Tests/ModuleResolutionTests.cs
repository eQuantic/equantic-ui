using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A module must RESOLVE: every capitalised name its body mentions is one the module declares or
/// imports. Nothing else in the suite asserts this — the emission tests compare TEXT, so a name
/// emitted without its import reads perfectly and dies at load with "X is not defined", taking the
/// whole page with it. The typed boundary is exactly where that happened: a hydration spec names a
/// record (<c>{ _todos: [Todo] }</c>), and a record reaches a page only as a field's declared type
/// or an action's return type — neither of which used to produce a runtime reference, so the type
/// scan that feeds the import router had never needed to collect them.
/// </summary>
public class ModuleResolutionTests
{
    private const string Page = """
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using eQuantic.UI.Primitives;

        public sealed record Money(decimal Amount);
        public readonly record struct Point(long X, long Y);

        [Page("/wallet")]
        public sealed class Wallet : StatefulComponent
        {
            private List<Money> _monies = new();
            private Money? _maybe;
            private Point _point;
            private Dictionary<string, Money> _byName = new();
            private (decimal Amount, long Id) _tuple;

            [ServerAction]
            public async Task<List<Money>> Load() { await Task.Delay(1); return new(); }
            [ServerAction]
            public async Task<(decimal, long)> Pair() { await Task.Delay(1); return default; }

            public override VisualNode Build(ComponentContext context)
                => new Text("", TypeRole.BodyM, context.Theme.TextPrimary);
        }
        """;

    [Fact]
    public void EveryNameTheBodyMentionsIsImportedOrDeclared()
    {
        var module = new ComponentCompiler().CompileSource(Page, "Wallet.cs")
            .Single(r => r.ComponentName == "Wallet").TypeScript;

        var available = new HashSet<string>(JsGlobals.Concat(TypeLevelNames));
        foreach (Match import in Regex.Matches(module, @"import\s*\{([^}]*)\}"))
            foreach (var name in import.Groups[1].Value.Split(','))
                available.Add(name.Trim().Split(' ').Last());
        foreach (Match declared in Regex.Matches(module, @"\b(?:class|function|const|let|var)\s+([A-Z][A-Za-z0-9_]*)"))
            available.Add(declared.Groups[1].Value);

        var body = string.Join('\n', module.Split('\n').Where(line => !line.TrimStart().StartsWith("import")));
        // A capitalised name in a VALUE position: after `[`, `,`, `:`, `(` or `new`, never after a dot
        // (a member) and never inside a string.
        var mentioned = Regex.Matches(Regex.Replace(body, @"'[^']*'|""[^""]*""|`[^`]*`", "''"),
                @"(?<![.\w$])(?<!\bimport\s)([A-Z][A-Za-z0-9_]*)\b(?!\s*:)")
            .Select(m => m.Groups[1].Value)
            .ToHashSet();

        mentioned.Except(available).Should().BeEmpty(
            "every capitalised name the emitted module mentions must be declared or imported, or it "
            + "throws \"is not defined\" at load and the page never renders");
    }

    /// <summary>The hydration spec of a TUPLE is positional — it crosses as an array, and has no
    /// twin to name. Naming its symbol (<c>ValueTuple</c>) emitted a reference to a class that
    /// exists in no module at all.</summary>
    [Fact]
    public void ATupleHydratesPositionally()
    {
        var module = new ComponentCompiler().CompileSource(Page, "Wallet.cs")
            .Single(r => r.ComponentName == "Wallet").TypeScript;
        module.Should().Contain("_tuple: { tuple: ['decimal', 'long'] }");
        module.Should().Contain("'Wallet/Pair', []), { tuple: ['decimal', 'long'] })");
        module.Should().NotContain("ValueTuple");
    }

    /// <summary>TypeScript's own type-level names. They appear only in annotations
    /// (<c>_byName: Record&lt;string, any&gt;</c>) and are erased before anything runs, so they need
    /// no import and cannot be "not defined".</summary>
    private static readonly string[] TypeLevelNames =
    [
        "Record", "Partial", "Readonly", "Required", "Pick", "Omit", "ReturnType", "Awaited",
    ];

    /// <summary>Names JavaScript itself provides, which no module imports.</summary>
    private static readonly string[] JsGlobals =
    [
        "Array", "Object", "String", "Number", "Boolean", "Math", "JSON", "Map", "Set", "Date",
        "Promise", "Error", "RegExp", "Symbol", "BigInt", "Infinity", "NaN", "Intl",
    ];
}
