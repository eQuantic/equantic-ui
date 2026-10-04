using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// What a C# deconstruction destructures, ONE lowering for its three shapes: the declaration
/// (<c>var (a, b) = p</c>), the assignment (<c>(a, b) = p</c>, <c>(var a, b) = p</c>) and the loop
/// (<c>foreach (var (a, b) in ps)</c>). A tuple, and a dictionary's pair, are arrays here and
/// destructure by position. Anything else deconstructs through the <c>Deconstruct</c> the bound
/// tree says each level calls. A record's own, and a BCL type's, read the members its out
/// parameters name (a record's positional properties). One the app wrote is CALLED, and its outs
/// come back as the object every method with outs returns (<see cref="OutParameters"/>), so a
/// <c>Deconstruct</c> that computes a part, or names it differently from a member, still answers as
/// it does in .NET. A nested deconstruction nests the two kinds, each part by its own type; a nested
/// level whose <c>Deconstruct</c> the app wrote is a <see cref="Step"/> of its own, since a pattern
/// cannot call one.
/// <para>
/// Only the declaration had it, from the type's first <c>Deconstruct</c> whatever its arity: the
/// assignment and the loop wrote array destructuring, and a record is not iterable, so
/// <c>(a, b) = point</c> and <c>foreach (var (a, b) in points)</c> threw <c>{} is not iterable</c>,
/// and a nested declaration left its inner names undeclared (#486).
/// </para>
/// </summary>
internal static class DeconstructionPattern
{
    /// <summary>
    /// A nested level destructured on its own: bound to <paramref name="Temporary"/> in its parent's
    /// pattern, then destructured by <paramref name="Pattern"/> from what <paramref name="Called"/>
    /// hands back, a <c>Deconstruct</c> the app wrote, or from the temporary itself when there is none
    /// to call. A step comes after the one that binds its temporary, and steps run in the order C#
    /// deconstructs, each level before the next one to its right.
    /// </summary>
    internal sealed record Step(string Temporary, string Pattern, IMethodSymbol? Called);

    /// <summary>
    /// A deconstruction's destructuring: the top level's <paramref name="Pattern"/>, the
    /// <c>Deconstruct</c> of the app's its value goes through first, if any, the nested levels that
    /// need a call of their own, and, written with temporaries, the temporaries every part was bound
    /// to, each with the write that puts it in its target (none for a discard), the tuple they make,
    /// which is the value of an assignment, and what the targets <paramref name="Captures"/>: each
    /// name, with the receiver or the index it holds, evaluated before the value.
    /// <paramref name="Converts"/> says a part changes its representation on its way into its target
    /// (an int part into a long), which destructuring cannot do, and <paramref name="Declared"/> holds a
    /// declaration's converted parts, each declared after the destructuring from its temporary.
    /// </summary>
    internal sealed record Lowered(
        string Pattern,
        IMethodSymbol? Called,
        IReadOnlyList<Step> Steps,
        IReadOnlyList<string> Temporaries,
        IReadOnlyList<(string Temporary, string Write)> Assignments,
        string Tuple,
        IReadOnlyList<(string Name, string Value)> Captures,
        bool Converts,
        IReadOnlyList<(string Name, string Value)> Declared);

    /// <summary>
    /// The destructuring of <paramref name="left"/> (an assignment's tuple, a declaration's
    /// designation, a loop's variable) from a value of <paramref name="type"/>. Null when no level of
    /// it deconstructs through a <c>Deconstruct</c>, tuples all the way down, which the array
    /// destructuring every caller already writes is right for, unless its targets
    /// <paramref name="capture"/>. <paramref name="temporaries"/> binds every part to a temporary
    /// instead of its target, for an assignment whose value is read, that has a step or whose targets
    /// capture: the targets are then assigned from them, and the tuple built of them.
    /// </summary>
    internal static Lowered? Of(SyntaxNode left, DeconstructionInfo? info, ITypeSymbol? type,
        ConversionContext context, bool temporaries = false, bool capture = false)
    {
        if (Parts(left) is not { } parts) return null;
        var top = Positional(type) ? null : Deconstruct(info, type);
        var called = top is not null && IsTheApps(top) ? top : null;
        var walk = new Walk(context, temporaries, $"$d{left.SpanStart}_", stepsAll: false);
        var (pattern, tuple) = walk.Composite(parts, info, type, called is not null);
        // A call among the levels runs as a step, after the parent's pattern has read every member it
        // names: a Deconstruct that changes what a later level reads would read it first. With one,
        // every nested level is a step, so each is read when C# reads it, left to right.
        if (walk.Steps.Count > 0)
        {
            walk = new Walk(context, temporaries, $"$d{left.SpanStart}_", stepsAll: true);
            (pattern, tuple) = walk.Composite(parts, info, type, called is not null);
        }
        return walk.Deconstructs || capture || walk.Converts
            ? new Lowered(pattern, called, walk.Steps, walk.Temporaries, walk.Assignments, tuple, walk.Captures,
                walk.Converts, walk.Declared)
            : null;
    }

