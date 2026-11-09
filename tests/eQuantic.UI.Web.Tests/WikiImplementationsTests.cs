using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit.Abstractions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A type the wiki writes over one of the SDK's interfaces implements it as the interface IS: a member
/// named like one of the interface's has the interface's type, in every page and every language.
///
/// <para>
/// <c>WikiClaimsCompile</c> compiles the snippets someone copied into it, and a snippet nobody copied is
/// held by nothing. The DesignSystem page's "The document is the theme's" was never copied, and when
/// <c>IAppTheme.TextFamily</c> became a <c>FontFamily</c> in 0.2.0-preview.66 its theme went on saying
/// <c>public string? TextFamily =&gt; "…"</c> in all five languages, while the section beside it, which
/// had been copied, moved with the change (#1226). A reader who copies it meets CS0738 and no word of the
/// shape that replaced it.
/// </para>
///
/// <para>
/// DERIVED, not transcribed. Every C# block of every page is compiled alone against the SDK's assemblies,
/// and every type it declares is read from the compiler's own symbols: the interfaces it names, resolved
/// to the SDK's, and each of its members beside the interface's member of that name. A block is a
/// fragment ("…the rest delegates to inner"), so a member the snippet leaves out is not an offence, and a
/// name the block cannot bind (a type the page declared in another block) is compared as it is written.
/// What is compared is the signature, which is the part a reader copies and the compiler refuses.
/// </para>
/// </summary>
public class WikiImplementationsTests(ITestOutputHelper output)
{
    /// <summary>A fenced C# block, its body and where it starts. A line may end in CRLF: the wiki is
    /// checked out with Windows' line endings on the Windows leg, and a <c>$</c> stops only before
    /// <c>\n</c>, so a fence closed by <c>```\r</c> was never closed there and the guard read nothing.</summary>
    private static readonly Regex Block = new(
        @"(?ms)^[ \t]*```(?:csharp|cs|c\#)(?:[ \t][^\r\n]*)?\r?\n(?<code>.*?)^[ \t]*```[ \t]*\r?$", RegexOptions.Compiled);

    /// <summary>
    /// A type the way a reader writes it: no namespace, generic arguments named, keywords for the
    /// special types, and the nullable annotation kept, since <c>FontFamily</c> and <c>FontFamily?</c>
    /// are different answers to "may this be null".
    /// </summary>
    private static readonly SymbolDisplayFormat AsWritten = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>The language eqc parses an app in (<c>Services/ParseDefaults</c>), so a snippet in the
    /// newest syntax the SDK takes parses here too.</summary>
    private static readonly CSharpParseOptions Parse = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

    private static readonly Lazy<CSharpCompilation> Sdk = new(Compile);

    /// <summary>
    /// What the compiler says of a NAME it could not take: not found (CS0246, CS0234), the wrong number of
    /// type arguments (CS0305, CS0308), or inaccessible (CS0122). The type's other errors land on the same
    /// span, a member the fragment left out (CS0535) among them, and say nothing about the name.
    /// </summary>
    private static readonly HashSet<string> NameErrors = ["CS0246", "CS0234", "CS0305", "CS0308", "CS0122"];

    /// <summary>The projects under <c>src/</c>, by name, each of which builds the assembly it is named for.</summary>
    private static readonly Lazy<HashSet<string>> SdkAssemblies = new(() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToHashSet(StringComparer.Ordinal));

    /// <summary>A public interface a source file declares, by its name.</summary>
    private static readonly Regex Interface = new(@"\bpublic\s+(?:partial\s+)?interface\s+(?<name>I[A-Z]\w*)", RegexOptions.Compiled);

    /// <summary>The public interfaces <c>src/</c> declares, by name: what a snippet may implement, and
    /// so what this guard must be able to resolve.</summary>
    private static readonly Lazy<HashSet<string>> SdkInterfaces = new(() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => Interface.Matches(File.ReadAllText(path)).Select(match => match.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal));

