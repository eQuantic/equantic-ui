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

    private static readonly DiagnosticDescriptor AppWritesTheEntryPoint = new(
        "EQ3006", "A Photon app writes its own entry point",
        "This app declares an entry point, and the SDK generates one from CreateApp — a program "
        + "cannot have two. Delete the Main (or the top-level statements): the generated one is "
        + "`CreateApp(args).Run()`, which is what yours would have to become. Whatever it did "
        + "first belongs inside CreateApp, where every platform runs it — Android launches an "
        + "Activity and no Main is ever called there.",
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

        // What the app asked of the device, read from its own fluent calls — see
        // CapabilityDeclarations for why this has to happen at compile time.
        var capabilities = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => CapabilityDeclarations.MightDeclare(node),
                transform: static (ctx, _) => CapabilityDeclarations.Read(ctx))
            .Where(static declaration => declaration is not null)
            .Collect();

        // And what the app asked of the SYSTEM — the release-time half, same idiom, same reason it
        // cannot wait for run time: an entitlement is signed into the binary at build.
        var entitlements = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => EntitlementDeclarations.MightDeclare(node),
                transform: static (ctx, _) => EntitlementDeclarations.Read(ctx))
            .Where(static declaration => declaration is not null)
            .Collect();

        // And what the system reads ABOUT the app — its copyright, its category, the URLs it
        // answers to. Same idiom again, and the last of the three platform-fact families.
        var bundle = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => BundleDeclarations.MightDeclare(node),
                transform: static (ctx, _) => BundleDeclarations.Read(ctx))
            .Where(static declaration => declaration is not null)
            .Collect();

        // And which schemes the app hands to the system beyond the web's: a run-time fact on a
        // desktop, and one a phone's manifest has to list as well.
        var opened = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => WorkspaceDeclarations.MightDeclare(node),
                transform: static (ctx, _) => System.Collections.Immutable.ImmutableArray.CreateRange(WorkspaceDeclarations.Read(ctx)))
            .SelectMany(static (declarations, _) => declarations)
            .Where(static declaration => declaration is not null)
            .Collect();

        // The declaration families travel together rather than as more levels of Combine:
        // `input.Left.Left.Left.Left.Left` is not a thing anyone can read, and the next family
        // would add another one.
        var declarations = capabilities.Combine(entitlements).Combine(bundle).Combine(opened);

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(programs).Combine(icon).Combine(declarations),
            static (context, input) => Emit(context,
                input.Left.Left.Left,       // compilation
                input.Left.Left.Right,      // programs
                input.Left.Right,           // has an app icon
                input.Right.Left.Left.Left,     // capabilities
                input.Right.Left.Left.Right,    // entitlements
                input.Right.Left.Right,         // bundle facts
                input.Right.Right));            // opened schemes
    }

    /// <summary>Ordered, so the generated file is the same file for the same declarations: a
    /// manifest that reshuffles is a diff nobody can review.</summary>
    private static System.Collections.Generic.List<string> Sorted(
        System.Collections.Generic.IEnumerable<string> keys)
    {
        var list = new System.Collections.Generic.List<string>(keys);
        list.Sort(System.StringComparer.Ordinal);
        return list;
    }

    /// <summary>A C# string literal for a value that came from the app's own source.</summary>
    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

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
        System.Collections.Immutable.ImmutableArray<INamedTypeSymbol?> programs, bool hasIcon,
        System.Collections.Immutable.ImmutableArray<CapabilityDeclaration?> capabilities,
        System.Collections.Immutable.ImmutableArray<(string? Key, Location Location)?> entitlements,
        System.Collections.Immutable.ImmutableArray<BundleDeclarations.Declaration?> bundle,
        System.Collections.Immutable.ImmutableArray<WorkspaceDeclarations.Declaration?> opened)
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

        // ---- What the app asked of the SYSTEM ------------------------------------------------
        // The same journey as a capability, one file further along: fluent call → assembly
        // attribute → the entitlements plist codesign is handed. Nobody writes a plist.
        var entitled = new System.Collections.Generic.SortedSet<string>(System.StringComparer.Ordinal);
        foreach (var asked in entitlements)
        {
            if (asked is not { } declaration) continue;
            if (string.IsNullOrEmpty(declaration.Key))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    EntitlementDeclarations.KeyMustBeConstant, declaration.Location));
                continue;
            }
            entitled.Add(declaration.Key!);
        }

        if (entitled.Count > 0)
        {
            var file = new System.Text.StringBuilder();
            file.AppendLine("// <auto-generated/> Written by eQuantic.UI.Native.Generators.");
            file.AppendLine("#nullable enable");
            file.AppendLine();
            file.AppendLine("// What this app needs the SYSTEM to permit, said once on the builder. The SDK reads");
            file.AppendLine("// these off the compiled assembly and writes the entitlements file codesign is handed —");
            file.AppendLine("// and adds what the .NET runtime itself needs, which is never the app author's to know.");
            foreach (var key in entitled)
                file.AppendLine($"[assembly: global::eQuantic.UI.Native.Hosting.PhotonEntitlement({Quote(key)})]");
            context.AddSource("PhotonEntitlements.g.cs", file.ToString());
        }

        // ---- What the system reads ABOUT the app -----------------------------------------------
        // One more step along the same road: fluent call → assembly attribute → Info.plist. The
        // keys are Apple's; the app author never types one unless they reach for the escape hatch.
        var facts = new System.Collections.Generic.List<BundleDeclarations.Declaration>();
        foreach (var stated in bundle)
        {
            if (stated is not { } declaration) continue;
            if (declaration.Key is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    BundleDeclarations.ValueMustBeConstant, declaration.Location, declaration.Method));
                continue;
            }
            facts.Add(declaration);
        }

        // The schemes outlive this block: Android reads them off the launcher activity, below.
        var schemes = new System.Collections.Generic.List<string>();
        if (facts.Count > 0)
        {
            // LAST WINS for a repeated key, and the URL schemes accumulate — the same semantics the
            // builder itself has, so what the generated file declares is what the C# said.
            var byKey = new System.Collections.Generic.Dictionary<string, BundleDeclarations.Declaration>(
                System.StringComparer.Ordinal);
            foreach (var fact in facts)
            {
                if (fact.Kind == "UrlScheme")
                {
                    if (!schemes.Contains(fact.Value)) schemes.Add(fact.Value);
                }
                else
                {
                    byKey[fact.Key!] = fact;
                }
            }

            var manifest = new System.Text.StringBuilder();
            manifest.AppendLine("// <auto-generated/> Written by eQuantic.UI.Native.Generators.");
            manifest.AppendLine("#nullable enable");
            manifest.AppendLine();
            manifest.AppendLine("// What the system reads about this app before it runs, said once on the builder. The");
            manifest.AppendLine("// SDK reads these off the compiled assembly and writes the Info.plist — nobody opens one.");
            foreach (var key in Sorted(byKey.Keys))
            {
                var fact = byKey[key];
                manifest.AppendLine($"[assembly: global::eQuantic.UI.Native.Hosting.PhotonBundleKey("
                    + $"{Quote(key)}, {Quote(fact.Value)}, "
                    + $"global::eQuantic.UI.Native.Hosting.PhotonBundleValueKind.{fact.Kind})]");
            }
            foreach (var scheme in schemes)
            {
                manifest.AppendLine($"[assembly: global::eQuantic.UI.Native.Hosting.PhotonBundleKey("
                    + $"\"\", {Quote(scheme)}, "
                    + "global::eQuantic.UI.Native.Hosting.PhotonBundleValueKind.UrlScheme)]");
            }
            context.AddSource("PhotonBundle.g.cs", manifest.ToString());
        }

        // ---- What the app hands to the system ------------------------------------------------
        // A phone answers "does something open this link" only for the schemes its manifest lists,
        // so a scheme that cannot be read here can never be listed: the app would open the link and
        // be told nothing opens it. That is an error where a manifest is written. On a desktop
        // nothing reads the list, and a scheme built at run time is as good as any other.
        var phone = compilation.GetTypeByMetadataName("Android.App.ActivityAttribute") is not null
            || compilation.GetTypeByMetadataName("UIKit.UIApplication") is not null;
        var openedSchemes = new System.Collections.Generic.SortedSet<string>(System.StringComparer.Ordinal);
        foreach (var stated in opened)
        {
            if (stated is not { } declaration) continue;
            if (declaration.Scheme is { } scheme) openedSchemes.Add(scheme);
            else if (phone)
                context.ReportDiagnostic(Diagnostic.Create(
                    WorkspaceDeclarations.SchemeMustBeConstant, declaration.Location));
        }

        if (openedSchemes.Count > 0)
        {
            var schemesOpened = new System.Text.StringBuilder();
            schemesOpened.AppendLine("// <auto-generated/> Written by eQuantic.UI.Native.Generators.");
            schemesOpened.AppendLine("#nullable enable");
            schemesOpened.AppendLine();
            schemesOpened.AppendLine("// The schemes this app hands to the system, said once on the builder. A phone's build");
            schemesOpened.AppendLine("// reads these off the compiled assembly and lists them where the system looks before it");
            schemesOpened.AppendLine("// answers CanOpen: nobody opens an Info.plist or an AndroidManifest.xml.");
            foreach (var scheme in openedSchemes)
            {
                schemesOpened.AppendLine(
                    $"[assembly: global::eQuantic.UI.Native.Hosting.PhotonOpenedScheme({Quote(scheme)})]");
            }
            context.AddSource("PhotonOpenedSchemes.g.cs", schemesOpened.ToString());
        }

        // ---- What the app asked of the device ------------------------------------------------
        // One declaration in C#, and the platform files are written from it: the app author never
        // opens an Info.plist or an AndroidManifest.xml.
        var declared = new System.Collections.Generic.Dictionary<string, CapabilityDeclaration>();
        foreach (var asked in capabilities)
        {
            if (asked is not { } declaration) continue;
            if (string.IsNullOrEmpty(declaration.Reason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    CapabilityDeclarations.ReasonMustBeConstant, declaration.Location, declaration.Capability));
                continue;
            }
            declared[declaration.Capability] = declaration;
        }

        if (declared.Count > 0)
        {
            var caps = new System.Text.StringBuilder();
            caps.AppendLine("// <auto-generated/> Written by eQuantic.UI.Native.Generators.");
            caps.AppendLine("#nullable enable");
            caps.AppendLine();
            caps.AppendLine("// The attribute is how a DECLARATION reaches the build: the SDK reads these off the");
            caps.AppendLine("// compiled assembly and writes the Apple manifest keys. Nobody writes them by hand —");
            caps.AppendLine("// the app said what it needs once, fluently, on the builder.");
            foreach (var declaration in declared.Values)
            {
                caps.AppendLine($"[assembly: global::eQuantic.UI.Native.Hosting.PhotonCapability("
                    + $"{Quote(declaration.Capability)}, {Quote(declaration.Reason)})]");
            }

            // Android reads its permissions from the manifest, and the manifest is merged from
            // assembly attributes — so the same declaration lands there too, in that platform's
            // own spelling.
            if (compilation.GetTypeByMetadataName("Android.App.UsesPermissionAttribute") is not null)
            {
                caps.AppendLine();
                foreach (var declaration in declared.Values)
                foreach (var permission in CapabilityDeclarations.AndroidPermissions(declaration.Capability))
                {
                    caps.AppendLine($"[assembly: global::Android.App.UsesPermission({Quote(permission)})]");
                }
            }

            context.AddSource("PhotonCapabilities.g.cs", caps.ToString());
        }

        // Android launches an Activity, and it has to be one in the APP's assembly: an activity in
        // the shell would leave the app assembly unreferenced, unloaded, and its program unnamed.
        if (compilation.GetTypeByMetadataName("Android.App.ActivityAttribute") is not null)
        {
            var activity = new System.Text.StringBuilder();
            activity.AppendLine("// <auto-generated/> Written by eQuantic.UI.Native.Generators.");
            activity.AppendLine("#nullable enable");
            activity.AppendLine();
            if (hasIcon)
            {
                // The launcher reads the icon from the APPLICATION, not from the activity: it is
                // the app that has an icon, and every activity in it inherits the same one.
                //
                // Emitted BEFORE the class's doc comment, not between it and the class: an
                // assembly-level attribute is a declaration of its own, so a `///` above it belongs
                // to nothing and C# discards it (CS1587). The summary below only survived in apps
                // that declared no icon.
                activity.AppendLine("[assembly: global::Android.App.Application(Icon = \"@mipmap/ic_launcher\")]");
                activity.AppendLine();
            }
            activity.AppendLine("/// <summary>The launcher: this app's screen, hosted by the Photon activity.</summary>");
            activity.AppendLine("[global::Android.App.Activity(");
            activity.AppendLine("    MainLauncher = true,");
            activity.AppendLine("    Exported = true,");
            activity.AppendLine("    // The app draws its OWN chrome: a system title bar would sit on top of a header the");
            activity.AppendLine("    // design already accounts for, and no tree would know it was there.");
            activity.AppendLine("    Theme = \"@android:style/Theme.Material.Light.NoActionBar\",");
            // Predictive back: the shell registers for back only while something is open, so the
            // system can show "leave the app" under the finger before the gesture ends. Android 16
            // assumes it for an app that targets it; Android 14 and 15 only for one that says so.
            // The application-level spelling Android 13 also reads has no attribute in .NET for
            // Android, and there the back KEY reaches the same handler.
            activity.AppendLine("    EnableOnBackInvokedCallback = true,");
            if (schemes.Count > 0)
            {
                // ONE activity, in one task, for an app that links open. A link opened by another
                // app starts the activity it names, and by default that is a second instance on top
                // of the running one: a second surface, a second tree built from the app's root, and
                // the first one's state under it. Asked for as a single task, the running activity
                // is handed the link instead. It has a price, which is why an app with no scheme
                // does not pay it: re-entered from its icon, a single task ends whatever it had
                // opened on top of itself, a photo picker among them.
                activity.AppendLine("    LaunchMode = global::Android.Content.PM.LaunchMode.SingleTask,");
            }
            // Each change listed here is the HOST's to follow, so Android keeps the activity, and the
            // app's state with it, instead of building a new one: a rotation, a window resized into
            // split screen or onto a foldable's other display (screen size and smallest screen
            // size), a hardware keyboard attached (and the d-pad or trackball one can bring), the
            // system's dark mode, a new density.
            activity.AppendLine("    ConfigurationChanges =");
            activity.AppendLine("        global::Android.Content.PM.ConfigChanges.Orientation");
            activity.AppendLine("        | global::Android.Content.PM.ConfigChanges.ScreenSize");
            activity.AppendLine("        | global::Android.Content.PM.ConfigChanges.SmallestScreenSize");
            activity.AppendLine("        | global::Android.Content.PM.ConfigChanges.ScreenLayout");
            activity.AppendLine("        | global::Android.Content.PM.ConfigChanges.Keyboard");
            activity.AppendLine("        | global::Android.Content.PM.ConfigChanges.KeyboardHidden");
            activity.AppendLine("        | global::Android.Content.PM.ConfigChanges.Navigation");
            activity.AppendLine("        | global::Android.Content.PM.ConfigChanges.UiMode");
            activity.AppendLine("        | global::Android.Content.PM.ConfigChanges.Density)]");
            if (schemes.Count > 0
                && compilation.GetTypeByMetadataName("Android.App.IntentFilterAttribute") is not null)
            {
                // The schemes the app declared on the builder, as the filter Android matches a link
                // against: VIEW is "open this", DEFAULT is what an implicit intent needs to reach
                // an activity at all, and BROWSABLE is what lets a link in a browser do it.
                var quoted = new System.Collections.Generic.List<string>();
                foreach (var scheme in schemes) quoted.Add(Quote(scheme));
                activity.AppendLine("[global::Android.App.IntentFilter(");
                activity.AppendLine("    new[] { global::Android.Content.Intent.ActionView },");
                activity.AppendLine("    Categories = new[]");
                activity.AppendLine("    {");
                activity.AppendLine("        global::Android.Content.Intent.CategoryDefault,");
                activity.AppendLine("        global::Android.Content.Intent.CategoryBrowsable,");
                activity.AppendLine("    },");
                activity.AppendLine($"    DataSchemes = new[] {{ {string.Join(", ", quoted)} }})]");
            }
            activity.AppendLine("public sealed class MainActivity : global::eQuantic.UI.Native.Shell.Android.PhotonActivity");
            activity.AppendLine("{");
            if (schemes.Count > 0)
            {
                // The same schemes as CODE, for the shell to hold an intent against: the filter
                // above is the system's, and an intent aimed at this activity by name skips it.
                var known = new System.Collections.Generic.List<string>();
                foreach (var scheme in schemes) known.Add(Quote(scheme));
                activity.AppendLine("    protected override global::System.Collections.Generic.IReadOnlyList<string> UrlSchemes { get; } =");
                activity.AppendLine($"        new[] {{ {string.Join(", ", known)} }};");
            }
            activity.AppendLine("}");
            context.AddSource("PhotonMainActivity.g.cs", activity.ToString());
        }

        if (!executable) return;

        // An app ADOPTING this SDK arrives with a Main already written, and until this check it got
        // CS0017 — "more than one entry point" — pointing at its own line, naming a generated file
        // it had never heard of and saying nothing about why a second one exists. Reported here
        // instead, at the app's own Main, saying what to delete and where the work goes.
        //
        // GetEntryPoint is the whole test: THIS compilation does not contain the generated half
        // yet, so anything it finds is the app's, and top-level statements (which synthesise a Main)
        // are caught by the same call. Returning before the emit leaves ONE error rather than two.
        var appEntryPoint = compilation.GetEntryPoint(context.CancellationToken);
        if (appEntryPoint is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(AppWritesTheEntryPoint,
                appEntryPoint.Locations.FirstOrDefault()));
            return;
        }

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
