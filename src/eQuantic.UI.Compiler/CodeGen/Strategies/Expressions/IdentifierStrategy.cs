using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// A bare name. The symbol decides what it reaches: a local/parameter/range variable is the name
/// itself, an instance member is <c>this.member</c> (a method group additionally <c>.bind(this)</c>),
/// a static member goes through its class, a local function is the <c>const</c> beside the caller.
/// Without a symbol the shape decides — a leading underscore or a capital reads as a member — which
/// is legal exactly where guessing is (see <c>ConversionContext.CanGuess</c>).
/// </summary>
public class IdentifierStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is IdentifierNameSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var identifier = (IdentifierNameSyntax)node;
        // ValueText strips the verbatim-identifier @ (C# `@checked` → JS `checked` — not reserved there).
        var name = identifier.Identifier.ValueText;

        // Map 'Component' property (in State classes) to 'this._component'
        if (name == "Component") return JsExpr.ThisMember("_component");

        // Priority: Semantic Check > String Check (Fallback)
        var symbol = context.SemanticHelper.GetSymbol(identifier);

        // If it's a type symbol, return as is (to allow EnumStrategy to work)
        if (symbol is ITypeSymbol || symbol is INamedTypeSymbol) return JsExpr.Identifier(name);

        if (context.SemanticHelper.IsSystemConsole(symbol)) return JsExpr.Identifier("console");
        if (context.SemanticModel == null && name == "Console") return JsExpr.Identifier("console");

        // Is this identifier the `.Name` side of `other.Member`? Then the receiver already
        // qualifies it, and only the member name is emitted.
        var isMemberName = identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier;

        // Resolve member access prefix (this.) using semantic model
        if (symbol != null)
        {
            // A LOCAL is a local, whatever it is called. The heuristics at the bottom read a leading
            // underscore as a field — a fair guess with no model, and a WRONG answer with one: a
            // generated `var _password = form.Add(…)` came back as `this._password`, which inside a
            // static method is undefined and throws on the first use.
            // A primary-constructor PARAMETER is excluded on purpose: C# 12 lets an instance member
            // read one, and there it behaves like a field (handled below). The name still goes
            // through the JS-identifier rename, or a local called `new` would emit `new`.
            if (symbol.Kind is SymbolKind.Local or SymbolKind.RangeVariable
                || (symbol.Kind == SymbolKind.Parameter && !symbol.IsPrimaryConstructorParameter()))
                return JsExpr.Identifier(name.ToJsIdentifier());

            // A LOCAL FUNCTION is a function in SCOPE, not a member — whatever its containing type
            // says. It compiles to a `const` arrow beside the code that calls it, so a reference to
            // it is the name: `this.row` reads it off an object that never had it, and the `.bind`
            // below then throws on `undefined` where the C# ran perfectly. Only the browser sees it
            // (the server runs the C#), which is the worst place for a difference to live.
            // InvocationStrategy already excludes local functions on three paths; this is the fourth.
            if (symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction })
                return JsExpr.Identifier(name.ToCamelCase().ToJsIdentifier());

            if (symbol.Kind == SymbolKind.Field || symbol.Kind == SymbolKind.Property || symbol.Kind == SymbolKind.Method)
            {
                // A STATIC member is reached through the class, not the instance: a bare `Items` reference
                // to `static Items` on `Widget` must emit `Widget.items`, never `this.items` (which the
                // uppercase fallback below would otherwise produce, leaving the value undefined at runtime).
                // This holds for a static METHOD passed as a delegate too (`onPressed: Helper` → the method
                // group `Widget.helper`; no `.bind`, statics have no receiver).
                if (symbol.IsStatic && symbol.ContainingType != null)
                {
                    return isMemberName
                        ? JsExpr.Identifier(name.ToCamelCase())
                        : JsExpr.Member(JsExpr.Identifier(symbol.ContainingType.Name), name.ToCamelCase());
                }

                // If it's a member of the current class and not static, add 'this.'
                if (!symbol.IsStatic && symbol.ContainingType != null)
                {
                    if (isMemberName) return JsExpr.Identifier(name.ToCamelCase());

                    var member = JsExpr.ThisMember(name.ToCamelCase());

                    // A method REFERENCE (not being called) is a method group: bind it to the instance.
                    if (symbol is IMethodSymbol)
                    {
                        var isDirectInvocation = identifier.Parent is InvocationExpressionSyntax invocation &&
                                              invocation.Expression == identifier;
                        if (!isDirectInvocation)
                            return JsExpr.Call(JsExpr.Member(member, "bind"), JsExpr.This);
                    }

                    return member;
                }
            }

            // C# 12 primary-constructor parameter captured in an instance member (e.g. referenced in Build):
            // it behaves like an instance field, so emit `this.<name>`.
            if (symbol.IsPrimaryConstructorParameter())
            {
                return isMemberName
                    ? JsExpr.Identifier(name.ToCamelCase())
                    : JsExpr.ThisMember(name.ToCamelCase());
            }
        }

        // Fallback Heuristics
        if (name.StartsWith("_"))
        {
            return JsExpr.ThisMember(name);
        }

        // If it starts with Uppercase and not obviously a local/param, it's likely a property
        if (char.IsUpper(name[0]))
        {
            return isMemberName
                ? JsExpr.Identifier(name.ToCamelCase())
                : JsExpr.ThisMember(name.ToCamelCase());
        }

        return JsExpr.Identifier(name.ToJsIdentifier());
    }

    public int Priority => 10;
}
