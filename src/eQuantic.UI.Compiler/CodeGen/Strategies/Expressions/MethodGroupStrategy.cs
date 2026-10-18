using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// A method GROUP of a member no twin carries by name, the platform's or a package's
/// (<see cref="TwinlessTypeExtensions.IsTwinless"/>): the delegate its call is, an arrow over the
/// method's own parameters, which are the delegate's, whose body is the call those parameters make as
/// the strategies translate it (#839). So the group answers as the call answers: <c>Select(DateTime.Parse)</c>
/// reads each text in the page's culture, and the index <c>Select</c> hands its callback never reaches the
/// twin's culture flag; <c>Func&lt;string, string&gt; f = ts.ToString</c> writes the format it is handed.
/// Where the call has no translation, the group has none either, and the call's refusal is the group's,
/// reported where the group is written: an untranslated group was written as a name, unbound or called
/// with no argument, and built with no diagnostic.
/// <para>
/// The call is a CALL the strategies are asked for, as they are for any other: the group's own spelling
/// called with the method's parameters, bound by the model where the group is written
/// (<see cref="Services.SemanticHelper.Speculate"/>). A receiver is read once,
/// where the delegate is made, as C# reads it: a value copied as C# boxes it (<see cref="ValueCopies"/>),
/// and a null one refused there, as .NET refuses to make the delegate (an <c>ArgumentException</c> for a
/// method it calls directly, a <c>NullReferenceException</c> for one it looks up on the instance). A group
/// made on <c>this</c> needs no copy: an arrow keeps the <c>this</c> it is written in. A group whose method
/// hands a value back through an <c>out</c> or a <c>ref</c> has no delegate the browser can call.
/// </para>
/// <para>
/// Every strategy that claims a member access, a name or a generic name is passed by first, so no one of
/// them can write a group as a name of its own: this is the one lowering of a group of a member no twin
/// carries. A group of the app's own members, and of the framework's, whose twins carry them, is the
/// member bound to its receiver (<see cref="MemberAccessStrategy"/>, <see cref="IdentifierStrategy"/>).
/// </para>
/// </summary>
public class MethodGroupStrategy : IExpressionIrStrategy
{
    /// <summary>The name the receiver a delegate is made over is held by, inside the delegate, as C# spells it in
    /// the speculated call: <see cref="Holder"/> sets it apart from the method's own parameters.</summary>
    private const string Target = "__target";

    public bool CanConvert(SyntaxNode node, ConversionContext context) => Group(node, context) is not null;

    /// <summary>The method <paramref name="node"/> names without calling it, when no twin carries it; null
    /// for anything else, a call and the name part of a member access included.</summary>
    private static IMethodSymbol? Group(SyntaxNode node, ConversionContext context)
    {
        if (node is not (MemberAccessExpressionSyntax or IdentifierNameSyntax or GenericNameSyntax)) return null;
        // The name a member access, a call or a ?. binding stands on is part of that node.
        if (node.Parent is MemberAccessExpressionSyntax owner && owner.Name == node) return null;
        if (node.Parent is InvocationExpressionSyntax call && call.Expression == node) return null;
        if (node.Parent is MemberBindingExpressionSyntax) return null;
        // `base.M` is the base's method on this object, which the member-access path binds.
        if (node is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax }) return null;
        if (context.SemanticHelper.GetSymbol(node) is not IMethodSymbol { ContainingType: { } declaring } method
            || !declaring.IsTwinless())
            return null;
        // A receiver of a type no source can name, an anonymous object's, keeps the lowering its members
        // have (MemberAccessStrategy): its call cannot be written where the group is.
        return Receiver((ExpressionSyntax)node, method) is { } receiver
            && context.SemanticHelper.GetType(receiver) is { } held && !Nameable(held)
            ? null
            : method;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var group = (ExpressionSyntax)node;
        var method = Group(node, context)!;
        if (method.Parameters.Any(parameter => parameter.RefKind is RefKind.Out or RefKind.Ref))
            return Refused(node, method, "it hands a value back through an out or a ref parameter, and no function "
                + "the browser calls can. Call it where its value is needed.", context);

        var receiver = Receiver(group, method);
        if (Call(group, method, receiver, context) is not { } invocation)
            return Refused(node, method, "the types of its call cannot be written where the group is. Write the "
                + "lambda that calls it, which is translated as the call is.", context);
        var captured = receiver is null ? null : Captured(receiver, method, context);

