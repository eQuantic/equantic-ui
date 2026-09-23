using System.Linq.Expressions;
using System.Reflection;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What it takes to CALL a factory and the constructor it mirrors: plausible arguments for a
/// mirrored prefix, the value an omitted argument really carries, and a value distinguishable from
/// it. <see cref="UiFactoryConformanceTests"/> asks the questions; this answers "with what".
///
/// <para>
/// It lives in a type of its own because it is machinery rather than a rule — the tests beside it
/// read as the contract they state, and this reads as reflection. Every entry point is TOTAL: a
/// type it cannot build or vary is reported by name and fails the test that asked, never skipped.
/// </para>
/// </summary>
internal static class FactoryArguments
{
    /// <summary>
    /// Plausible arguments for a mirrored prefix, so the factory and its constructor can both be
    /// CALLED. Total on purpose: a type it cannot build is reported by name and fails the test,
    /// never skipped — a check that steps over what it cannot construct answers "green" for exactly
    /// the factories nobody measured.
    /// </summary>
    internal static bool For(ParameterInfo[] parameters, out object?[] arguments, out string why) =>
        TryArguments(parameters, 0, out arguments, out why);

    private static bool TryArguments(ParameterInfo[] parameters, int depth,
        out object?[] arguments, out string why)
    {
        arguments = new object?[parameters.Length];
        why = "";
        var nullability = new NullabilityInfoContext();
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (parameter.HasDefaultValue)
            {
                arguments[i] = Omitted(parameter);
                continue;
            }

            // A parameter the signature says may be null takes null: it is a legal argument, it is
            // the same on both sides, and building one would measure nothing the comparison reads.
            if (!parameter.ParameterType.IsValueType
                && nullability.Create(parameter).WriteState == NullabilityState.Nullable)
                continue;

            if (!TryMake(parameter.ParameterType, depth, out var made))
            {
                why = $"nothing plausible to pass for `{parameter.Name}` "
                    + $"({parameter.ParameterType.Name})";
                return false;
            }
            arguments[i] = made;
        }
        return true;
    }

    /// <summary>
    /// What an omitted argument IS at the call site. <c>= default</c> on a struct carries no
    /// metadata constant, so <see cref="ParameterInfo.DefaultValue"/> answers <c>null</c> where C#
    /// passes an all-zero value — the one case where the metadata and the language disagree.
    /// </summary>
    internal static object? Omitted(ParameterInfo parameter) =>
        parameter.DefaultValue is null && parameter.ParameterType.IsValueType
            ? Activator.CreateInstance(parameter.ParameterType)
            : parameter.DefaultValue;

    internal static readonly Type[] CollectionInterfaces =
    [
        typeof(IReadOnlyList<>), typeof(IReadOnlyCollection<>), typeof(IEnumerable<>),
        typeof(IList<>), typeof(ICollection<>),
    ];

    private static bool TryMake(Type type, int depth, out object? value)
    {
        value = null;
        // A node needing five nested builds is not a plausible argument, it is a runaway.
        if (depth > 4) return false;
        if (type == typeof(string)) { value = "x"; return true; }
        if (typeof(Delegate).IsAssignableFrom(type)) { value = NoOp(type); return true; }
        if (type.IsArray) { value = Array.CreateInstance(type.GetElementType()!, 0); return true; }
        if (type.IsGenericType && CollectionInterfaces.Contains(type.GetGenericTypeDefinition()))
        {
            value = Array.CreateInstance(type.GetGenericArguments()[0], 0);
            return true;
        }
        if (type.IsValueType) { value = Activator.CreateInstance(type); return true; }
        // The vocabulary's own base: any leaf will do, and Text is the one every screen has.
        if (type == typeof(VisualNode)) { value = new Text("x"); return true; }
        // A code surface's model is an interface in the vocabulary and a class in the engine; the
        // engine's controller is the one every code surface is built with.
        if (type == typeof(ICodeSurfaceModel)) { value = new eQuantic.UI.Code.CodeEditorController("x"); return true; }
        if (type.IsAbstract || type.IsInterface) return false;

        foreach (var ctor in type.GetConstructors().OrderBy(c => c.GetParameters().Length))
        {
            if (!TryArguments(ctor.GetParameters(), depth + 1, out var arguments, out _)) continue;
            try
            {
                value = ctor.Invoke(arguments);
                return true;
            }
            catch (TargetInvocationException)
            {
                // A constructor that refuses these arguments is not the one to build with.
            }
        }
        return false;
    }

    /// <summary>A delegate of the right shape that does nothing — any signature, no reflection
    /// over the callee, and the same instance kind on both sides of the comparison.</summary>
    private static Delegate NoOp(Type delegateType)
    {
        var invoke = delegateType.GetMethod("Invoke")!;
        var parameters = invoke.GetParameters()
            .Select(parameter => Expression.Parameter(parameter.ParameterType)).ToArray();
        var body = invoke.ReturnType == typeof(void)
            ? (Expression)Expression.Empty()
            : Expression.Default(invoke.ReturnType);
        return Expression.Lambda(delegateType, body, parameters).Compile();
    }

    /// <summary>
    /// A value DIFFERENT from the one omitting the argument gives, which is the only way to ask
    /// whether a body applies what it declares: a dropped tail answers with the omitted value, and
    /// nothing but a distinguishable one tells the two apart. Total, like
    /// <see cref="TryMake"/>: a type it cannot vary is named and fails.
    /// </summary>
    internal static bool TryVary(Type type, object? from, int depth, out object? value)
    {
        value = null;
        if (depth > 4) return false;

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            // A nullable tail's two states ARE the distinction: null becomes a value, a value null.
            if (from is null) return TryVary(underlying, Activator.CreateInstance(underlying), depth + 1, out value)
                || TryMake(underlying, depth + 1, out value);
            value = null;
            return true;
        }

        if (type == typeof(bool)) { value = !(bool)from!; return true; }
        if (type == typeof(string)) { value = (string?)from == "varied" ? "other" : "varied"; return true; }
        if (type.IsEnum)
        {
            value = Enum.GetValues(type).Cast<object>().FirstOrDefault(member => !Equals(member, from));
            return value is not null;
        }
        if (type.IsPrimitive || type == typeof(decimal))
        {
            value = Convert.ChangeType(Convert.ToDecimal(from) + 1, type);
            return true;
        }
        if (typeof(Delegate).IsAssignableFrom(type))
        {
            value = NoOp(type);
            return from is null || !Equals(value, from);
        }
        if (!type.IsValueType && from is null) return TryMake(type, depth + 1, out value);

        // Anything else — a struct like CornerRadii, or a class already holding a value — is varied
        // through its own widest constructor, with every argument varied in turn.
        foreach (var ctor in type.GetConstructors().OrderByDescending(c => c.GetParameters().Length))
        {
            var ctorParameters = ctor.GetParameters();
            if (ctorParameters.Length == 0) continue;
            if (!TryArguments(ctorParameters, depth + 1, out var arguments, out _)) continue;
            for (var i = 0; i < arguments.Length; i++)
                if (TryVary(ctorParameters[i].ParameterType, arguments[i], depth + 1, out var varied))
                    arguments[i] = varied;
            try
            {
                var made = ctor.Invoke(arguments);
                if (Equals(made, from)) continue;
                value = made;
                return true;
            }
            catch (TargetInvocationException)
            {
                // Not the constructor to vary through.
            }
        }
        return false;
    }
}
