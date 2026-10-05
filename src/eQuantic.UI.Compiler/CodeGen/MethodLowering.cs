using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// How a C# method becomes a member of its type's twin, and how a member's body is lowered: one
/// path for a class, a component, a record and a struct (#432).
/// <para>
/// The record and struct emitter kept its own copy of the method, and the copy had been taught none
/// of what this path handles. An <c>async</c> method's <c>await</c> landed outside an async function
/// and the whole module failed to parse; an iterator's <c>yield</c> did the same; an <c>out</c> or
/// <c>ref</c> parameter stayed in the signature; an expression body read the variable its pattern
/// binds (<c>o is SE m &amp;&amp; m.V == V</c>) before anything declared it; and a parameter was
/// declared by its camel case where every reference in the body names it by its JavaScript
/// identifier. A shape taught here reaches every type that has methods.
/// </para>
/// </summary>
internal sealed class MethodLowering
{
    /// <summary>The array an iterator method fills — one name, so every emitter agrees.</summary>
    private const string IteratorBufferName = "_seq";

    private readonly CSharpToJsConverter _converter;
    private readonly Func<bool> _typeAnnotations;
    private readonly Func<SyntaxNode, SemanticModel?> _modelFor;

    /// <param name="converter">The converter of the emitter that owns the member.</param>
    /// <param name="typeAnnotations">Whether the emitter writes TypeScript, asked at each member:
    /// the class emitter's flag is a property its caller sets, and the record emitter's changes with
    /// each type it is asked to emit.</param>
    /// <param name="modelFor">The semantic model of a node's own file, as the emitter finds it: a
    /// default an interface supplies is written into a class declared in another one.</param>
    public MethodLowering(CSharpToJsConverter converter, Func<bool> typeAnnotations,
        Func<SyntaxNode, SemanticModel?> modelFor)
    {
        _converter = converter;
        _typeAnnotations = typeAnnotations;
        _modelFor = modelFor;
    }

    private bool TypeAnnotations => _typeAnnotations();

    /// <summary>
    /// A method as a class member: its modifiers, its name, its type parameters where the output is
    /// TypeScript, its parameters and its body. An <c>out</c> parameter leaves the signature, since
    /// what it carries comes back in the returned object (<see cref="OutParameters"/>). Null for a
    /// method with nothing to emit: an abstract one, which TypeScript needs no stub for, or one with
    /// no body.
    /// </summary>
    /// <param name="method">The method, with a body or without one.</param>
    /// <param name="asStatic">Emit it as a static member whatever its modifiers say: a static
    /// class's members are its twin's statics.</param>
    /// <param name="declaredType">The emitter's annotation for a parameter's declared type.</param>
    /// <param name="withReceiver">For a C# 14 extension member, what puts its receiver in front of
    /// the parameters: the member lowers to a static that takes the receiver first.</param>
    /// <param name="returns">The emitter's return annotation for the method's declared return type:
    /// a tuple's is said, since a tuple crosses as an array literal that TypeScript would read as an
    /// array of the union of its elements (#412).</param>
    public JsClassMember? Method(MethodDeclarationSyntax method, bool asStatic, Func<TypeSyntax?, string> declaredType,
        Func<string, string>? withReceiver = null, Func<TypeSyntax, string>? returns = null)
    {
        if (method.Modifiers.Any(SyntaxKind.AbstractKeyword)) return null;
        if (method.Body == null && method.ExpressionBody == null) return null;

        var byReference = OutParameters.Of(method.ParameterList);
        var text = method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? "";
        var parameters = string.Join(", ", method.ParameterList.Parameters
            .Where(parameter => !OutParameters.IsOut(parameter))
            .Select(parameter =>
            {
                // A parameter the body never mentions takes the underscore convention — the
                // interface a tokenizer implements hands over state that a simple language never
                // reads, and the runtime's own build rejects an unused name.
                var name = text.Contains(parameter.Identifier.Text)
                    ? parameter.Identifier.Text.ToJsIdentifier()
                    : "_" + parameter.Identifier.Text.ToJsIdentifier();
                // An optional parameter keeps its default, converted for the parameter's type, so a
                // caller that omits the trailing arguments runs it.
                return ParamWithDefault(name, declaredType(parameter.Type),
                    parameter.Default is null
                        ? null
                        : _converter.ConvertExpression(parameter.Default.Value, parameter.Type?.ToString()),
                    parameter.Modifiers.Any(SyntaxKind.ParamsKeyword));
            }));

        if (withReceiver is not null) parameters = withReceiver(parameters);
        var isAsync = IsAsync(method);
        var isIterator = method.Body.IsIteratorBody();
        if (isIterator) ReportIfEndless(method.Body);
        // A generic method keeps its type parameters in a TypeScript signature (`also<T>(node: T)`):
        // the constraints drop, since TypeScript needs none of them to bind. Plain JavaScript has no
        // type parameters, and `<T>` there is a syntax error.
        var generics = TypeAnnotations && method.TypeParameterList is { Parameters.Count: > 0 } list
            ? $"<{string.Join(", ", list.Parameters.Select(parameter => parameter.Identifier.Text))}>"
            : "";
        var body = Body(method.Body, method.ExpressionBody?.Expression, isIterator, byReference);
        var modifiers = (method.Modifiers.Any(SyntaxKind.StaticKeyword) || asStatic ? "static " : "")
            + (isAsync ? "async " : "");
        return JsClassMember.Method(modifiers, method.Identifier.Text.ToCamelCase(), generics, parameters,
            returns?.Invoke(method.ReturnType) ?? "", body);
    }