        // The call's diagnostics were raised at a call that has no place in the file: each is the group's.
        var reported = context.Diagnostics.Count;
        var (call, bound) = context.Temporaries.In(() => context.Converter.ConvertIr(invocation));
        var raised = context.Diagnostics.Skip(reported).ToList();
        context.Diagnostics.RemoveRange(reported, raised.Count);
        foreach (var diagnostic in raised) context.Report(node, diagnostic.Severity, diagnostic.Code, diagnostic.Message);
        if (raised.Any(diagnostic => diagnostic.Severity == ConversionSeverity.Error)) return JsExpr.Identifier("undefined");

        var parameters = string.Join(", ", method.Parameters.Select(parameter =>
            LambdaExpressionStrategy.Typed(Named(parameter).ToJsIdentifier(), parameter.Type, context)));
        var arrow = bound.Count == 0
            ? JsExpr.Arrow(parameters, call)
            : JsExpr.ArrowBlock(parameters, context.Converter.ExpressionBodyBlock(invocation, call, bound, returns: true),
                context.Layout, context.Depth);
        return captured is null ? arrow : JsExpr.Call(JsExpr.Group(JsExpr.Arrow(Holder(method), arrow)), captured);
    }

    /// <summary>EQ2004 for a group the browser has no delegate of, naming its method, with the reason.</summary>
    private static JsExpr Refused(SyntaxNode node, IMethodSymbol method, string reason, ConversionContext context)
    {
        context.Report(node, ConversionSeverity.Error, "EQ2004",
            $"'{method.ContainingType.ToDisplayString()}.{method.Name}' has no JavaScript translation as a delegate: {reason}");
        return JsExpr.Identifier("undefined");
    }

    /// <summary>What the delegate is made over and must hold: the receiver a group of an instance method or
    /// of a reduced extension is written on, unless it is <c>this</c>, which the arrow keeps. Null for a
    /// static method and a group written bare.</summary>
    private static ExpressionSyntax? Receiver(ExpressionSyntax group, IMethodSymbol method) =>
        group is MemberAccessExpressionSyntax { Expression: var receiver } && !method.IsStatic
            && receiver is not ThisExpressionSyntax
            ? receiver
            : null;

    /// <summary>
    /// The receiver as the delegate holds it, read once where the delegate is made: a mutable value
    /// copied as C# boxes it, and a reference refused there when it is null, as .NET refuses to make a
    /// delegate over one. So is an empty <c>Nullable&lt;T&gt;</c>: the only groups C# lets it make are of
    /// object's virtual methods (CS1728), over its value boxed, and it boxes to null, so its <c>Equals</c>
    /// group answered where .NET throws. A reduced extension takes its receiver as an argument, and a null
    /// one is no error until the delegate is called.
    /// </summary>
    private static JsExpr Captured(ExpressionSyntax receiver, IMethodSymbol method, ConversionContext context)
    {
        var type = context.SemanticHelper.GetType(receiver);
        var converted = ValueCopies.Captured(type, context.Converter.ConvertIr(receiver));
        if (method.MethodKind == MethodKind.ReducedExtension || (type is not { IsReferenceType: true } && !type.IsNullableValue())
            || receiver is LiteralExpressionSyntax or InterpolatedStringExpressionSyntax or BaseObjectCreationExpressionSyntax)
            return converted;
        context.UsedHelpers.Add(Eq.Import);
        // A virtual method is looked up on the instance, and a delegate's own Invoke is called directly.
        var looksUp = method.MethodKind != MethodKind.DelegateInvoke
            && (method.IsVirtual || method.IsOverride || method.IsAbstract || method.ContainingType.TypeKind == TypeKind.Interface);
        return JsExpr.Call(JsExpr.Identifier(looksUp ? Eq.Receiver : Eq.DelegateTarget), converted);
    }

    /// <summary>
    /// The call the group stands for, BOUND where the group is written: a lambda over the method's own
    /// parameters, typed as the method types them, whose body is the group's own spelling called with
    /// them, and <see cref="Target"/>, typed as the receiver, in its receiver's place where the delegate
    /// holds one. The lambda is speculated where the group is, as a statement inside a body and as an
    /// initializer outside one, and the model of that speculation answers the strategies for every node of
    /// it (<see cref="Services.SemanticHelper.Speculate"/>): the call is bound as any other is, its
    /// operation, its arguments and their conversions. Null where a type cannot be written there (an
    /// anonymous one) or the call binds another method than the group does.
    /// </summary>
    private static InvocationExpressionSyntax? Call(ExpressionSyntax group, IMethodSymbol method, ExpressionSyntax? receiver,
        ConversionContext context)
    {
        // A group a strategy rebuilt, inside a ?. chain or a query, stands for the node it was written as, where
        // the model knows its scope: the call is bound there. Asked of the rebuilt node, no model knew it, and
        // the group was refused (Copilot's second review of #958).
        var written = context.SemanticHelper.Original(group) as ExpressionSyntax ?? group;
        if (context.SemanticModel is not { } model || !ReferenceEquals(written.SyntaxTree, model.SyntaxTree)) return null;
        var parameters = new List<ParameterSyntax>();
        foreach (var parameter in method.Parameters)
        {
            if (Written(parameter.Type) is not { } type) return null;
            parameters.Add(Parameter(Named(parameter), type));
        }
        var arguments = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(
            method.Parameters.Select(parameter => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(Identifier(Named(parameter)))))));
        ExpressionSyntax callee = written.WithoutTrivia();
        if (receiver is not null)
        {
            if (Written(context.SemanticHelper.GetType(receiver) ?? method.ContainingType) is not { } held) return null;
            callee = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(Holder(method)), ((MemberAccessExpressionSyntax)written).Name.WithoutTrivia());
            parameters.Insert(0, Parameter(Holder(method), held));
        }
        // The lambda over the parameters a held receiver is one more of: its call is the inner one.
        var call = SyntaxFactory.InvocationExpression(callee, arguments);
        ExpressionSyntax lambda = receiver is null
            ? SyntaxFactory.ParenthesizedLambdaExpression(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)), call)
            : SyntaxFactory.ParenthesizedLambdaExpression(SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(parameters[0])),
                SyntaxFactory.ParenthesizedLambdaExpression(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters.Skip(1))), call));

        // A statement inside a body, where the group's scope is; an initializer outside one.
        SyntaxNode root = SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName("_"), lambda));
        if (!model.TryGetSpeculativeSemanticModel(written.SpanStart, (StatementSyntax)root, out var speculated))
        {
            root = SyntaxFactory.EqualsValueClause(SyntaxFactory.CastExpression(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)), SyntaxFactory.ParenthesizedExpression(lambda)));
            if (!model.TryGetSpeculativeSemanticModel(written.SpanStart, (EqualsValueClauseSyntax)root, out speculated)) return null;
        }
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Last();
        if (!SymbolEqualityComparer.Default.Equals(speculated.GetSymbolInfo(invocation).Symbol, method)) return null;
        context.SemanticHelper.Speculate(root, speculated);
        return invocation;
    }

    /// <summary>
    /// The name that holds the receiver, apart from every parameter of <paramref name="method"/>: a parameter
    /// named <see cref="Target"/> hid the receiver in the speculated call, which then bound nothing, and the
    /// group was refused (Copilot's review of #958).
    /// </summary>
    private static string Holder(IMethodSymbol method)
    {
        var name = Target;
        while (method.Parameters.Any(parameter => Named(parameter) == name)) name = "_" + name;
        return name;
    }

    /// <summary>A parameter of the speculated lambda, typed as C# writes the type.</summary>
    private static ParameterSyntax Parameter(string name, string type) =>
        SyntaxFactory.Parameter(Identifier(name)).WithType(SyntaxFactory.ParseTypeName(type));

    /// <summary>A name as an identifier, a keyword escaped: the parameter's name is its value either way.</summary>
    private static SyntaxToken Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None
            ? SyntaxFactory.Identifier(name)
            : SyntaxFactory.Identifier(default, SyntaxKind.IdentifierToken, "@" + name, name, default);

    /// <summary>A type as C# writes it anywhere, or null for one no source can name.</summary>
    private static string? Written(ITypeSymbol type) =>
        Nameable(type) ? type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) : null;

    private static bool Nameable(ITypeSymbol type) => type switch
    {
        { TypeKind: TypeKind.Error or TypeKind.Pointer or TypeKind.FunctionPointer } => false,
        INamedTypeSymbol { IsAnonymousType: true } => false,
        IArrayTypeSymbol array => Nameable(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.All(Nameable) && (named.ContainingType is null || Nameable(named.ContainingType)),
        _ => true,
    };

    /// <summary>A parameter's name, and one by its place where the metadata gives it none.</summary>
    private static string Named(IParameterSymbol parameter) =>
        parameter.Name.Length > 0 ? parameter.Name : $"arg{parameter.Ordinal}";

    public int Priority => 70; // Above every strategy that claims a member access, a name or a generic name.
}
