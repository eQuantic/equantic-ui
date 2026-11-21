using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The one dispatch over the DEFAULT INTERFACE MEMBERS a type takes (#414, #1511): JavaScript has no
/// interface to hold them, so each is written into the twin of every type that relies on it, as that
/// type's own member. This class owns the loop over <see cref="DefaultInterfaceMembers.Of"/>, the
/// refusals (EQ1008: a default that reaches an interface's static, one nothing can supply), the file each
/// body converts under, and the choice of the member's kind; each emitter supplies the writers it has
/// for its own members. The class's and the record's emitters each switched over the defaults, and the
/// two switches had drifted (different property guards, the delegated default written two ways), so a
/// kind of member could be written by one and missed by the other: a kind added here is a compile error
/// in every emitter until it writes it.
/// </summary>
internal abstract class DefaultMemberWriter(CSharpToJsConverter converter, Func<SyntaxNode, SemanticModel?> modelFor)
{
    /// <summary>Writes every default <paramref name="declaration"/>'s type takes, or refuses it.</summary>
    public void WriteAll(TypeDeclarationSyntax declaration)
    {
        if (modelFor(declaration) is not { } model || model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol self)
            return;
        foreach (var (implementation, member, _) in DefaultInterfaceMembers.Of(self, model.Compilation))
        {
            if (member is not null && modelFor(member) is { } memberModel
                && DefaultInterfaceMembers.InterfaceStaticIn(member, memberModel) is { } reached)
            {
                converter.Report(declaration, ConversionSeverity.Error, "EQ1008",
                    DefaultInterfaceMembers.Homeless(self, implementation, reached));
                continue;
            }
            switch (member)
            {
                case MethodDeclarationSyntax method when method.Body != null || method.ExpressionBody != null:
                    converter.InFileOf(method, () => Method(method));
                    break;
                // Of never hands an abstract implementation over, so a default property always has a body.
                case PropertyDeclarationSyntax property:
                    converter.InFileOf(property, () => Property(property));
                    break;
                case IndexerDeclarationSyntax indexer:
                    converter.InFileOf(indexer, () => Indexer(indexer));
                    break;
                case EventDeclarationSyntax e:
                    converter.InFileOf(e, () => Event(e));
                    break;
                // A vocabulary default from the interface's ASSEMBLY, with no body to convert: the twin
                // delegates to the runtime's copy, which the runtime import brings in.
                case null when implementation is IMethodSymbol or IPropertySymbol
                               && DefaultInterfaceMembers.RuntimeCarries(implementation.ContainingType):
                    var (name, parameters, call) = DefaultInterfaceMembers.Delegation(implementation);
                    converter.UsedRuntimeTypes.Add(implementation.ContainingType.Name);
                    Delegated(implementation, name, parameters, call);
                    break;
                // The runtime's copy is reached by a delegation, and an event's accessors have none.
                case null when implementation is IEventSymbol && DefaultInterfaceMembers.RuntimeCarries(implementation.ContainingType):
                    converter.Report(declaration, ConversionSeverity.Error, "EQ1008",
                        DefaultInterfaceMembers.Undelegated(self, implementation));
                    break;
                default:
                    converter.Report(declaration, ConversionSeverity.Error, "EQ1008",
                        DefaultInterfaceMembers.Unreadable(self, implementation));
                    break;
            }
        }
    }

    /// <summary>A default method, under its interface's file.</summary>
    protected abstract void Method(MethodDeclarationSyntax method);

    /// <summary>A default property, its getter and its setter.</summary>
    protected abstract void Property(PropertyDeclarationSyntax property);

    /// <summary>A default indexer, as the type's own is written (#427).</summary>
    protected abstract void Indexer(IndexerDeclarationSyntax indexer);

    /// <summary>A default event, its <c>add</c> and <c>remove</c> as the type's own event's accessors (#1511).</summary>
    protected abstract void Event(EventDeclarationSyntax e);

    /// <summary>A vocabulary default the runtime carries: a member named <paramref name="name"/> whose body
    /// is <paramref name="call"/>, a method taking <paramref name="parameters"/> or a getter.</summary>
    protected abstract void Delegated(ISymbol implementation, string name, IReadOnlyList<string> parameters, string call);
}