    [Fact]
    public void EveryTypeAWikiSnippetWritesOverAnSdkInterface_DeclaresTheInterfacesSignatures_InEveryLanguage()
    {
        if (WikiClone.Absent()) return;

        var offences = new List<string>();
        var compared = 0;
        var pages = 0;
        foreach (var path in Directory.EnumerateFiles(WikiClone.Location!, "*.md", SearchOption.AllDirectories)
                     .Where(path => !Path.GetRelativePath(WikiClone.Location!, path).StartsWith(".git", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            var page = Path.GetRelativePath(WikiClone.Location!, path);
            var text = File.ReadAllText(path);
            var before = compared;
            foreach (Match block in Block.Matches(text))
            {
                var line = text.AsSpan(0, block.Groups["code"].Index).Count('\n') + 1;
                compared += Read(page, line, block.Groups["code"].Value, offences);
            }
            if (compared > before) pages++;
        }
        output.WriteLine($"{compared} members compared with the SDK interface they implement, in {pages} pages");

        // Not vacuous: a pattern that stopped matching the wiki's fences, or references that stopped
        // resolving the SDK's interfaces, would read every page and compare nothing.
        compared.Should().BeGreaterThan(0, $"the wiki's snippets implement the SDK's interfaces; read from {WikiClone.Describe()}");

        // Joined, so every language's copy of the same drift is named in one run (see WikiDiagnosticsTests).
        string.Join("\n", offences).Should().BeEmpty(
            "a snippet that implements an SDK interface is copied by readers as it stands, so each member it "
            + "declares has the type the interface gives it: write the interface's type, in every language the "
            + $"page is written in, on the wiki branch named like this pull request's (#406). Read from {WikiClone.Describe()}");
    }

    /// <summary>
    /// The theme examples are what this guard was written for, so it says it read them: every language
    /// the DesignSystem page is written in has a type over <c>IAppTheme</c> whose members were compared.
    /// </summary>
    [Fact]
    public void TheDesignSystemsThemes_AreRead_InEveryLanguage()
    {
        if (WikiClone.Absent()) return;

        var copies = WikiClone.Copies("DesignSystem");
        // Named rather than discovered, as every docs guard names them: a rename that lost one fails here.
        copies.Select(copy => copy.Language).Should().Contain(["English", "pt-BR"]);
        foreach (var (language, path) in copies)
        {
            var text = File.ReadAllText(path);
            var offences = new List<string>();
            var themes = Block.Matches(text)
                .Sum(block => Read(language, 0, block.Groups["code"].Value, offences, only: "IAppTheme"));
            themes.Should().BeGreaterThan(0, $"the {language} DesignSystem page writes themes over IAppTheme, and none was compared");
        }
    }

    /// <summary>
    /// The reading itself, without a wiki: each kind of drift it exists for is an offence, and the
    /// snippets the pages write as they should are not. A guard that read the wiki and found nothing
    /// would otherwise be indistinguishable from one that could not find anything.
    /// </summary>
    [Theory]
    [InlineData("the type that drifted (#1226)",
        "class Brand(IAppTheme inner) : IAppTheme { public string? TextFamily => \"Inter\"; }",
        "Brand.TextFamily is written `property string?`, and IAppTheme.TextFamily is `property FontFamily?`")]
    [InlineData("a nullability the interface does not have",
        "class Brand(IAppTheme inner) : IAppTheme { public ColorToken? Selection => null; }",
        "Brand.Selection is written `property ColorToken?`, and IAppTheme.Selection is `property ColorToken`")]
    [InlineData("a method's parameter",
        "class Page : IServerPrefetch { public Task PrefetchAsync(IServiceProvider services, string slug) => Task.CompletedTask; }",
        "Page.PrefetchAsync is written `method Task(IServiceProvider, string)`, and IServerPrefetch.PrefetchAsync is `method Task(IServiceProvider, CancellationToken)`")]
    [InlineData("a property where the interface has a method",
        "class Brand(IAppTheme inner) : IAppTheme { public TypeStyle Type => default; }",
        "Brand.Type is written `property TypeStyle`, and IAppTheme.Type is `method TypeStyle(TypeRole)`")]
    [InlineData("an explicit implementation",
        "class Page : IServerPrefetch { Task IServerPrefetch.PrefetchAsync(IServiceProvider services, int ct) => Task.CompletedTask; }",
        "Page.PrefetchAsync is written `method Task(IServiceProvider, int)`")]
    [InlineData("an interface src/ declares in an assembly the test host does not carry",
        "class Backend : IRenderBackend { public void Draw() { } }",
        "Backend implements IRenderBackend, which src/ declares and this guard cannot resolve")]
    [InlineData("an SDK interface written without its type argument",
        "class Assets : IComponentAssetProvider { }",
        "Assets implements IComponentAssetProvider, which src/ declares and this guard cannot resolve")]
    public void ADriftedSignature_IsAnOffence(string kind, string code, string offence)
    {
        var offences = new List<string>();

        Read("Page.md", 1, code, offences);

        offences.Should().ContainSingle(kind).Which.Should().Contain(offence, kind);
    }

    [Theory]
    [InlineData("the theme the page now writes",
        "public sealed class BrandTheme(IAppTheme inner) : IAppTheme\n{\n    public FontFamily? TextFamily => new(\"Atkinson Hyperlegible Next\");\n"
        + "    public ColorToken Selection => new(new Color(0x1B, 0x5E, 0x4B, 255));\n    public ColorToken? OnSelection => new(Color.White);\n"
        + "    // …the rest delegates to inner\n}")]
    [InlineData("a qualified name and a member of the snippet's own",
        "class Brand(IAppTheme inner) : IAppTheme { public eQuantic.UI.Primitives.FontFamily? MonoFamily => null; public int Extra => 1; }")]
    [InlineData("a type the page declared in another block, compared as written",
        "class Room : IServerPrefetch { public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken) => Load(); }")]
    [InlineData("an interface of the app's own",
        "interface IDocs { Task<string?> FindAsync(string? slug); }\nclass Docs : IDocs { public Task<string?> FindAsync(string? slug) => Task.FromResult(slug); }")]
    public void ASnippetWrittenAsTheInterfaceIs_IsNot(string kind, string code)
    {
        var offences = new List<string>();

        Read("Page.md", 1, code, offences);

        offences.Should().BeEmpty(kind);
    }

    /// <summary>A fence is read whatever ends its lines, and whatever follows its language.</summary>
    [Theory]
    [InlineData("\n", "")]
    [InlineData("\r\n", "")]
    [InlineData("\r\n", " title=\"Theme.cs\"")]
    public void AFence_IsRead_WhateverEndsItsLines(string newline, string info)
    {
        var page = $"Text{newline}{newline}```csharp{info}{newline}class A {{ }}{newline}```{newline}{newline}More text{newline}";

        Block.Matches(page).Should().ContainSingle().Which.Groups["code"].Value.Trim().Should().Be("class A { }");
    }

    /// <summary>
    /// Compares the types <paramref name="code"/> declares with the SDK interfaces they name, adds an
    /// offence for each member that differs, and answers how many members were compared.
    /// </summary>
    private static int Read(string page, int line, string code, List<string> offences, string? only = null)
    {
        var tree = CSharpSyntaxTree.ParseText(code, Parse);
        var declarations = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Where(type => type is not InterfaceDeclarationSyntax && type.BaseList is not null)
            .ToList();
        if (declarations.Count == 0) return 0;

        var model = Sdk.Value.AddSyntaxTrees(tree).GetSemanticModel(tree);
        var compared = 0;
        foreach (var declaration in declarations)
        {
            if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol type) continue;

            // A name the SDK declares and this compilation cannot bind would be skipped in silence: the
            // assembly that declares it is not one the test host carries, and every snippet over it
            // would pass unread. Measured when this was written: IRequireAssets, IHandleMetadata and
            // three more lived in eQuantic.UI.Server, which this project did not reference.
            // Read from the base list as written: the compiler takes an unbound FIRST name for the base
            // class, so it is never among the type's interfaces.
            // A name written with the wrong number of type arguments binds to the interface with an
            // error beside it, so the compiler's own diagnostic on the name is what says so.
            var unbound = declaration.BaseList!.Types
                .Select(written => model.GetTypeInfo(written.Type).Type)
                .Zip(declaration.BaseList.Types, (bound, written) => (bound, written))
                .Where(pair => pair.bound is { } bound && SdkInterfaces.Value.Contains(bound.Name)
                    && (pair.bound is IErrorTypeSymbol { CandidateReason: not CandidateReason.Ambiguous }
                        || model.GetDiagnostics(NameOf(pair.written.Type).Span).Any(diagnostic => NameErrors.Contains(diagnostic.Id))))
                .Select(pair => pair.bound!)
                .ToList();
            foreach (var name in unbound.Select(bound => bound.Name).Distinct(StringComparer.Ordinal))
                offences.Add($"{Where(page, line, declaration)}: {type.Name} implements {name}, which src/ declares "
                    + "and this guard cannot resolve: reference the project that declares it from eQuantic.UI.Web.Tests, "
                    + "or write it with its type arguments");

            var interfaces = type.Interfaces
                .SelectMany(candidate => candidate is IErrorTypeSymbol { CandidateReason: CandidateReason.Ambiguous } error
                    ? error.CandidateSymbols.OfType<INamedTypeSymbol>()
                    : [candidate])
                .Where(candidate => candidate.TypeKind == TypeKind.Interface && IsSdk(candidate))
                .Where(candidate => !unbound.Any(broken => broken.Name == candidate.Name))
                .Where(candidate => only is null || candidate.Name == only)
                .ToList();
            if (interfaces.Count == 0) continue;

            foreach (var member in declaration.Members)
            {
                if (model.GetDeclaredSymbol(member) is not { IsStatic: false } declared) continue;
                var name = declared.Name[(declared.Name.LastIndexOf('.') + 1)..];
                var counterparts = interfaces
                    .SelectMany(contract => contract.AllInterfaces.Prepend(contract)
                        .SelectMany(inherited => inherited.GetMembers(name).Select(counterpart => (contract: inherited, counterpart))))
                    .Where(pair => pair.counterpart is IPropertySymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary } or IEventSymbol)
                    .ToList();
                if (counterparts.Count == 0) continue;

                compared++;
                var written = Signature(declared, member);
                if (counterparts.Any(pair => Signature(pair.counterpart, null) == written)) continue;

                offences.Add(
                    $"{Where(page, line, member)}: {type.Name}.{name} is written `{written}`, and "
                    + string.Join(" or ", counterparts.Select(pair => $"{pair.contract.ToDisplayString(AsWritten)}.{name} is `{Signature(pair.counterpart, null)}`")));
            }
        }
        return compared;
    }

