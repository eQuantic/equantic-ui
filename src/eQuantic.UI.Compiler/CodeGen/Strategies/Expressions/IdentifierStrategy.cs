using System.Linq;
using eQuantic.UI.Compiler.CodeGen.Extensions;
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

        // Priority: Semantic Check > String Check (Fallback)
        var symbol = context.SemanticHelper.GetSymbol(identifier);

        // Map 'Component' property (in State classes) to 'this._component', but not a name bound in
        // scope: a local, a parameter or a local function called `Component` is that binding, and
        // mapped first it read the inherited value instead (Copilot's review of #399).
        if (name == "Component" && !BoundInScope(symbol, identifier, name)) return JsExpr.ThisMember("_component");

        // A nested type of the app's is its twin, named by its owner (#584): `Ops` inside Calc is
        // `Calc$Ops`, which a top-level `Ops` can never be.
        if (symbol is INamedTypeSymbol nested && nested.NestedTwinName() is { } twin)
        {
            nested.RegisterIntroduced(context);
            return JsExpr.Identifier(twin);
        }

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
            // The name is the one its declaration takes, renamed where the member holds it already.
            if (symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction } localFunction)
                return JsExpr.Identifier(LocalFunctionName.Of(localFunction));

            if (symbol.Kind == SymbolKind.Field || symbol.Kind == SymbolKind.Property || symbol.Kind == SymbolKind.Method)
            {
                // A STATIC member is reached through the class, not the instance: a bare `Items` reference
                // to `static Items` on `Widget` must emit `Widget.items`, never `this.items` (which the
                // uppercase fallback below would otherwise produce, leaving the value undefined at runtime).
                // This holds for a static METHOD passed as a delegate too (`onPressed: Helper` → the method
                // group `Widget.helper`; no `.bind`, statics have no receiver).
                if (symbol.IsStatic && symbol.ContainingType != null)
                {
                    // A method GROUP names the symbol without calling it — `Func<…> f = Usable` emits
                    // `FaceName.usable` just as a call would, and fails at hydration just as hard.
                    symbol.ReportIfHostOnly(identifier, context);

                    // An enum's member reached bare (`using static`) is the member, as its qualified
                    // spelling is: it was `Level.high`, a member of a class nothing defines (#485).
                    if (!isMemberName && symbol is IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum } enumMember)
                        return JsExpr.Literal(Types.EnumStrategy.MemberLiteral(enumMember));

                    // A .NET type's member reached bare that no strategy claimed has no translation:
                    // the class-static rule below is for the types the transpiler EMITS (#485).
                    if (!isMemberName && symbol.ReportIfPlatformReachedBare(identifier, context))
                        return JsExpr.Literal("undefined");
                    return isMemberName
                        ? JsExpr.Identifier(name.ToCamelCase())
                        : JsExpr.Member(JsExpr.Identifier(StaticHome(symbol.ContainingType, context)), name.ToCamelCase());
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
            // it behaves like an instance field, so emit `this.<name>`. In a twin constructor's own
            // initializers it is that constructor's parameter, named as the constructor names it: a
            // legal JavaScript name too, since a parameter `class` does not parse where `this.class` does.
            if (symbol.IsPrimaryConstructorParameter())
            {
                if (isMemberName) return JsExpr.Identifier(name.ToCamelCase());
                return context.ConstructorParametersInScope
                    ? JsExpr.Identifier(name.ToCamelCase().ToJsIdentifier())
                    : JsExpr.ThisMember(name.ToCamelCase());
            }
        }

        // With no model to ask, a name can still be a local function a block around it declares,
        // which C# finds before any member: its declaration's name, not a guessed `this.<name>`.
        if (symbol == null && !isMemberName && LocalFunctionName.InScope(identifier, name) is { } local)
            return JsExpr.Identifier(LocalFunctionName.Of(local, context));

        // ...and so is a local or a parameter of a scope around it, which the heuristics below would
        // read as a member for its capital: `int Component = 4; return Component;` came out as
        // `this.component` (Copilot's review of #399).
        if (symbol == null && !isMemberName && LocalFunctionName.IsBoundAt(identifier, name))
            return JsExpr.Identifier(name.ToJsIdentifier());

        // A source-directory scan can prove that an otherwise-unbound PascalCase receiver is a
        // top-level static/runtime type. Preserve the type name so the emitter can route its import;
        // do not turn it into an instance member purely by casing.
        var isReceiver = (identifier.Parent as MemberAccessExpressionSyntax)?.Expression == identifier;
        if (isReceiver && context.CanGuess(identifier) && context.IsFallbackTypeReceiver(name))
            return JsExpr.Identifier(name);

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

    /// <summary>Whether <paramref name="name"/> reaches a binding of the scope it is read in, a local,
    /// a parameter, a range variable or a local function, rather than a member.</summary>
    private static bool BoundInScope(ISymbol? symbol, SyntaxNode at, string name) => symbol switch
    {
        // With no model, a local or a parameter of a scope around it is as much a binding as a local
        // function: `int Component = 4; return Component;` read the component's `_component`.
        null => LocalFunctionName.IsBoundAt(at, name),
        IMethodSymbol { MethodKind: MethodKind.LocalFunction } => true,
        { Kind: SymbolKind.Local or SymbolKind.RangeVariable } => true,
        { Kind: SymbolKind.Parameter } => !symbol.IsPrimaryConstructorParameter(),
        _ => false,
    };

    public int Priority => 10;

    /// <summary>The twin a static member reached bare lives on: its type's, named by its owner where it is
    /// nested (#584), and imported where it is another type's, as an owner's static read from inside a
    /// nested type is.</summary>
    private static string StaticHome(INamedTypeSymbol type, ConversionContext context)
    {
        if (type.Locations.Any(location => location.IsInSource)) type.RegisterIntroduced(context);
        return type.TwinReference();
    }

}
