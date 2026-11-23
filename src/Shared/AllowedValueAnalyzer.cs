using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI;

/// <summary>
/// A constant handed to a parameter outside the values it allows (EQ3106): an <c>Icon</c> of 22 dp,
/// which the node refuses when it is built, on the server and in the browser alike, so the page that
/// builds it breaks where nothing in the build looked.
/// <para>
/// The values a parameter allows are declared with .NET's own <c>[AllowedValues]</c>
/// (<c>System.ComponentModel.DataAnnotations</c>), never listed here: the vocabulary marks the
/// parameter, and every call that hands it a constant is read against the mark, an app's own
/// parameters included. A value only known when the code runs is left to the parameter's own check,
/// since no build can read it.
/// </para>
/// <para>
/// One file, linked into the web SDK's generator and the native SDK's, since an <c>Icon</c> throws on
/// Photon as it does on the web.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AllowedValueAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor OutsideTheAllowedValues = new(
        "EQ3106", "A constant outside the values its parameter allows",
        "{0} is not a value '{1}' of {2} allows: it takes {3}{4}",
        "eQuantic.UI", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(OutsideTheAllowedValues);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var allowed = start.Compilation.GetTypeByMetadataName("System.ComponentModel.DataAnnotations.AllowedValuesAttribute");
            if (allowed is null) return;
            start.RegisterOperationAction(operation => Check(operation, allowed), OperationKind.Argument);
        });
    }

    private static void Check(OperationAnalysisContext context, INamedTypeSymbol allowed)
    {
        var argument = (IArgumentOperation)context.Operation;
        // A default the parameter writes for itself is its author's, and a params array has no single value.
        if (argument.ArgumentKind != ArgumentKind.Explicit || argument.Parameter is not { } parameter) return;
        if (!argument.Value.ConstantValue.HasValue) return;

        foreach (var attribute in parameter.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, allowed)) continue;
            // netstandard2.0, which Roslyn loads analyzers as, has no System.Index for a list pattern.
            if (attribute.ConstructorArguments.Length != 1) continue;
            var values = attribute.ConstructorArguments[0];
            if (values.Kind != TypedConstantKind.Array) continue;

            var value = argument.Value.ConstantValue.Value;
            if (values.Values.Any(candidate => SameValue(candidate.Value, value))) continue;

            var why = attribute.NamedArguments
                .Where(named => named.Key == "ErrorMessage" && named.Value.Value is string { Length: > 0 })
                .Select(named => (string)named.Value.Value!)
                .FirstOrDefault();
            context.ReportDiagnostic(Diagnostic.Create(OutsideTheAllowedValues, argument.Value.Syntax.GetLocation(),
                Written(value), parameter.Name, Owner(parameter),
                Listed(values.Values.Select(candidate => Written(candidate.Value))),
                why is null ? "." : ". " + why));
        }
    }

    /// <summary>Whether two constants are the same value: numbers by their value whatever their type
    /// (the attribute holds <c>16f</c>, a call may hand <c>16</c> or <c>16d</c>), anything else by
    /// <see cref="object.Equals(object, object)"/>.</summary>
    private static bool SameValue(object? allowed, object? written) =>
        IsNumber(allowed) && IsNumber(written)
            ? System.Convert.ToDouble(allowed, CultureInfo.InvariantCulture) == System.Convert.ToDouble(written, CultureInfo.InvariantCulture)
            : Equals(allowed, written);

    private static bool IsNumber(object? value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static string Written(object? value) => value switch
    {
        null => "null",
        string text => "\"" + text + "\"",
        bool flag => flag ? "true" : "false",
        _ when IsNumber(value) => System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? "?",
        _ => value.ToString() ?? "?",
    };

    private static string Listed(IEnumerable<string> values)
    {
        var all = values.ToList();
        return all.Count switch
        {
            0 => "no value",
            1 => all[0],
            _ => string.Join(", ", all.Take(all.Count - 1)) + " or " + all[all.Count - 1],
        };
    }

    /// <summary>What a reader calls the method the parameter belongs to: <c>new Icon(…)</c> for a
    /// constructor, <c>Icon(…)</c> for a method.</summary>
    private static string Owner(IParameterSymbol parameter) => parameter.ContainingSymbol switch
    {
        IMethodSymbol { MethodKind: MethodKind.Constructor } constructor => $"new {constructor.ContainingType.Name}(…)",
        IMethodSymbol method => $"{method.Name}(…)",
        var other => other.Name,
    };
}