    /// <summary>The name a base is written with, without its type arguments: a type argument the
    /// block cannot bind is the page's own type, not a name of the SDK's.</summary>
    private static SyntaxNodeOrToken NameOf(TypeSyntax written) => written switch
    {
        GenericNameSyntax generic => generic.Identifier,
        QualifiedNameSyntax { Right: GenericNameSyntax generic } => generic.Identifier,
        _ => written,
    };

    /// <summary>The page and the line of <paramref name="node"/> in it, or the page alone where the
    /// block's line is not known.</summary>
    private static string Where(string page, int line, SyntaxNode node) =>
        line > 0 ? $"{page}:{line + node.GetLocation().GetLineSpan().StartLinePosition.Line}" : page;

    /// <summary>
    /// A member's signature as a reader writes it: its kind, its type, and a method's parameters. From
    /// the compiler's symbol where it bound, and from the snippet's own text where it did not, so a type
    /// the page declared in another block still compares by the name the page gave it.
    /// </summary>
    private static string Signature(ISymbol symbol, MemberDeclarationSyntax? syntax) => symbol switch
    {
        IPropertySymbol property when property.IsIndexer =>
            $"indexer {Type(property.Type, (syntax as IndexerDeclarationSyntax)?.Type)}["
            + string.Join(", ", property.Parameters.Select((parameter, index) =>
                Modifier(parameter.RefKind)
                + Type(parameter.Type, (syntax as IndexerDeclarationSyntax)?.ParameterList.Parameters[index].Type)))
            + "]",
        IPropertySymbol property => $"property {Type(property.Type, (syntax as PropertyDeclarationSyntax)?.Type)}",
        IEventSymbol @event => $"event {Type(@event.Type, null)}",
        IMethodSymbol method => $"method {Type(method.ReturnType, (syntax as MethodDeclarationSyntax)?.ReturnType)}("
            + string.Join(", ", method.Parameters.Select((parameter, index) =>
                Modifier(parameter.RefKind)
                + Type(parameter.Type, (syntax as MethodDeclarationSyntax)?.ParameterList.Parameters[index].Type)))
            + ")",
        _ => symbol.Kind.ToString(),
    };