    /// <summary>
    /// Whether an assignment's targets hold something C# evaluates before the value: an element's
    /// receiver and index, a member's receiver other than <c>this</c>. C# evaluates the targets, then
    /// the value, then writes, where destructuring evaluates the value first and each target as it
    /// writes it: <c>(xs[i++], xs[i++]) = new Point(i, i)</c> stored [2, 2] in .NET and [0, 0] here.
    /// </summary>
    internal static bool Captures(SyntaxNode left) => Parts(left) is { } parts && parts.Any(part =>
        part is ElementAccessExpressionSyntax
            or MemberAccessExpressionSyntax { Expression: not (ThisExpressionSyntax or BaseExpressionSyntax) }
        || Captures(part));

    /// <summary>The value a deconstruction reads, through the <c>Deconstruct</c> the app wrote: an
    /// instance method on the value, or an extension's static with the value first.</summary>
    internal static JsExpr Through(IMethodSymbol called, JsExpr value, ConversionContext context)
    {
        var name = called.Name.ToCamelCase();
        if ((called.ReducedFrom ?? called) is { IsExtensionMethod: true } extension)
        {
            extension.ContainingType.RegisterIntroduced(context);
            return JsExpr.Call(JsExpr.Member(JsExpr.Identifier(extension.ContainingType.Name), name), value);
        }
        if (called.ExtensionBlockHome() is { } home)
        {
            home.RegisterIntroduced(context);
            return JsExpr.Call(JsExpr.Member(JsExpr.Identifier(home.Name), name), value);
        }
        return JsExpr.Call(JsExpr.Member(value, name));
    }

    /// <summary>
    /// The declarators after the top one: one per step, each destructuring what its
    /// <c>Deconstruct</c> hands back (<c>, { celsius: c } = $d12_0.deconstruct()</c>), and then one per
    /// converted part, its name from its temporary in its own type (<c>, total = BigInt($d12_0)</c>),
    /// which a declaration appends to its own <c>let</c> or <c>const</c>.
    /// </summary>
    internal static string StepDeclarators(Lowered lowered, ConversionContext context) =>
        string.Concat(lowered.Steps.Select(step => $", {step.Pattern} = {StepValue(step, context)}"))
        + string.Concat(lowered.Declared.Select(declared => $", {declared.Name} = {declared.Value}"));

    /// <summary>What a step destructures: its temporary, through the <c>Deconstruct</c> it calls.</summary>
    internal static string StepValue(Step step, ConversionContext context) =>
        step.Called is { } called
            ? JsExprWriter.Write(Through(called, JsExpr.Identifier(step.Temporary), context))
            : step.Temporary;

    /// <summary>The parts a target splits into, or null for a target that is one part.</summary>
    private static IReadOnlyList<SyntaxNode>? Parts(SyntaxNode target) => target switch
    {
        TupleExpressionSyntax tuple => tuple.Arguments.Select(argument => (SyntaxNode)argument.Expression).ToList(),
        DeclarationExpressionSyntax { Designation: ParenthesizedVariableDesignationSyntax designation } =>
            designation.Variables.Cast<SyntaxNode>().ToList(),
        ParenthesizedVariableDesignationSyntax designation => designation.Variables.Cast<SyntaxNode>().ToList(),
        _ => null,
    };

    /// <summary>
    /// The <c>Deconstruct</c> a level calls: the bound tree's, which is none for a tuple's level.
    /// Where the model cannot be asked, the type's own, the first whose parameters are all outs.
    /// </summary>
    private static IMethodSymbol? Deconstruct(DeconstructionInfo? info, ITypeSymbol? type) =>
        info is { } bound
            ? bound.Method
            : type?.GetMembers("Deconstruct").OfType<IMethodSymbol>()
                .FirstOrDefault(method => method.Parameters.Length > 0
                    && method.Parameters.All(parameter => parameter.RefKind == RefKind.Out));

    /// <summary>A tuple, and a dictionary's pair, whose runtime value is iterable as its parts.</summary>
    private static bool Positional(ITypeSymbol? type) =>
        type is INamedTypeSymbol { IsTupleType: true }
        || type is INamedTypeSymbol { MetadataName: "KeyValuePair`2" } pair
            && pair.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic";

