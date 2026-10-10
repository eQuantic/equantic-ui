using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Shared C#-pattern → JavaScript conversion, used by switch expressions, switch statements (rewritten to
/// an if/else chain) and <c>is</c> patterns. Produces two things for a pattern matched against a value
/// reachable at <c>access</c>:
/// <list type="bullet">
/// <item><see cref="BuildCondition"/> — the boolean test (constant/relational/type/property/positional/
///   list/<c>and</c>/<c>or</c>/<c>not</c>).</item>
/// <item><see cref="CollectBindings"/> — every variable the pattern binds (<c>var</c>/<c>Type t</c>),
///   anywhere in it, with the JS access path to read it.</item>
/// </list>
/// Positional subpatterns resolve their access from the matched type: a tuple is indexed (<c>[i]</c>),
/// while a record/struct uses its <c>Deconstruct</c> element names (<c>.x</c>, <c>.y</c>) — a record is a
/// plain object at runtime, so index access would read <c>undefined</c> — and a <c>Deconstruct</c> the app
/// wrote is called (<see cref="PositionalPart"/>). A property subpattern reads its member as a member
/// access reads it, a field in its slot (<see cref="Access"/>).
/// </summary>
public static class PatternConverter
{
    public static string BuildCondition(PatternSyntax pattern, string access, ConversionContext context,
        ITypeSymbol? accessType = null)
    {
        switch (pattern)
        {
            case ConstantPatternSyntax constant:
                // `is null` must be LOOSE: the transpiled world produces undefined wherever C#
                // produced null (Array.find for FirstOrDefault, absent properties, defaults), and
                // `=== null` lets undefined sail past the guard — found when a closed mega menu
                // crashed on `panel.Id` after `if (panel is null) return` didn't return.
                if (constant.Expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression })
                    return $"{access} == null";
                // `state switch { ClosedGate => … }` — a bare TYPE name PARSES as a constant
                // pattern but BINDS as a type pattern. Emitted as `=== ClosedGate` it compared the
                // value to the class object itself: always false, and the arm silently dead.
                if (constant.Expression is TypeSyntax bareType
                    && context.SemanticHelper.GetSymbol(constant.Expression) is INamedTypeSymbol)
                    return TypeCheck(bareType, access, context);
                return ConstantMatch(constant, constant.Expression, access, context);

            case RelationalPatternSyntax relational:
                return $"{access} {relational.OperatorToken.Text} {context.Converter.ConvertExpression(relational.Expression)}";

            case DeclarationPatternSyntax declaration:
                return TypeCheck(declaration.Type, access, context);

            // `o is int or long`, `int => …`, `case int:` — a type with nothing bound (C# 9). It had
            // no case here, so it fell to the default below and every such test was `false` (#482).
            case TypePatternSyntax typePattern:
                return TypeCheck(typePattern.Type, access, context);

            case RecursivePatternSyntax recursive:
                return BuildRecursive(recursive, access, context, accessType);

            case ListPatternSyntax list:
                return BuildList(list, access, context);

            case UnaryPatternSyntax unary when unary.OperatorToken.IsKind(SyntaxKind.NotKeyword):
                return $"!({BuildCondition(unary.Pattern, access, context, accessType)})";

            case BinaryPatternSyntax binary:
                var l = BuildCondition(binary.Left, access, context, accessType);
                var r = BuildCondition(binary.Right, access, context, accessType);
                var op = binary.OperatorToken.IsKind(SyntaxKind.OrKeyword) ? "||" : "&&";
                return $"({l} {op} {r})";

            case ParenthesizedPatternSyntax paren:
                return BuildCondition(paren.Pattern, access, context, accessType);

            case DiscardPatternSyntax:
            case VarPatternSyntax:
                return "true";

            case SlicePatternSyntax:
                return "true"; // length is enforced by the containing list pattern

            default:
                return "false";
        }
    }

    /// <summary>
    /// The test that <paramref name="access"/> matches a constant label or pattern, by
    /// <see cref="ConstantTest"/>'s one rule, with the constant the BOUND tree converted to the
    /// input's type: a decimal is the runtime's Decimal, an object <c>===</c> compares by identity, so
    /// <c>d is 1m</c>, <c>case decimal.One:</c> and a <c>1m =&gt;</c> arm never matched, and its exact
    /// value is written from the bound constant, so <c>d is 1</c> meets 1m.
    /// </summary>
    internal static string ConstantMatch(SyntaxNode label, ExpressionSyntax constant, string access,
        ConversionContext context)
    {
        var value = context.SemanticHelper.GetOperation(label) switch
        {
            IConstantPatternOperation pattern => pattern.Value,
            ISingleValueCaseClauseOperation single => single.Value,
            IPatternCaseClauseOperation { Pattern: IConstantPatternOperation pattern } => pattern.Value,
            _ => null,
        };
        var text = DecimalConstant(label, context) is { } exact
            ? ConstantLiteral.Write(exact, null, context)!
            : context.Converter.ConvertExpression(constant);
        return ConstantTest(access, text, value, context);
    }

    /// <summary>The decimal a constant pattern or a case label compares with, read from the bound
    /// tree after its conversion to the input's type, or null when it compares something else.</summary>
    internal static decimal? DecimalConstant(SyntaxNode label, ConversionContext context) =>
        context.SemanticHelper.GetOperation(label) switch
        {
            IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: decimal value } } => value,
            ISingleValueCaseClauseOperation { Value.ConstantValue: { HasValue: true, Value: decimal value } } => value,
            IPatternCaseClauseOperation
            {
                Pattern: IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: decimal value } },
            } => value,
            _ => null,
        };

    public static void CollectBindings(PatternSyntax pattern, string access, ConversionContext context,
        List<(string Name, string Access)> bindings, ITypeSymbol? accessType = null)
    {
        switch (pattern)
        {
            // Each binding under the name every reference reads it by (ToJsIdentifier): as source
            // text, `is int @class` bound `@class` and `is int package` a reserved word.
            case VarPatternSyntax { Designation: SingleVariableDesignationSyntax v }:
                bindings.Add((v.Identifier.Text.ToJsIdentifier(), access));
                break;

            case DeclarationPatternSyntax { Designation: SingleVariableDesignationSyntax d }:
                bindings.Add((d.Identifier.Text.ToJsIdentifier(), access));
                break;

            case RecursivePatternSyntax recursive:
                if (recursive.Designation is SingleVariableDesignationSyntax r)
                    bindings.Add((r.Identifier.Text.ToJsIdentifier(), access));
                if (recursive.PositionalPatternClause is { } positional)
                {
                    // Read after the test, which called the app's Deconstruct, if the pattern has one.
                    var part = PositionalPart(recursive, access, accessType, context, out _);
                    for (int i = 0; i < positional.Subpatterns.Count; i++)
                        CollectBindings(positional.Subpatterns[i].Pattern, part(i), context, bindings);
                }
                if (recursive.PropertyPatternClause != null)
                    foreach (var sp in recursive.PropertyPatternClause.Subpatterns)
                        if (MemberPath(sp) is { } path)
                        {
                            var receivers = ReceiverTypes(sp, PatternType(recursive, context) ?? accessType, context);
                            CollectBindings(sp.Pattern,
                                path.Select((name, i) => (name, i))
                                    .Aggregate(access, (at, step) => Access(at, step.name, receivers[step.i], context)),
                                context, bindings);
                        }
                break;

            case ListPatternSyntax list:
                CollectListBindings(list, access, context, bindings);
                break;

            case ParenthesizedPatternSyntax paren:
                CollectBindings(paren.Pattern, access, context, bindings, accessType);
                break;

            case BinaryPatternSyntax binary when binary.OperatorToken.IsKind(SyntaxKind.AndKeyword):
                CollectBindings(binary.Left, access, context, bindings, accessType);
                CollectBindings(binary.Right, access, context, bindings, accessType);
                break;
        }
    }

    private static string BuildRecursive(RecursivePatternSyntax recursive, string access,
        ConversionContext context, ITypeSymbol? accessType)
    {
        var checks = new List<string>();
        // `OpenGate(var percent)` — the pattern NAMES a type, and the test must be that type's,
        // not a bare null-check: with two positional arms over a hierarchy, `!= null` made the
        // first arm win every time. TypeCheck degrades to the null-check itself exactly where no
        // stronger test exists, so this is never weaker than before.
        if (recursive.Type is { } patternType)
            checks.Add(TypeCheck(patternType, access, context));
        else if (recursive.PropertyPatternClause != null || recursive.PositionalPatternClause != null)
            checks.Add($"{access} != null");

        if (recursive.PositionalPatternClause is { } positional)
        {
            var part = PositionalPart(recursive, access, accessType, context, out var called);
            if (called is not null) checks.Add(called);
            for (int i = 0; i < positional.Subpatterns.Count; i++)
            {
                var sub = BuildCondition(positional.Subpatterns[i].Pattern, part(i), context);
                if (sub != "true") checks.Add(sub);
            }
        }
        if (recursive.PropertyPatternClause != null)
            foreach (var sp in recursive.PropertyPatternClause.Subpatterns)
            {
                if (MemberPath(sp) is not { } path) continue;
                var receivers = ReceiverTypes(sp, PatternType(recursive, context) ?? accessType, context);
                // `{ A.B: p }` is `{ A: { B: p } }`: every member before the last must be there, or
                // the pattern answers false, as C#'s does, rather than reading through a null.
                var at = access;
                for (var i = 0; i < path.Count - 1; i++)
                {
                    at = Access(at, path[i], receivers[i], context);
                    checks.Add($"{at} != null");
                }
                var sub = BuildCondition(sp.Pattern, Access(at, path[^1], receivers[^1], context), context);
                if (sub != "true") checks.Add(sub);
            }

        return checks.Count > 0 ? "(" + string.Join(" && ", checks) + ")" : $"{access} != null";
    }

    private static string BuildList(ListPatternSyntax list, string access, ConversionContext context)
    {
        var (before, after, sliceIndex) = SliceShape(list);
        var checks = new List<string>
        {
            $"Array.isArray({access})",
            sliceIndex < 0 ? $"{access}.length === {list.Patterns.Count}" : $"{access}.length >= {before + after}",
        };
        for (int i = 0; i < before; i++)
        {
            var sub = BuildCondition(list.Patterns[i], $"{access}[{i}]", context);
            if (sub != "true") checks.Add(sub);
        }
        for (int j = 0; j < after; j++)
        {
            var sub = BuildCondition(list.Patterns[sliceIndex + 1 + j], $"{access}[{access}.length - {after - j}]", context);
            if (sub != "true") checks.Add(sub);
        }
        return "(" + string.Join(" && ", checks) + ")";
    }

    private static void CollectListBindings(ListPatternSyntax list, string access,
        ConversionContext context, List<(string Name, string Access)> bindings)
    {
        // The list ITSELF, when the pattern names it (`is [1, _] pair`): declared by the scanner,
        // so it must be assigned here, or it reads undefined where C# reads the list.
        if (list.Designation is SingleVariableDesignationSyntax whole)
            bindings.Add((whole.Identifier.Text.ToJsIdentifier(), access));
        var (before, after, sliceIndex) = SliceShape(list);
        for (int i = 0; i < before; i++)
            CollectBindings(list.Patterns[i], $"{access}[{i}]", context, bindings);
        if (sliceIndex >= 0 && list.Patterns[sliceIndex] is SlicePatternSyntax { Pattern: { } slicePat })
            CollectBindings(slicePat, $"{access}.slice({before}, {access}.length - {after})", context, bindings);
        for (int j = 0; j < after; j++)
            CollectBindings(list.Patterns[sliceIndex + 1 + j], $"{access}[{access}.length - {after - j}]", context, bindings);
    }

    private static (int Before, int After, int SliceIndex) SliceShape(ListPatternSyntax list)
    {
        for (int i = 0; i < list.Patterns.Count; i++)
            if (list.Patterns[i] is SlicePatternSyntax)
                return (i, list.Patterns.Count - i - 1, i);
        return (list.Patterns.Count, 0, -1);
    }

    /// <summary>
    /// How a positional pattern reads its part <c>i</c>, and, for a pattern that goes through a
    /// <c>Deconstruct</c> the app wrote, the test that calls it. Such a <c>Deconstruct</c> is CALLED, as
    /// a deconstruction calls it (<see cref="Strategies.DeconstructionPattern"/>), the one the bound
    /// tree names: its outs come back as the object every method with outs returns, each under its
    /// parameter's name. Read off the value by those names instead, one that computes a part read
    /// nothing, and one whose out is named after a field that moved a case apart from a property
    /// (#396) read the property: <c>new Point(1, 2) is (1, 2)</c> was false where .NET says true. What
    /// it hands back is held where the pattern-matching operation keeps it (<see cref="MatchParts"/>),
    /// assigned by the first test that reaches it, after the type test, and read by every other test
    /// and binding of the operation, so the operation calls it once for the value, as .NET does. A
    /// record's own <c>Deconstruct</c> and a BCL type's read the members their outs name, and a tuple
    /// reads by index.
    /// </summary>
    private static Func<int, string> PositionalPart(RecursivePatternSyntax recursive, string access,
        ITypeSymbol? accessType, ConversionContext context, out string? called)
    {
        called = null;
        if (context.SemanticHelper.GetOperation(recursive)
                is not IRecursivePatternOperation { DeconstructSymbol: IMethodSymbol deconstruct }
            || !Strategies.DeconstructionPattern.IsTheApps(deconstruct))
        {
            // The pattern's OWN type decides the deconstruction names — the governing
            // expression's static type may be the base (`GateState`), which deconstructs
            // nothing and left positional access as `[i]` on a plain object: undefined.
            var names = PositionalNames(PatternType(recursive, context) ?? accessType);
            return i => PositionalAccess(access, i, names);
        }
        var outs = deconstruct.Parameters.Where(parameter => parameter.RefKind == RefKind.Out).ToList();
        var parts = JsExprWriter.Write(
            Strategies.DeconstructionPattern.Through(deconstruct, JsExpr.Opaque(access), context));
        var held = context.MatchParts.Held(access, deconstruct);
        called = $"({held} ?? ({held} = {parts}))";
        return i => $"{held}.{outs[i].Name.ToJsIdentifier()}";
    }

    /// <summary>Deconstruct element names of a non-tuple type (record/struct) for positional access, or
    /// <c>null</c> to fall back to index access (tuples, or unknown types).</summary>
    private static IReadOnlyList<string>? PositionalNames(ITypeSymbol? type)
        => type is { IsTupleType: false } ? type.DeconstructElementNames() : null;

    /// <summary>The type a recursive pattern NAMES (<c>OpenGate(var p)</c> → OpenGate), resolved
    /// through the model; null for type-less patterns or when the model cannot answer.</summary>
    private static ITypeSymbol? PatternType(RecursivePatternSyntax recursive, ConversionContext context)
        => recursive.Type is { } t ? context.SemanticHelper.GetSymbol(t) as ITypeSymbol : null;

    private static string PositionalAccess(string access, int i, IReadOnlyList<string>? names)
        => names != null && i < names.Count ? $"{access}.{names[i]}" : $"{access}[{i}]";

    /// <summary>
    /// The one type test. Public because the binary <c>x is Type</c> form is a separate strategy and
    /// used to carry its OWN copy of this rule — a copy that never learned about vocabulary classes,
    /// which is exactly how one of the two answers went stale without anyone noticing. One rule, one
    /// place, both callers.
    /// </summary>
    public static string TypeCheck(TypeSyntax typeSyntax, string access, ConversionContext context)
    {
        // A type the platform represents by a value of its own, read off the SYMBOL: the spelling
        // missed `Int32` and `System.Int64`, and asked a long, which is a BigInt here, whether it was
        // a number, so `o is long` was false for every long.
        if (context.SemanticHelper.GetSymbol(typeSyntax) is INamedTypeSymbol known && ScalarCheck(known, access, context) is { } scalar)
            return scalar;
        switch (typeSyntax.ToString())
        {
            case "string": return $"typeof {access} === 'string'";
            case "int" or "double" or "float" or "decimal" or "number":
                return $"typeof {access} === 'number'";
            case "long": return $"typeof {access} === 'bigint'";
            case "bool" or "boolean": return $"typeof {access} === 'boolean'";
        }

        // A class that lowers to a REAL JS class supports `instanceof`. That is the whole VOCABULARY
        // — every `VisualNode` is an `export class` in the runtime, components included (UiComponent
        // derives from VisualNode). An exception is an Error carrying its .NET types, which its own
        // test reads. Everything else keeps the null-check: enums lower to string literals, value
        // types to plain config objects.
        //
        // It used to be components ONLY, and the fallback is where that hurt: `leading switch { Icon
        // icon => …, Avatar avatar => … }` emitted `_s != null` for the Icon arm, so the FIRST arm
        // matched everything and the Avatar arm was dead code. A wrong answer with no diagnostic —
        // caught by tsc complaining about `.size` on a VisualNode, which is luck, not a net.
        // A CLASS or a STRUCT: a record struct is a real class on the other side too (a boxed one is
        // exactly what a type pattern over `object` meets), so the same `instanceof` is its test.
        if (context.SemanticHelper.GetSymbol(typeSyntax) is INamedTypeSymbol
            {
                TypeKind: TypeKind.Class or TypeKind.Struct
            } named)
        {
            // `e is ArgumentException`, a switch arm over exceptions, `e as …` and a typed catch, the
            // same test: was `!= null`, so the first arm took every exception (#474).
            if (ExceptionTypes.Is(named)) return ExceptionTypes.Test(access, named, context);

            if (LowersToAJsClass(named))
            {
                // The name has to reach the import list, or the module references a free variable.
                context.UsedRuntimeTypes.Add(named.Name);
                return $"{access} instanceof {named.Name}";
            }

            // The APP's own classes, records and structs are emitted as real JS classes too, so the
            // honest test is the same `instanceof` — without it, `state switch { ClosedGate => …,
            // OpenGate o => … }` emitted `!= null` for every arm and the FIRST one always won.
            // Known caveat: a value that crossed the SERVER boundary as JSON is a plain object and
            // fails instanceof — pattern-match client-constructed values, not raw prefetch payloads.
            if (IsEmittedAppType(named))
            {
                context.UsedAppTypes.Add(named.Name);
                return $"{access} instanceof {named.Name}";
            }
        }

        return $"{access} != null";
    }

    /// <summary>
    /// The test that a value IS a constant, as a constant pattern asks it, by the constant's bound
    /// value. A null is any absence, as <c>is null</c> is. A decimal is an object on this side and
    /// compares by value, and so does a NaN, which a pattern matches where <c>===</c> never does.
    /// Anything else is <c>===</c> its literal. One rule for <c>x is 5</c>, <c>case Limits.Max:</c>
    /// and the binary <c>x is Limits.Max</c>, which parses as a type test and binds as a constant
    /// (#451).
    /// </summary>
    internal static string ConstantTest(string access, string constant, IOperation? value, ConversionContext context)
    {
        if (value?.ConstantValue is { HasValue: true } known)
        {
            if (known.Value is null) return $"{access} == null";
            if (known.Value is decimal or double.NaN or float.NaN)
            {
                context.UsedHelpers.Add(Eq.Import);
                return $"{Eq.Equals}({access}, {constant})";
            }
        }
        return $"{access} === {constant}";
    }

    /// <summary>
    /// The test for a type the browser holds as a value of its own: a string (a char is one too), a
    /// bool, a long as a BigInt, an integer as a whole number, a real as any number, and a decimal and
    /// the dates as the runtime's classes. Null for every other type. A boxed double holding a whole
    /// number still tests as an int, since both are one JavaScript number.
    /// </summary>
    private static string? ScalarCheck(INamedTypeSymbol type, string access, ConversionContext context)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_String:
                return $"typeof {access} === 'string'";
            // A char is one UTF-16 code unit, a string of one here: any longer string is not one.
            case SpecialType.System_Char:
                return $"typeof {access} === 'string' && {access}.length === 1";
            case SpecialType.System_Boolean:
                return $"typeof {access} === 'boolean'";
            case SpecialType.System_Int64 or SpecialType.System_UInt64:
                return $"typeof {access} === 'bigint'";
            case SpecialType.System_Int32 or SpecialType.System_Int16 or SpecialType.System_SByte
                or SpecialType.System_Byte or SpecialType.System_UInt16 or SpecialType.System_UInt32:
                return $"Number.isInteger({access})";
            case SpecialType.System_Double or SpecialType.System_Single:
                return $"typeof {access} === 'number'";
            case SpecialType.System_Decimal or SpecialType.System_DateTime:
                context.UsedRuntimeTypes.Add(type.Name);
                return $"{access} instanceof {type.Name}";
        }
        if (type.ContainingNamespace?.ToDisplayString() == "System"
            && type.Name is "TimeSpan" or "DateOnly" or "TimeOnly" or "DateTimeOffset")
        {
            context.UsedRuntimeTypes.Add(type.Name);
            return $"{access} instanceof {type.Name}";
        }
        return null;
    }

    /// <summary>
    /// Whether this is one of the APP's own types, whose twin the compiler emits as a class. A class
    /// declared in source always was. A STRUCT is when it is the app's and its twin is emitted
    /// (<see cref="RecordTypeEmitter.CanEmit"/>, the parser's own rule): a vocabulary struct declared
    /// in source — a probe, the library's own build — still has a hand-written twin, some of them
    /// plain objects, and <c>instanceof</c> against one is a TypeError rather than an answer.
    /// </summary>
    private static bool IsEmittedAppType(INamedTypeSymbol type)
    {
        if (!type.Locations.Any(location => location.IsInSource)) return false;
        if (type.TypeKind == TypeKind.Class) return true;
        return !Services.RuntimeProvidedTypeScanner.IsRuntimeProvidedNamespace(
                   type.ContainingNamespace?.ToDisplayString() ?? "")
               && RecordTypeEmitter.EmitsTwin(type);
    }

    /// <summary>
    /// Whether this type exists as a real class on the other side — every vocabulary node does, and
    /// so does every class and record of the code engine, the component library and the charts,
    /// which the compiler transpiles whole. That is what makes <c>instanceof</c> the honest test for
    /// them; anywhere else the vocabulary's twins may be written by hand, and a type test there stays
    /// the presence check below.
    /// </summary>
    private static bool LowersToAJsClass(INamedTypeSymbol type)
    {
        if (Services.RuntimeProvidedTypeScanner.IsTranspiledNamespace(
                type.ContainingNamespace?.ToDisplayString() ?? "")
            || type.TwinIsTranspiled())
            return true;
        for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
        {
            if (baseType.Name is "VisualNode" or "UiComponent"
                && baseType.ContainingNamespace?.ToDisplayString() == "eQuantic.UI.Primitives")
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The members a subpattern names, outermost first: one for <c>{ X: … }</c>, the whole path for
    /// the extended <c>{ A.B.C: … }</c>, each as the name the model binds to its member. The path was
    /// lower-cased as ONE name, so <c>{ Changes.Count: > 0 }</c> read <c>changes.Count</c>, undefined,
    /// and was quietly always false.
    /// </summary>
    private static List<SimpleNameSyntax>? MemberPath(SubpatternSyntax sp)
    {
        if (sp.NameColon is { } nameColon) return [nameColon.Name];
        if (sp.ExpressionColon is not { } expressionColon) return null;
        var path = new List<SimpleNameSyntax>();
        var at = expressionColon.Expression;
        while (at is MemberAccessExpressionSyntax member)
        {
            path.Insert(0, member.Name);
            at = member.Expression;
        }
        // C# accepts nothing else before the colon (CS8918): a name, then members of it.
        if (at is not IdentifierNameSyntax first) return null;
        path.Insert(0, first);
        return path;
    }

    /// <summary>
    /// The type each member of a subpattern's path is read from, aligned with <see cref="MemberPath"/>:
    /// the pattern's own type for the first, and the member before it for each one after, so
    /// <c>{ Map.Count: > 0 }</c> reads a dictionary's <c>size</c> as <c>{ Count: > 0 }</c> does. A type
    /// the model cannot give is null, and the member takes its plain camelCase name.
    /// </summary>
    private static List<ITypeSymbol?> ReceiverTypes(SubpatternSyntax sp, ITypeSymbol? first, ConversionContext context)
    {
        var receivers = new List<ITypeSymbol?> { first };
        if (sp.ExpressionColon is not { } expressionColon) return receivers;
        var prefixes = new List<ExpressionSyntax>();
        for (var at = expressionColon.Expression; at is MemberAccessExpressionSyntax member; at = member.Expression)
            prefixes.Insert(0, member.Expression);
        foreach (var prefix in prefixes) receivers.Add(context.SemanticHelper.GetType(prefix));
        return receivers;
    }

    /// <summary>
    /// A property pattern names a MEMBER, read as a member access reads it. A FIELD, asked of the
    /// model, is read in its slot (<see cref="FieldSlotExtensions.TwinSlot"/>), the one rule every read
    /// of a field takes, ahead of the collections' table below: named by its text,
    /// <c>this is { value: 1 }</c> beside a property <c>Value</c> read the property, and a field called
    /// <c>Count</c> the method <c>count()</c> a case apart from it, each answering the opposite of .NET
    /// (#396). Any other member's JS name is not always its camelCase: a collection's <c>Count</c> is
    /// <c>length</c>, a string's <c>Length</c> likewise. Lower-casing blindly emitted
    /// <c>actions.count</c> on a JS array — <c>undefined</c>, so <c>Actions is { Count: > 3 }</c> was
    /// quietly always false, with nothing to see at build time. A <c>Count</c> reads as a member access
    /// reads it (<see cref="Strategies.CountSpelling"/>, one table for both): <c>{ Roles.Count: > 0 }</c>
    /// over a set read <c>length</c>, and was false in the browser where the server had drawn the other
    /// branch (#516).
    /// </summary>
    private static string Access(string at, SimpleNameSyntax name, ITypeSymbol? receiver, ConversionContext context) =>
        (context.SemanticHelper.GetSymbol(name), name.Identifier.ValueText) switch
        {
            (IFieldSymbol field, _) => $"{at}.{field.TwinSlot()}",
            (_, "Count") => JsExprWriter.Write(Strategies.CountSpelling.Read(JsExpr.Opaque(at), receiver, context)),
            (_, "Length") => $"{at}.length",
            (_, var member) => $"{at}.{TwinName.Of(member)}",
        };
}