    private static string Modifier(RefKind kind) => kind switch
    {
        RefKind.Out => "out ",
        RefKind.Ref => "ref ",
        RefKind.In => "in ",
        RefKind.RefReadOnlyParameter => "ref readonly ",
        _ => "",
    };

    /// <summary>The type as written: the symbol's name where it bound whole, the snippet's text where any
    /// part of it did not, without the namespaces a reader may or may not have spelled.</summary>
    private static string Type(ITypeSymbol type, TypeSyntax? syntax) =>
        syntax is not null && Unbound(type)
            ? Regex.Replace(Regex.Replace(syntax.ToString(), @"\s+", ""), @"\b(?:[A-Za-z_]\w*\.)+(?=[A-Za-z_])", "")
            : type.ToDisplayString(AsWritten);

    private static bool Unbound(ITypeSymbol type) => type switch
    {
        IErrorTypeSymbol => true,
        IArrayTypeSymbol array => Unbound(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(Unbound),
        _ => false,
    };

    private static bool IsSdk(ISymbol symbol) => symbol.ContainingAssembly is { } assembly && IsSdk(assembly);

    /// <summary>An assembly of the SDK: an eQuantic.UI project under <c>src/</c>, which a test assembly
    /// (this one is on the host's list too) is not, so a type a test declares never shadows the SDK's.</summary>
    private static bool IsSdk(IAssemblySymbol assembly) =>
        assembly.Name.StartsWith("eQuantic.UI", StringComparison.Ordinal) && SdkAssemblies.Value.Contains(assembly.Name);

    /// <summary>
    /// The compilation every block joins: the platform and every eQuantic.UI assembly this test host
    /// carries, with the SDK's implicit usings and a using for each namespace those assemblies declare
    /// a public type in, since a snippet names the SDK's types without the usings an app has.
    /// </summary>
    private static CSharpCompilation Compile()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var references = paths.Select(path => TestReferences.Of(path)).ToList();
        var probe = CSharpCompilation.Create("WikiSnippets", [], references);

        var namespaces = probe.SourceModule.ReferencedAssemblySymbols
            .Where(assembly => IsSdk(assembly))
            .SelectMany(assembly => Namespaces(assembly.GlobalNamespace))
            .Where(ns => ns.GetTypeMembers().Any(member => member.DeclaredAccessibility == Accessibility.Public))
            .Select(ns => ns.ToDisplayString())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        var usings = CSharpSyntaxTree.ParseText(
            SdkImplicitUsings.Text + "\n" + string.Concat(namespaces.Select(ns => $"global using {ns};\n")),
            Parse, "WikiUsings.g.cs");

        return CSharpCompilation.Create("WikiSnippets", [usings], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private static string RepositoryRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        return here?.FullName ?? throw new InvalidOperationException($"no repository above {AppContext.BaseDirectory}");
    }

    private static IEnumerable<INamespaceSymbol> Namespaces(INamespaceSymbol root) =>
        root.GetNamespaceMembers().SelectMany(child => Namespaces(child).Prepend(child));
}
