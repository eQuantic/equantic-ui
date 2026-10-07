using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Value semantics for a mutable struct and a value tuple (#560). C# copies such a value wherever it
/// flows (an assignment, an argument, a return, a boxing), and JavaScript hands the same object over,
/// so a change made through one name showed through every other: after <c>var u = t;</c>,
/// <c>t.Item1 = 9</c> made <c>u.Item1</c> 9 as well.
/// <para>
/// The twin copies ON WRITE. Before a member of such a value is written (an assignment, a compound
/// assignment, an increment), or a method that writes the value's own state is called on it, the
/// variable, parameter, field or array element that holds it is given a copy of it, each value along
/// the path first, outermost to innermost, so every other name keeps the value it had and a value
/// nobody writes is never copied. A method of the struct writes its own <c>this</c> in place, which
/// the call has already copied. The one copy taken on READ is that <c>this</c>, when it flows into a
/// variable, an argument, a return or a boxing, since nothing can replace it. A mutating call on a
/// value that is no storage (a property's or a call's result) runs on a copy, as C#'s runs on the
/// temporary.
/// </para>
/// <para>
/// A write through a readonly field outside its constructor, a <c>foreach</c> or <c>using</c>
/// variable, an <c>in</c> or <c>ref</c> parameter, or a path whose evaluation has effects
/// (<c>items[i++].X = 1</c>) is done in place, as before.
/// </para>
/// </summary>
internal static class ValueCopies
{
    /// <summary>The translation of <paramref name="node"/>, with the copies a write of a value needs.</summary>
    public static JsExpr Settle(ExpressionSyntax node, JsExpr translated, ConversionContext context)
    {
        var operation = context.SemanticHelper.GetOperation(node);
        if (operation is null) return translated;

        if (operation is IInstanceReferenceOperation self && IsMutableValue(self.Type) && FlowsAway(self))
            return Copy(self.Type!, translated);

        if (Written(operation, context) is not { } owner || !IsMutableValue(owner.Type)) return translated;

        // A mutating call on a temporary runs on a copy of it, as C# runs it on the temporary.
        if (operation is IInvocationOperation && owner is IPropertyReferenceOperation or IInvocationOperation
            && translated is JsCall { Target: JsMember callee } call)
            return call with { Target = callee with { Target = Copy(owner.Type!, callee.Target) } };

        var copies = new List<JsExpr>();
        if (!CopiesBefore(owner, node, copies, context) || copies.Count == 0) return translated;
        return copies.Append(translated).Aggregate((before, next) => JsExpr.Binary(before, ",", next));
    }

    /// <summary>
    /// Whether the twin holds values of <paramref name="type"/> as objects a write changes in place: a
    /// value tuple, and a struct of this compilation that is not readonly. A readonly struct, an enum, a
    /// primitive, a value the browser holds as data and a struct from a referenced assembly never are.
    /// </summary>
    internal static bool IsMutableValue(ITypeSymbol? type) => type switch
    {
        INamedTypeSymbol { IsTupleType: true } => true,
        INamedTypeSymbol { TypeKind: TypeKind.Struct, IsReadOnly: false, SpecialType: SpecialType.None } named
            => named.Locations.Any(location => location.IsInSource) && !named.TwinIsData(),
        _ => false,
    };

    /// <summary>A copy of a value of <paramref name="type"/>: a tuple's array spread into a new one, a
    /// struct's twin through its <c>$clone</c>. Both are shallow: a value inside is copied when it is
    /// written in turn.</summary>
    private static JsExpr Copy(ITypeSymbol type, JsExpr value) =>
        type.IsTupleType
            ? JsExpr.Array([JsExpr.Spread(value)])
            : JsExpr.Call(JsExpr.Member(value, "$clone"));

    /// <summary>The value whose own state <paramref name="operation"/> writes, or null.</summary>
    private static IOperation? Written(IOperation operation, ConversionContext context) => operation switch
    {
        ISimpleAssignmentOperation assignment => Owner(assignment.Target),
        ICompoundAssignmentOperation compound => Owner(compound.Target),
        ICoalesceAssignmentOperation coalesce => Owner(coalesce.Target),
        IIncrementOrDecrementOperation step => Owner(step.Target),
        IInvocationOperation { Instance: { } receiver, TargetMethod: var method } when Mutates(method, context) => receiver,
        _ => null,
    };

    /// <summary>The value a target writes a member of.</summary>
    private static IOperation? Owner(IOperation target) => target switch
    {
        IFieldReferenceOperation { Instance: { } owner } => owner,
        IPropertyReferenceOperation { Instance: { } owner, Property.IsIndexer: false } => owner,
        _ => null,
    };

    /// <summary>
    /// The copies that make <paramref name="storage"/> a value of its own before it is written, the
    /// values that hold it first. False where the write is done in place: storage that cannot be
    /// replaced, and a path whose evaluation has effects.
    /// </summary>
    private static bool CopiesBefore(IOperation storage, SyntaxNode at, List<JsExpr> copies, ConversionContext context)
    {
        switch (storage)
        {
            case IInstanceReferenceOperation:
                return true;
            case ILocalReferenceOperation { Local: { RefKind: RefKind.None, IsForEach: false, IsUsing: false } }:
            case IParameterReferenceOperation { Parameter.RefKind: RefKind.None }:
                break;
            case IFieldReferenceOperation field when field.Field.IsStatic ? !field.Field.IsReadOnly : Writable(field, at, context):
                if (field.Instance is { } instance)
                {
                    if (IsMutableValue(instance.Type))
                    {
                        if (!CopiesBefore(instance, at, copies, context)) return false;
                    }
                    else if (!Pure(instance)) return false;
                }
                break;
            case IArrayElementReferenceOperation element when Pure(element.ArrayReference) && element.Indices.All(Pure):
                break;
            default:
                return false;
        }
        if (storage.Syntax is not ExpressionSyntax syntax) return false;
        var path = context.Converter.ConvertIr(syntax);
        copies.Add(JsExpr.Binary(path, "=", Copy(storage.Type!, path)));
        return true;
    }

    /// <summary>Whether a field can be given a copy here: one that is not readonly, or a readonly one
    /// inside a constructor of its own type.</summary>
    private static bool Writable(IFieldReferenceOperation field, SyntaxNode at, ConversionContext context)
    {
        if (!field.Field.IsReadOnly) return true;
        var constructor = at.Ancestors().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
        return constructor?.Parent is TypeDeclarationSyntax owner
            && context.SemanticModel is { } model && model.SyntaxTree == owner.SyntaxTree
            && model.GetDeclaredSymbol(owner) is INamedTypeSymbol declared
            && SymbolEqualityComparer.Default.Equals(declared, field.Field.ContainingType);
    }

    /// <summary>Whether evaluating <paramref name="operation"/> twice reads the same storage and does
    /// nothing else: a local, a parameter, <c>this</c>, a constant, and a field or an element of them.</summary>
    private static bool Pure(IOperation operation) => operation switch
    {
        ILocalReferenceOperation or IParameterReferenceOperation or IInstanceReferenceOperation or ILiteralOperation => true,
        IFieldReferenceOperation field => field.Instance is null || Pure(field.Instance),
        IArrayElementReferenceOperation element => Pure(element.ArrayReference) && element.Indices.All(Pure),
        IConversionOperation { IsImplicit: true } conversion => Pure(conversion.Operand),
        _ => operation.ConstantValue.HasValue,
    };

    /// <summary>Whether <c>this</c> leaves the member for somewhere that keeps it: a variable, an
    /// argument, a return, a tuple, an array or a boxing, each of which C# copies it into.</summary>
    private static bool FlowsAway(IOperation value)
    {
        var parent = value.Parent;
        if (parent is IConversionOperation conversion && ReferenceEquals(conversion.Operand, value))
            return true;
        return parent switch
        {
            IVariableInitializerOperation => true,
            ISimpleAssignmentOperation assignment => ReferenceEquals(assignment.Value, value),
            IArgumentOperation { Parameter.RefKind: RefKind.None } => true,
            IReturnOperation or ITupleOperation or IArrayInitializerOperation => true,
            _ => false,
        };
    }

    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<IMethodSymbol, bool>> Mutating = new();

    /// <summary>
    /// Whether a method writes the state of the value it is called on: an instance method of a mutable
    /// value that is not readonly and whose body assigns a member of <c>this</c> (or of a value
    /// <c>this</c> holds), assigns <c>this</c>, or calls one that does. A method the compilation does not
    /// declare writes nothing the twin can see.
    /// </summary>
    private static bool Mutates(IMethodSymbol method, ConversionContext context)
    {
        if (method.IsStatic || method.IsReadOnly || !IsMutableValue(method.ContainingType)) return false;
        if (context.SemanticModel?.Compilation is not { } compilation) return false;
        var known = Mutating.GetValue(compilation, _ => new ConcurrentDictionary<IMethodSymbol, bool>(SymbolEqualityComparer.Default));
        return known.TryGetValue(method, out var answer) ? answer : known[method] = Writes(method, compilation, []);
    }

    private static bool Writes(IMethodSymbol method, Compilation compilation, HashSet<IMethodSymbol> seen)
    {
        if (!seen.Add(method)) return false;
        foreach (var reference in method.DeclaringSyntaxReferences)
        {
            var syntax = reference.GetSyntax();
            if (compilation.GetSemanticModel(syntax.SyntaxTree).GetOperation(syntax) is not { } body) continue;
            foreach (var operation in body.Descendants())
            {
                var target = operation switch
                {
                    ISimpleAssignmentOperation assignment => assignment.Target,
                    ICompoundAssignmentOperation compound => compound.Target,
                    ICoalesceAssignmentOperation coalesce => coalesce.Target,
                    IIncrementOrDecrementOperation step => step.Target,
                    _ => null,
                };
                if (target is IInstanceReferenceOperation || (target is not null && OfThis(Owner(target)))) return true;
                if (operation is IInvocationOperation { Instance: { } receiver, TargetMethod: var called }
                    && OfThis(receiver) && !called.IsStatic && !called.IsReadOnly
                    && IsMutableValue(called.ContainingType) && Writes(called, compilation, seen))
                    return true;
            }
        }
        return false;
    }

    /// <summary>Whether a value is <c>this</c>, or a value <c>this</c> holds in a field.</summary>
    private static bool OfThis(IOperation? value) => value switch
    {
        IInstanceReferenceOperation => true,
        IFieldReferenceOperation { Instance: { } owner } field => IsMutableValue(field.Type) && OfThis(owner),
        _ => false,
    };
}