    /// <summary>
    /// An instance indexer as the two methods its twin carries (#427): its getter as
    /// <c>item(…)</c> and its setter as <c>setItem(…, value)</c>, which answers the value it wrote,
    /// as C#'s assignment does, so a call site can use it as the assignment's value. A setter that
    /// returns early runs in an arrow of its own, so its <c>return;</c> ends the setter and not the
    /// answer. One path for a class, a component, a record, a struct and an interface's default.
    /// </summary>
    /// <param name="indexer">The indexer, its accessors bodied or expression-bodied.</param>
    /// <param name="declaredType">The emitter's annotation for a declared type.</param>
    public IEnumerable<JsClassMember> Indexer(IndexerDeclarationSyntax indexer, Func<TypeSyntax?, string> declaredType)
    {
        var keys = string.Join(", ", indexer.ParameterList.Parameters.Select(parameter =>
            Param(parameter.Identifier.ValueText.ToJsIdentifier(), declaredType(parameter.Type))));
        var annotation = TypeAnnotations ? $": {declaredType(indexer.Type)}" : "";
        var get = indexer.AccessorList?.Accessors.FirstOrDefault(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
        var getter = indexer.ExpressionBody is { } arrow ? Body(null, arrow.Expression, isIterator: false, [])
            : get?.ExpressionBody is { } getArrow ? Body(null, getArrow.Expression, isIterator: false, [])
            : get?.Body is { } getBlock ? AccessorBody(getBlock)
            : null;
        if (getter is not null)
            yield return JsClassMember.Method("", Strategies.Expressions.Indexer.Get, "", keys, annotation, getter)
                with { Origin = new JsOrigin(indexer) };

        var set = indexer.AccessorList?.Accessors.FirstOrDefault(accessor => accessor.IsKind(SyntaxKind.SetAccessorDeclaration)
            || accessor.IsKind(SyntaxKind.InitAccessorDeclaration));
        JsStatement? written = set?.ExpressionBody is { } setArrow ? ExpressionBody(setArrow.Expression, returns: false)
            : set?.Body is { } setBlock ? AccessorBody(setBlock)
            : null;
        if (written is null) yield break;
        if (set!.Body?.DescendantNodes().OfType<ReturnStatementSyntax>().Any() == true)
            written = JsStatement.Raw($"(() => {JsStatementWriter.Write(written, JsLayout.Compact)})();");
        IReadOnlyList<JsStatement> statements = written is JsBlock block ? block.Statements : [written];
        var parameters = string.IsNullOrEmpty(keys)
            ? Param("value", declaredType(indexer.Type))
            : $"{keys}, {Param("value", declaredType(indexer.Type))}";
        yield return JsClassMember.Method("", Strategies.Expressions.Indexer.Set, "", parameters, "",
            JsStatement.Block([.. statements, JsStatement.Raw("return value;")])) with { Origin = new JsOrigin(set) };
    }

    /// <summary>
    /// Whether a method's twin is an async function: an <c>async</c> method, whose body awaits, and
    /// one that returns a <c>Task</c> without awaiting, whose value is a Promise either way. Asked of
    /// the return type's symbol: the name alone made a method returning a type called
    /// <c>TaskItem</c> async, and its callers read a Promise (#432). The name decides only where
    /// there is no model to ask.
    /// </summary>
    public bool IsAsync(MethodDeclarationSyntax method)
    {
        if (method.Modifiers.Any(SyntaxKind.AsyncKeyword)) return true;
        var model = _modelFor(method.ReturnType);
        var returned = model?.GetSymbolInfo(method.ReturnType).Symbol as ITypeSymbol
            ?? model?.GetTypeInfo(method.ReturnType).Type;
        if (returned is null || returned.TypeKind == TypeKind.Error)
            return method.ReturnType.ToString().StartsWith("Task");
        return returned.OriginalDefinition.ToDisplayString()
            is "System.Threading.Tasks.Task" or "System.Threading.Tasks.Task<TResult>";
    }

    /// <summary>
    /// An accessor's block as its body, lowered as a method's is: a getter that yields fills a
    /// buffer and returns it, where it wrote <c>yield</c> outside a generator and the module did
    /// not parse (#432).
    /// </summary>
    public JsStatement AccessorBody(BlockSyntax block)
    {
        var isIterator = block.IsIteratorBody();
        if (isIterator) ReportIfEndless(block);
        return Body(block, null, isIterator, []);
    }

    /// <summary>
    /// A member's body as IR, so each statement reaches the class writer carrying the C# it came
    /// from and the source map leads a frame or a breakpoint to it (#293): converted as text, the
    /// body lost every origin before the writer saw it, and the map stopped at the method's first
    /// line. The variables the body's expressions declare are each statement's to declare
    /// (ExpressionVariableScanner, #484), with C#'s scope.
    /// <para>
    /// The two shapes a lowering wraps around the body are IR as well: an iterator's buffer, and the
    /// arrow an <c>out</c> or <c>ref</c> parameter's body runs in (<see cref="OutParameters.Body"/>).
    /// Both wrapped the body's TEXT, so neither body mapped a single line (#487).
    /// </para>
    /// </summary>
    public JsStatement Body(BlockSyntax? block, ExpressionSyntax? expressionBody, bool isIterator,
        IReadOnlyList<ParameterSyntax> byReference)
    {
        if (byReference.Count > 0) return OutParameters.Body(block, expressionBody, byReference, _converter);
        if (block is null)
            return expressionBody is null ? JsStatement.Block([]) : ExpressionBody(expressionBody, returns: true);
        if (!isIterator) return _converter.ConvertBlockIr(block);

        // An iterator's yields lower to pushes onto a buffer the body fills and returns, so the
        // buffer is named while the body converts.
        JsStatement converted;
        _converter.SetIteratorBuffer(IteratorBufferName);
        try
        {
            converted = _converter.ConvertBlockIr(block);
        }
        finally
        {
            _converter.SetIteratorBuffer(null);
        }
        // The buffer's two lines are the declaration's, as an out parameter's wrapper is: the body's
        // statements carry their own origins, and without one here those two inherited none.
        return JsStatement.Block([
            JsStatement.Const(IteratorBufferName, JsExpr.Array([])),
            .. converted is JsBlock filled ? filled.Statements : [converted],
            JsStatement.Return(JsExpr.Identifier(IteratorBufferName)),
        ]) with { Origin = block.Parent ?? block };
    }

    /// <summary>
    /// An expression body as the block of its member: a return, or the bare statement where a
    /// setter or a constructor has nothing to return, with what it declares in front, all carrying
    /// the expression, so the map leads a frame in it to its line (#293). The concise body's one
    /// lowering (<see cref="CSharpToJsConverter.ConvertExpressionBodyIr"/>), the one a lambda's
    /// takes: written here as a raw statement, it took a lambda inside it to the text, and every line
    /// of that lambda's block with it (#492).
    /// </summary>
    public JsBlock ExpressionBody(ExpressionSyntax expression, bool returns) =>
        _converter.ConvertExpressionBodyIr(expression, returns);

    /// <summary>One parameter in a hand-written signature: annotated in TypeScript mode, bare in
    /// plain-JavaScript mode.</summary>
    public string Param(string name, string type) => TypeAnnotations ? $"{name}: {type}" : name;

    /// <summary>A parameter with its default, or a rest parameter.</summary>
    public string ParamWithDefault(string name, string type, string? convertedDefault,
        bool rest = false) =>
        // `params xs` is a REST parameter. Emitted as a plain one it bound only the FIRST argument
        // (`Count("a", "b")` answered 1) and was undefined when none were passed, which threw on
        // the first read of `.length`.
        rest ? $"...{Param(name, type)}"
        : convertedDefault is null ? Param(name, type)
        : convertedDefault == "undefined" && TypeAnnotations ? $"{name}?: {type}"
        : $"{Param(name, type)} = {convertedDefault}";

    /// <summary>
    /// An iterator that never ends is ordinary C# — the caller stops taking. Materialised into an
    /// array it is a hang, and a hung tab says nothing about why. Named here instead.
    /// </summary>
    public void ReportIfEndless(BlockSyntax? body)
    {
        if (body.FindEndlessYieldLoop() is not { } loop) return;
        _converter.Report(loop, ConversionSeverity.Error, "EQ2005",
            "This iterator never finishes, and iterators are MATERIALISED into an array — the loop "
            + "would run forever instead of yielding lazily. Give the loop an end (a bound, a "
            + "`yield break`), or take what you need inside it and return a finished sequence.");
    }

}
