using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The vocabulary's types the runtime transpiles from their C# (the spreadsheet's and the forms'
/// models, <c>[TwinIsTranspiled]</c>) are built by an app as its own types are: the constructor the
/// call binds, then the object initializer applied to what it built (#592). An app reaches them as
/// metadata in the vocabulary's namespace, and every one was taken for a hand-written twin, so the
/// initializer rode a trailing config object the transpiled constructor does not take. Run through the
/// module graph an app's build writes, against the runtime's own twins.
/// </summary>
public class TranspiledVocabularyConformanceTests
{
    private const string App = """
        using eQuantic.UI.Primitives;
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("a record struct's initializer", "var c = new CellRef(1, 2) { Col = 3 }; return c.Row + \"|\" + c.Col;"),
        ("a record's initializer", "var e = new FieldError(\"name\", \"required\") { Message = \"too short\" }; return e.Message;"),
        ("a class's init-only members", "var e = new SheetEdit { At = 3, Count = 2 }; return e.At + \"|\" + e.Count;"),
        ("a class's settable member", "var s = new SheetController(10, 4) { Changed = edit => { } }; return s.Changed is null ? \"none\" : \"set\";"),
        ("a record struct's with", "var c = new CellRef(1, 2) with { Col = 5 }; return c.Row + \"|\" + c.Col;"),
        ("a record struct's zero", "CellRef c = default; return c.Row + \"|\" + c.Col;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ATranspiledVocabularyType_IsBuiltAsAnAppsTypeIs(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(App, typeAnnotations, Cases);
}
