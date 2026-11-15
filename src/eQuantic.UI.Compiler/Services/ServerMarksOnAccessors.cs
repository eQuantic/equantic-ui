using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;

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
    /// <summary>
    /// The refusals of the accessors <paramref name="type"/>'s twin would hold, at each accessor, in source
    /// order: its own, a field-like event's that <c>[method: ServerAction]</c> marks, and those of the default
    /// interface members it takes (<see cref="CodeGen.DefaultInterfaceMembers.Of"/>), which no module of the
    /// interface's own checks, since an interface is written into no module but its implementers'. A partial
    /// type's other declaration, and a nested type, are modules of their own, each checked as one. None where
    /// <paramref name="model"/> does not hold its tree, which the compiler never hands: the parser gives every
    /// definition its tree, and the provider a model of any tree.
    /// </summary>
    public static List<CompilationError> Check(TypeDeclarationSyntax type, string sourcePath, SemanticModel? model)
    {
        var errors = new List<CompilationError>();
        if (model is null || model.SyntaxTree != type.SyntaxTree) return errors;
        IEnumerable<MemberDeclarationSyntax> members = type.Members;
        if (model.GetDeclaredSymbol(type) is INamedTypeSymbol self)
            members = members.Concat(CodeGen.DefaultInterfaceMembers.Of(self, model.Compilation)
                .Select(inherited => inherited.Declaration).OfType<MemberDeclarationSyntax>());
        foreach (var member in members)
        {
            var memberModel = member.SyntaxTree == model.SyntaxTree ? model : model.Compilation.ModelOf(member.SyntaxTree);
            if (memberModel is null) continue;
            var owner = (member.Parent as BaseTypeDeclarationSyntax)?.Identifier.ValueText ?? type.Identifier.ValueText;
            var path = member.SyntaxTree == type.SyntaxTree || member.SyntaxTree.FilePath.Length == 0 ? sourcePath : member.SyntaxTree.FilePath;
            void Refuse(SyntaxToken at, string name, string accessor, bool action)
            {
                var position = at.GetLocation().GetLineSpan().StartLinePosition;
                errors.Add(new CompilationError
                {
                    Code = "EQ2016",
                    Message = Message(owner, name, accessor, action),
                    SourcePath = path,
                    Line = position.Line + 1,
                    Column = position.Character + 1,
                });
            }

            switch (member)
            {
                case BasePropertyDeclarationSyntax property:
                    foreach (var accessor in property.AccessorList?.Accessors ?? default)
                    {
                        if (accessor.AttributeLists.Count == 0 || memberModel.GetDeclaredSymbol(accessor) is not { } symbol) continue;
                        if (PlainClassModule.IsServerAction(symbol))
                            Refuse(accessor.Keyword, MemberName(property), accessor.Keyword.ValueText, action: true);
                        else if (PlainClassModule.IsServerOnly(symbol) && property is not EventDeclarationSyntax)
                            Refuse(accessor.Keyword, MemberName(property), accessor.Keyword.ValueText, action: false);
                    }
                    break;
                // `[method: ServerAction]` marks both accessors the compiler writes for a field-like event.
                case EventFieldDeclarationSyntax { AttributeLists.Count: > 0 } field:
                    foreach (var variable in field.Declaration.Variables)
                        if (memberModel.GetDeclaredSymbol(variable) is IEventSymbol { AddMethod: { } add }
                            && PlainClassModule.IsServerAction(add))
                            Refuse(variable.Identifier, variable.Identifier.ValueText, "add", action: true);
                    break;
            }
        }
        return errors;
    }

    private static string MemberName(BasePropertyDeclarationSyntax member) => member switch
    {
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        EventDeclarationSyntax e => e.Identifier.ValueText,
        _ => "this[]",
    };

    private static string Message(string type, string member, string accessor, bool action) => action
        ? $"'{type}.{member}' marks its '{accessor}' accessor [ServerAction], and an accessor is no Server Action: the "
          + "server registers none, and no client stub can call one. Move the code into a [ServerAction] method and "
          + "call that, or take the mark off."
        : $"'{type}.{member}' marks its '{accessor}' accessor [ServerOnly], and only an event's accessor stays on the "
          + "server: a property's or an indexer's would cross with its server code. Move the code into a [ServerOnly] "
          + "method and call it from server code, or take the mark off.";
}