    /// <summary>A <c>Deconstruct</c> the app wrote, which its twin carries as a method. A record's
    /// own is the compiler's and has none, nor has a BCL type's.</summary>
    private static bool IsTheApps(IMethodSymbol deconstruct) =>
        !deconstruct.IsImplicitlyDeclared && deconstruct.Locations.Any(location => location.IsInSource);

    private static DeconstructionInfo? Nested(DeconstructionInfo? info, int index) =>
        info is { } bound && index < bound.Nested.Length ? bound.Nested[index] : null;

    private static ITypeSymbol? ElementType(ITypeSymbol? type, int index) => type switch
    {
        INamedTypeSymbol { IsTupleType: true } tuple when index < tuple.TupleElements.Length =>
            tuple.TupleElements[index].Type,
        INamedTypeSymbol { MetadataName: "KeyValuePair`2" } pair when index < pair.TypeArguments.Length =>
            pair.TypeArguments[index],
        _ => null,
    };

    /// <summary>One deconstruction's walk: the steps, and with temporaries, what each part binds.</summary>
    private sealed class Walk(ConversionContext context, bool temporaries, string prefix, bool stepsAll)
    {
        public bool Deconstructs { get; private set; }
        public bool Converts { get; private set; }
        public List<Step> Steps { get; } = [];
        public List<string> Temporaries { get; } = [];
        public List<(string Temporary, string Write)> Assignments { get; } = [];
        public List<(string Name, string Value)> Captures { get; } = [];
        public List<(string Name, string Value)> Declared { get; } = [];

        private string Fresh()
        {
            var name = prefix + Temporaries.Count;
            Temporaries.Add(name);
            return name;
        }

        private string Capture(string value)
        {
            var name = $"{prefix}c{Captures.Count}";
            Captures.Add((name, value));
            return name;
        }

        /// <summary>
        /// A target written through what it captures, with temporaries: an element as its captured
        /// receiver indexed by its captured index, a member read off its captured receiver. Null for
        /// any other target, and for an element whose indexer is a call (a dictionary's, which
        /// <see cref="Part"/> writes through its class).
        /// </summary>
        private string? Captured(ExpressionSyntax target)
        {
            if (!temporaries) return null;
            if (target is ElementAccessExpressionSyntax { ArgumentList.Arguments: [var argument] } element)
            {
                var receiver = context.Converter.ConvertExpression(element.Expression);
                var index = context.Converter.ConvertExpression(argument.Expression);
                if (context.Converter.ConvertExpression(element) != $"{receiver}[{index}]") return null;
                return $"{Capture(receiver)}[{Capture(index)}]";
            }
            if (target is MemberAccessExpressionSyntax { Expression: not (ThisExpressionSyntax or BaseExpressionSyntax) } member)
            {
                var receiver = context.Converter.ConvertExpression(member.Expression);
                var written = context.Converter.ConvertExpression(member);
                if (!written.StartsWith(receiver + ".", StringComparison.Ordinal)) return null;
                return Capture(receiver) + written[receiver.Length..];
            }
            return null;
        }

        /// <summary>
        /// One level's pattern and the tuple of its parts: by position, for a tuple, a pair and a
        /// value with no <c>Deconstruct</c>; otherwise by the names its <c>Deconstruct</c>'s outs
        /// carry, which a called one hands back under their own names and a read one finds on the
        /// members, in camelCase. A discard takes nothing: a hole by position, no entry by name,
        /// unless temporaries keep its value for the tuple.
        /// </summary>
        public (string Pattern, string Tuple) Composite(IReadOnlyList<SyntaxNode> parts, DeconstructionInfo? info,
            ITypeSymbol? type, bool called)
        {
            var deconstruct = Positional(type) ? null : Deconstruct(info, type);
            var tuple = new List<string>();
            if (deconstruct is null)
            {
                var elements = new List<string>();
                for (var i = 0; i < parts.Count; i++)
                {
                    var (bound, value) = Part(parts[i], Nested(info, i), ElementType(type, i));
                    elements.Add(bound ?? "");
                    tuple.Add(value);
                }
                return ($"[{string.Join(", ", elements)}]", $"[{string.Join(", ", tuple)}]");
            }

            Deconstructs = true;
            var outs = deconstruct.Parameters.Where(parameter => parameter.RefKind == RefKind.Out).ToList();
            var members = new List<string>();
            for (var i = 0; i < parts.Count && i < outs.Count; i++)
            {
                var (bound, value) = Part(parts[i], Nested(info, i), outs[i].Type);
                tuple.Add(value);
                if (bound is null) continue;
                var key = called ? outs[i].Name.ToJsIdentifier() : outs[i].Name.ToCamelCase();
                members.Add($"{key}: {bound}");
            }
            var pattern = members.Count == 0 ? "{}" : $"{{ {string.Join(", ", members)} }}";
            return (pattern, $"[{string.Join(", ", tuple)}]");
        }

