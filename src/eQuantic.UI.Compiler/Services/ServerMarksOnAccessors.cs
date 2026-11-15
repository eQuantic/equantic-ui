using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// A server mark on an accessor, refused where it is declared (EQ2016, #1512). C# takes <c>[ServerAction]</c>
/// and <c>[ServerOnly]</c>, a method's attributes, on any accessor, and neither means there what it means on a
/// method. A <c>[ServerAction]</c> accessor is no action: the server registers none (an event's handler and a
/// getter's answer cannot cross the wire, and no stub calls one), and the twin wrote its body all the same, or
/// left a component's out in silence. A <c>[ServerOnly]</c> getter or setter of a property or an indexer
/// would have to refuse every client read and write of the member; it crossed with its server code instead.
/// <c>[ServerOnly]</c> on an event's <c>add</c> or <c>remove</c> is what keeps that accessor on the server
/// (<see cref="Parser.ComponentParser.StaysOnTheServer"/>), and is not refused. The mark is read from the
/// accessor's SYMBOL, so an alias marks it too.
/// </summary>
internal static class ServerMarksOnAccessors
{
    /// <summary>The refusals of <paramref name="type"/>'s own accessors, at each accessor, in source order;
    /// none where <paramref name="model"/> does not hold its tree.</summary>
    public static List<CompilationError> Check(TypeDeclarationSyntax type, string sourcePath, SemanticModel? model)
    {
        var errors = new List<CompilationError>();
        if (model is null || model.SyntaxTree != type.SyntaxTree) return errors;
        foreach (var member in type.Members.OfType<BasePropertyDeclarationSyntax>())
        foreach (var accessor in member.AccessorList?.Accessors ?? default)
        {
            if (accessor.AttributeLists.Count == 0 || model.GetDeclaredSymbol(accessor) is not { } symbol) continue;
            var mark = PlainClassModule.IsServerAction(symbol) ? "[ServerAction]"
                : PlainClassModule.IsServerOnly(symbol) && member is not EventDeclarationSyntax ? "[ServerOnly]"
                : null;
            if (mark is null) continue;
            var position = accessor.Keyword.GetLocation().GetLineSpan().StartLinePosition;
            errors.Add(new CompilationError
            {
                Code = "EQ2016",
                Message = Message(type.Identifier.ValueText, MemberName(member), accessor.Keyword.ValueText, mark),
                SourcePath = sourcePath,
                Line = position.Line + 1,
                Column = position.Character + 1,
            });
        }
        return errors;
    }

    private static string MemberName(BasePropertyDeclarationSyntax member) => member switch
    {
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        EventDeclarationSyntax e => e.Identifier.ValueText,
        _ => "this[]",
    };

    private static string Message(string type, string member, string accessor, string mark) => mark == "[ServerAction]"
        ? $"'{type}.{member}' marks its '{accessor}' accessor [ServerAction], and an accessor is no Server Action: the "
          + "server registers none, and no client stub can call one. Move the code into a [ServerAction] method and "
          + "call that, or take the mark off."
        : $"'{type}.{member}' marks its '{accessor}' accessor [ServerOnly], and only an event's accessor stays on the "
          + "server: a property's or an indexer's would cross with its server code. Move the code into a [ServerOnly] "
          + "method and call it from server code, or take the mark off.";
}
