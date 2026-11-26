using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI;

/// <summary>
/// A resx accessor read once, into a static (EQ3107): <c>static readonly string Title = Strings.Title;</c>.
/// <para>
/// An accessor answers in the culture of the moment it is read, and a static is initialized once, by
/// whoever reaches it first. On the server that is the first request, and every later one gets its
/// language whatever its own culture; in the browser it is the first page, and a change of language
/// leaves the text behind. Both build and render without a word, and only a second language shows it.
/// </para>
/// <para>
/// What is read later is not reported: a getter (<c>static string Title =&gt; Strings.Title;</c>), a
/// method, and a lambda or a local function the initializer only creates. The catalog's shape is
/// <see cref="ResourceShape"/>, the rule eqc rewrites accessors by. One file, linked into the web SDK's
/// generator and the native SDK's: a Photon app that changes its language keeps a static's text too.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CultureFrozenResourceAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor ReadOnceIntoAStatic = new(
        "EQ3107", "A localized string read once, into a static",
        "'{0}' is read once, when {1}, so it keeps the language of whoever reaches it first: on the server "
        + "every later request gets that request's text, and in the browser a change of language leaves it "
        + "behind. Read it where it is shown, in a getter ('=> {0}') or a method.",
        "eQuantic.UI", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(ReadOnceIntoAStatic);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Check, OperationKind.PropertyReference);
    }

    private static void Check(OperationAnalysisContext context)
    {
        var read = (IPropertyReferenceOperation)context.Operation;
        if (!ResourceShape.IsResourceAccessor(read.Property)) return;
        if (ReadOnceAt(read, context.ContainingSymbol) is not { } when) return;

        context.ReportDiagnostic(Diagnostic.Create(ReadOnceIntoAStatic, read.Syntax.GetLocation(),
            $"{read.Property.ContainingType.Name}.{read.Property.Name}", when));
    }

    /// <summary>
    /// When the read runs once, said for the message: a static field's or property's initializer, or a
    /// static constructor. Null for a read that runs each time its member is reached, or inside a lambda
    /// or a local function, which runs when it is called.
    /// </summary>
    private static string? ReadOnceAt(IOperation read, ISymbol containing)
    {
        var root = read;
        for (var at = read.Parent; at is not null; at = at.Parent)
        {
            if (at is IAnonymousFunctionOperation or ILocalFunctionOperation) return null;
            root = at;
        }

        return root switch
        {
            IFieldInitializerOperation { InitializedFields: var fields } when fields.Any(field => field.IsStatic)
                => $"the static '{fields.First(field => field.IsStatic).Name}' is initialized",
            IPropertyInitializerOperation { InitializedProperties: var properties } when properties.Any(property => property.IsStatic)
                => $"the static '{properties.First(property => property.IsStatic).Name}' is initialized",
            _ when containing is IMethodSymbol { MethodKind: MethodKind.StaticConstructor } constructor
                => $"'{constructor.ContainingType.Name}' runs its static constructor",
            _ => null,
        };
    }
}
