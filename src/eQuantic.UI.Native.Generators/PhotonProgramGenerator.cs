using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Native.Generators;

/// <summary>
/// Writes the half of <c>Program</c> that is ceremony, from the half the app wrote.
/// <para>
/// A generator sees the COMPILATION, which a build step writing text into obj never could: it finds
/// the app's <c>CreateApp</c> by type, emits a direct call to it, and says so at compile time when
/// there is none or more than one. Nothing looks anything up at run time, on any platform.
/// </para>
/// <para>
/// Two things come out. A module initializer registers the factory — that is what a shell calls on
/// Android, where the system launches an Activity and no <c>Main</c> ever runs. And on the platforms
/// that DO have an entry point, the <c>Main</c> itself, as the other half of the same partial class.
/// </para>
/// </summary>
[Generator]
public sealed class PhotonProgramGenerator : IIncrementalGenerator
{
    private const string HostingNamespace = "eQuantic.UI.Native.Hosting";
    private const string ApplicationType = "eQuantic.UI.Native.Hosting.PhotonApplication";

    private static readonly DiagnosticDescriptor NoProgram = new(
        "EQ3001", "A Photon app has no program",
        "This app builds an executable but declares no program. Add a "
        + "`public static PhotonApplication CreateApp(string[] args)` — by convention on a "
        + "`public partial class Program`, whose other half is generated.",
        "eQuantic.UI", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ManyPrograms = new(
        "EQ3002", "A Photon app has more than one program",
        "'{0}' and '{1}' both declare CreateApp(string[]). An app is one program.",
        "eQuantic.UI", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var programs = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is MethodDeclarationSyntax { Identifier.ValueText: "CreateApp" },
                transform: static (ctx, _) => Program(ctx))
            .Where(static program => program is not null)
            .Collect();

        // Whether this app declared an app icon — the SDK makes the property compiler-visible.
        var icon = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.EQuanticAppIconSource", out var source)
            && !string.IsNullOrWhiteSpace(source));

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(programs).Combine(icon),
            static (context, input) => Emit(context, input.Left.Left, input.Left.Right, input.Right));
    }

    /// <summary>The shape that counts: public, static, one string[], returning a PhotonApplication.</summary>
    private static INamedTypeSymbol? Program(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not IMethodSymbol method) return null;
        if (method is not { IsStatic: true, DeclaredAccessibility: Accessibility.Public }) return null;
        if (method.ReturnType.ToDisplayString() != ApplicationType) return null;
        // netstandard2.0 is what Roslyn loads analyzers as, and it has no System.Index — so no
        // list patterns here, however much they would read better.
        if (method.Parameters.Length != 1) return null;
        if (method.Parameters[0].Type is not IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String })
            return null;
        return method.ContainingType;
    }

    private static void Emit(SourceProductionContext context, Compilation compilation,
        System.Collections.Immutable.ImmutableArray<INamedTypeSymbol?> programs, bool hasIcon)
    {
        // Nothing to write for a library that merely REFERENCES the hosting assembly.
        if (compilation.GetTypeByMetadataName(ApplicationType) is null) return;

        var executable = compilation.Options.OutputKind
            is OutputKind.ConsoleApplication or OutputKind.WindowsApplication;

        var distinct = programs.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>().ToArray();
        if (distinct.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(ManyPrograms, distinct[0].Locations.FirstOrDefault(),
                distinct[0].ToDisplayString(), distinct[1].ToDisplayString()));
            return;
        }

        if (distinct.Length == 0)
        {
            // A library with no program is an ordinary library; only an executable is missing one.
            if (executable) context.ReportDiagnostic(Diagnostic.Create(NoProgram, Location.None));
            return;
        }

        var program = distinct[0].ToDisplayString();
        var isPartialProgram = distinct[0].Name == "Program"
            && distinct[0].ContainingNamespace.IsGlobalNamespace
            && distinct[0].DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax())
                .OfType<TypeDeclarationSyntax>()
                .Any(declaration => declaration.Modifiers.Any(m => m.ValueText == "partial"));

        var source = new System.Text.StringBuilder();
        source.AppendLine("// <auto-generated/> Written by eQuantic.UI.Native.Generators.");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.AppendLine("namespace eQuantic.UI.Native.Hosting.Generated;");
        source.AppendLine();
        source.AppendLine("/// <summary>");
        source.AppendLine("/// Registers this app's program before anything can ask for it. A module initializer runs");
        source.AppendLine("/// when the assembly loads, which on Android is the only moment there is: the system");
        source.AppendLine("/// launches an Activity and no Main ever runs.");
        source.AppendLine("/// </summary>");
        source.AppendLine("internal static class PhotonProgramRegistration");
        source.AppendLine("{");
        source.AppendLine("    [System.Runtime.CompilerServices.ModuleInitializer]");
        source.AppendLine("    internal static void Register() =>");
        source.AppendLine($"        {HostingNamespace}.PhotonApplication.Program = args => {program}.CreateApp(args);");
        source.AppendLine("}");

        context.AddSource("PhotonProgram.g.cs", source.ToString());

        // Android launches an Activity, and it has to be one in the APP's assembly: an activity in
        // the shell would leave the app assembly unreferenced, unloaded, and its program unnamed.
        if (compilation.GetTypeByMetadataName("Android.App.ActivityAttribute") is not null)
        {
            var activity = new System.Text.StringBuilder();
            activity.AppendLine("// <auto-generated/> Written by eQuantic.UI.Native.Generators.");
            activity.AppendLine("#nullable enable");
            activity.AppendLine();
            activity.AppendLine("/// <summary>The launcher: this app's screen, hosted by the Photon activity.</summary>");
            // The launcher icon is not named here yet: Android collects its resources in a nested
            // build whose evaluation precedes anything this SDK writes, so generated mipmaps never
            // reach aapt. Naming one that is not in the package fails the build outright.
            _ = hasIcon;
            activity.AppendLine("[global::Android.App.Activity(");
            activity.AppendLine("    MainLauncher = true,");
            activity.AppendLine("    Exported = true,");
            activity.AppendLine("    // The app draws its OWN chrome: a system title bar would sit on top of a header the");
            activity.AppendLine("    // design already accounts for, and no tree would know it was there.");
            activity.AppendLine("    Theme = \"@android:style/Theme.Material.Light.NoActionBar\",");
            activity.AppendLine("    ConfigurationChanges =");
            activity.AppendLine("        global::Android.Content.PM.ConfigChanges.Orientation");
            activity.AppendLine("        | global::Android.Content.PM.ConfigChanges.ScreenSize");
            activity.AppendLine("        | global::Android.Content.PM.ConfigChanges.UiMode");
            activity.AppendLine("        | global::Android.Content.PM.ConfigChanges.ScreenLayout");
            activity.AppendLine("        | global::Android.Content.PM.ConfigChanges.Density)]");
            activity.AppendLine("public sealed class MainActivity : global::eQuantic.UI.Native.Shell.Android.PhotonActivity");
            activity.AppendLine("{");
            activity.AppendLine("}");
            context.AddSource("PhotonMainActivity.g.cs", activity.ToString());
        }

        if (!executable) return;

        // The entry point lives in the GLOBAL namespace, as the other half of `Program` when the app
        // named its program that — which is the convention — and on its own when it did not.
        var entry = new System.Text.StringBuilder();
        entry.AppendLine("// <auto-generated/> Written by eQuantic.UI.Native.Generators.");
        entry.AppendLine("#nullable enable");
        entry.AppendLine();
        entry.AppendLine("/// <summary>The entry point: the other half of the app's Program.</summary>");
        entry.AppendLine(isPartialProgram ? "public partial class Program" : "internal static class PhotonEntryPoint");
        entry.AppendLine("{");
        entry.AppendLine("    private static void Main(string[] args) =>");
        entry.AppendLine($"        {program}.CreateApp(args).Run();");
        entry.AppendLine("}");
        context.AddSource("PhotonEntryPoint.g.cs", entry.ToString());
    }
}