        /// <summary>
        /// One part: what it binds in its parent's pattern (the name a declaration binds, the target an
        /// assignment writes, a temporary, or null for a discard), and its value in the tuple.
        /// <para>
        /// A part reaches its target as C# puts it there (#542): converted to the target's type, as
        /// the bound tree's conversion for it says, and written by what the target is. A dictionary's
        /// entry is written by its class, through <c>$eq.mapSet</c>, which no destructuring can do: a
        /// destructuring target is a place, and the entry's read is a call. A converted part is bound
        /// to a temporary as well, and a declaration declares its name after the destructuring, from
        /// the temporary, converted.
        /// </para>
        /// </summary>
        private (string? Bound, string Value) Part(SyntaxNode target, DeconstructionInfo? info, ITypeSymbol? type)
        {
            if (target is DeclarationExpressionSyntax declaration) target = declaration.Designation;
            if (Parts(target) is { } nested) return Level(nested, info, type);

            var discard = target is DiscardDesignationSyntax
                || target is IdentifierNameSyntax { Identifier.ValueText: "_" } underscore
                    && context.SemanticHelper.GetSymbol(underscore) is null or IDiscardSymbol;
            if (discard)
            {
                if (!temporaries) return (null, "undefined");
                var kept = Fresh();
                return (kept, kept);
            }

            var targetType = target switch
            {
                SingleVariableDesignationSyntax single => (context.SemanticHelper.GetDeclaredSymbol(single) as ILocalSymbol)?.Type,
                ExpressionSyntax assigned => context.SemanticHelper.GetType(assigned),
                _ => null,
            };
            string Converted(string value) => info is { Conversion: { } conversion }
                ? JsExprWriter.Write(ValueFlow.Apply(conversion, type, targetType, null, null, JsExpr.Identifier(value), context))
                : value;
            const string probe = "$part";
            var converts = Converted(probe) != probe;
            if (converts) Converts = true;
            // A temporary converted ONCE, in place, as the value its target is written: a conversion the
            // app wrote runs once, as C# runs it, and the target and the tuple hold the same value.
            string InPlace(string temporary) => converts ? $"({temporary} = {Converted(temporary)})" : temporary;

            if (target is SingleVariableDesignationSyntax variable)
            {
                var name = variable.Identifier.Text.ToJsIdentifier();
                if (temporaries)
                {
                    var assignedTo = Fresh();
                    Assignments.Add((assignedTo, $"{name} = {InPlace(assignedTo)}"));
                    return (assignedTo, assignedTo);
                }
                if (!converts) return (name, name);
                var held = Fresh();
                Declared.Add((name, Converted(held)));
                return (held, held);
            }

            if (target is not ExpressionSyntax written) return (null, "undefined");
            if (temporaries && DictionaryEntry.Of(written, context) is { } entry)
            {
                context.UsedHelpers.Add(Eq.Import);
                var receiver = Capture(context.Converter.ConvertExpression(entry.Expression));
                var key = Capture(context.Converter.ConvertExpression(entry.ArgumentList.Arguments[0].Expression));
                var entered = Fresh();
                Assignments.Add((entered, DictionaryEntry.Write(receiver, key, InPlace(entered))));
                return (entered, entered);
            }
            var place = Captured(written) ?? context.Converter.ConvertExpression(written);
            if (temporaries)
            {
                var placed = Fresh();
                Assignments.Add((placed, $"{place} = {InPlace(placed)}"));
                return (placed, placed);
            }
            return (place, place);
        }

        /// <summary>
        /// A part that splits again. A level whose <c>Deconstruct</c> the app wrote cannot be called
        /// from inside a pattern: its value is bound to a temporary there, and a <see cref="Step"/>
        /// destructures what its <c>Deconstruct</c> hands back, after the level that binds it.
        /// </summary>
        private (string? Bound, string Value) Level(IReadOnlyList<SyntaxNode> parts, DeconstructionInfo? info,
            ITypeSymbol? type)
        {
            var deconstruct = Positional(type) ? null : Deconstruct(info, type);
            var calls = deconstruct is not null && IsTheApps(deconstruct);
            if (!calls && !stepsAll)
            {
                var (pattern, tuple) = Composite(parts, info, type, called: false);
                return (pattern, tuple);
            }
            var temporary = Fresh();
            var at = Steps.Count;
            Steps.Add(null!);
            var (stepPattern, stepTuple) = Composite(parts, info, type, called: calls);
            Steps[at] = new Step(temporary, stepPattern, calls ? deconstruct : null);
            return (temporary, stepTuple);
        }
    }
}
