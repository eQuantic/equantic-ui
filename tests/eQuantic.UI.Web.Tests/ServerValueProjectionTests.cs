using eQuantic.UI.Generators;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A value a page receives from the server's container crosses as what the browser reads of it, never
/// whole, and a use the build cannot follow fails it with EQ2114. Pinned on the manifest the generator
/// writes, with the app's own factory surface generated beside it as the SDK does, so a factory call is
/// exactly what it is in an app: bound in the compilation, invisible to the generator that follows it.
/// </summary>
public class ServerValueProjectionTests
{
    private const string Attribute = "global::eQuantic.UI.Primitives.HydratedMember";
    private const string Kind = "global::eQuantic.UI.Primitives.HydratedMemberKind";

    private const string Services = """
        public sealed class SiteIdentity
        {
            public string Authority { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public bool IsAdmin { get; set; }
            public Profile? Profile { get; set; }
            public bool IsInRole(string role) => false;
        }

        public sealed class Profile
        {
            public string Name { get; set; } = "";
            public string Secret { get; set; } = "";
        }

        public sealed class SiteOptions
        {
            public string Title { get; set; } = "";
            public string ApiKey { get; set; } = "";
        }

        public sealed class ProductRepository
        {
            public string Find(int id) => "";
        }

        public sealed class Product
        {
            public string Name { get; set; } = "";
        }

        public sealed class Catalog : System.Collections.Generic.List<Product> { }

        public sealed class UserBadge(SiteIdentity? identity) : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                new Text(identity?.DisplayName ?? "guest", TypeRole.BodyM);
        }
        """;

    private sealed record Generated(string Manifest, IReadOnlyList<Diagnostic> Reported, IReadOnlyList<Diagnostic> Errors)
    {
        public IEnumerable<Diagnostic> Escapes => Reported.Where(d => d.Id == "EQ2114");
    }

    private static Generated Run(string pages, string? another = null, MetadataReference? referenced = null)
    {
        var source = $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using eQuantic.UI.Primitives;

            namespace Shop;

            {{Services}}

            {{pages}}
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
            .Concat(referenced is null ? [] : [referenced]);
        var trees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(source, path: "Shop.cs") };
        if (another is not null) trees.Add(CSharpSyntaxTree.ParseText(another, path: "Another.cs"));
        var compilation = CSharpCompilation.Create("Shop", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(
                new AppFactorySurfaceGenerator().AsSourceGenerator(),
                new HydrationManifestGenerator().AsSourceGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var reported);
        var manifest = driver.GetRunResult().Results
            .SelectMany(result => result.GeneratedSources)
            .Where(generated => generated.HintName == "HydrationManifest.g.cs")
            .Select(generated => generated.SourceText.ToString())
            .FirstOrDefault() ?? "";
        var errors = updated.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        return new Generated(manifest, reported, errors);
    }

    private static string Projected(string component, string member, string kind, string projection) =>
        $"[assembly: {Attribute}(\"Shop.{component}\", \"Shop.{component}\", \"{member}\", {Kind}.{kind}, Projection = \"{projection}\")]";

    [Fact]
    public void ANullTest_CrossesOnlyWhetherItIsNull()
    {
        var generated = Run("""
            [Page("/login")]
            public sealed class LoginPage(SiteIdentity? identity) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text(identity is null ? "sign in" : "account", TypeRole.BodyM);
            }
            """);

        generated.Errors.Should().BeEmpty();
        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("LoginPage", "identity", "CapturedParameter", ""));
    }

    [Fact]
    public void AMemberReadThroughAField_CrossesThatMemberAlone()
    {
        var generated = Run("""
            [Page("/about")]
            public sealed class AboutPage : StatelessComponent
            {
                private readonly SiteOptions _options;

                public AboutPage(SiteOptions options) { _options = options; }

                public override VisualNode Build(ComponentContext context) => new Text(_options.Title, TypeRole.BodyM);
            }
            """);

        generated.Errors.Should().BeEmpty();
        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("AboutPage", "_options", "Field", "Title"))
            .And.NotContain("ApiKey");
    }

    [Fact]
    public void WhatAChildReads_ThroughItsFactory_JoinsThePagesProjection()
    {
        var generated = Run("""
            [Page("/account")]
            public sealed class AccountPage(SiteIdentity? identity) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => UserBadge(identity);
            }
            """);

        generated.Errors.Should().BeEmpty("the factory binds in the compilation the app builds");
        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("AccountPage", "identity", "CapturedParameter", "DisplayName"))
            .And.NotContain("\"Shop.UserBadge\"", "a component that is no page receives what its parent passes, on both sides");
    }

    [Fact]
    public void WhatAChildReads_ThroughNew_AndFromTheFieldItKeepsTheValueIn_JoinsThePagesProjection()
    {
        var generated = Run("""
            public sealed class ProfileCard : StatelessComponent
            {
                private readonly SiteIdentity _identity;

                public ProfileCard(SiteIdentity identity) { _identity = identity; }

                public override VisualNode Build(ComponentContext context) =>
                    new Text(_identity.Profile?.Name ?? "", TypeRole.BodyM);
            }

            [Page("/profile")]
            public sealed class ProfilePage(SiteIdentity identity) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => new ProfileCard(identity);
            }
            """);

        generated.Errors.Should().BeEmpty();
        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("ProfilePage", "identity", "CapturedParameter", "Profile.Name"))
            .And.NotContain("Secret");
    }

    [Fact]
    public void APatternAndAConditionalAccess_ReadWhatTheyTest()
    {
        var generated = Run("""
            [Page("/admin")]
            public sealed class AdminPage(SiteIdentity? identity) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text(identity is { IsAdmin: true } ? "admin"
                        : identity?.Profile is null ? "no profile" : "user", TypeRole.BodyM);
            }
            """);

        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("AdminPage", "identity", "CapturedParameter", "IsAdmin,Profile?"));
    }

    [Fact]
    public void ALocal_AndAMemberThatReturnsTheValue_AreFollowed()
    {
        var generated = Run("""
            [Page("/settings")]
            public sealed class SettingsPage(SiteOptions options) : StatelessComponent
            {
                private SiteOptions Options => options;

                public override VisualNode Build(ComponentContext context)
                {
                    var current = Options;
                    return new Text(current.Title, TypeRole.BodyM);
                }
            }
            """);

        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("SettingsPage", "options", "CapturedParameter", "Title"));
    }

    [Fact]
    public void ACallWithAnArgumentFromTheBrowser_FailsTheBuild_NamingThePageTheValueAndTheCall()
    {
        var generated = Run("""
            [Page("/products")]
            public sealed class ProductsPage(ProductRepository repository) : StatefulComponent
            {
                private int _selectedId;
                private string _found = "";

                private void Pick() => SetState(() => _found = repository.Find(_selectedId));

                public override VisualNode Build(ComponentContext context) => new Text(_found, TypeRole.BodyM);
            }
            """);

        var escape = generated.Escapes.Should().ContainSingle().Subject;
        escape.Severity.Should().Be(DiagnosticSeverity.Error);
        escape.GetMessage().Should().Contain("ProductsPage").And.Contain("'repository'")
            .And.Contain("repository.Find(_selectedId)").And.Contain("a method is called on it");
    }

    [Fact]
    public void AValuePassedWhereTheBuildHasNoSource_FailsTheBuild_NamingTheCall()
    {
        var generated = Run("""
            [Page("/keep")]
            public sealed class KeepPage(SiteIdentity identity) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    GC.KeepAlive(identity);
                    return new Text("kept", TypeRole.BodyM);
                }
            }
            """);

        generated.Escapes.Should().ContainSingle().Which.GetMessage()
            .Should().Contain("GC.KeepAlive(identity)").And.Contain("whose source the build does not have");
    }

    [Fact]
    public void AValueUsedOnlyOnTheServer_BuildsClean_AndNothingOfItCrosses()
    {
        var generated = Run("""
            [Page("/catalog")]
            public sealed class CatalogPage(ProductRepository repository) : StatelessComponent, IServerPrefetch
            {
                private string _first = "";

                [ServerOnly]
                public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
                {
                    _first = repository.Find(1);
                    return Task.CompletedTask;
                }

                public override VisualNode Build(ComponentContext context) => new Text(_first, TypeRole.BodyM);
            }
            """);

        generated.Errors.Should().BeEmpty();
        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain("\"_first\"").And.NotContain("\"repository\"");
    }

    [Fact]
    public void AReadWhileThePageIsConstructed_FailsTheBuild()
    {
        // The browser constructs the page with no arguments, and the server's value arrives after.
        var generated = Run("""
            [Page("/title")]
            public sealed class TitlePage : StatelessComponent
            {
                private readonly string _title;

                public TitlePage(SiteOptions options) { _title = options.Title; }

                public override VisualNode Build(ComponentContext context) => new Text(_title, TypeRole.BodyM);
            }
            """);

        generated.Escapes.Should().ContainSingle().Which.GetMessage().Should().Contain("constructed");
    }

    [Fact]
    public void AValueConvertedToText_FailsTheBuild()
    {
        var generated = Run("""
            [Page("/whoami")]
            public sealed class WhoAmIPage(SiteIdentity identity) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => new Text($"{identity}", TypeRole.BodyM);
            }
            """);

        generated.Escapes.Should().ContainSingle().Which.GetMessage().Should().Contain("converted to text");
    }

    [Fact]
    public void AValueStoredInACollection_FailsTheBuild()
    {
        var generated = Run("""
            [Page("/pair")]
            public sealed class PairPage(SiteIdentity identity) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text(new[] { identity }.Length.ToString(), TypeRole.BodyM);
            }
            """);

        generated.Escapes.Should().ContainSingle().Which.GetMessage().Should().Contain("stored in a collection");
    }

    [Fact]
    public void AValueComparedWithAnotherObject_FailsTheBuild()
    {
        var generated = Run("""
            [Page("/same")]
            public sealed class SamePage(SiteIdentity identity) : StatelessComponent
            {
                private static readonly SiteIdentity Nobody = new();

                public override VisualNode Build(ComponentContext context) =>
                    new Text(identity == Nobody ? "nobody" : "somebody", TypeRole.BodyM);
            }
            """);

        generated.Escapes.Should().ContainSingle().Which.GetMessage().Should().Contain("compared with another object");
    }

    [Fact]
    public void AValueEnumerated_FailsTheBuild()
    {
        var generated = Run("""
            [Page("/shelf")]
            public sealed class ShelfPage(Catalog catalog) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    var names = "";
                    foreach (var product in catalog) names += product.Name;
                    return new Text(names, TypeRole.BodyM);
                }
            }
            """);

        generated.Escapes.Should().ContainSingle().Which.GetMessage().Should().Contain("enumerated");
    }

    [Fact]
    public void ANullCheckWhileThePageIsConstructed_FailsTheBuild_AtTheCheck()
    {
        // The browser constructs the page with no arguments, so the check throws there before any value
        // arrives. The nameof in it reads nothing.
        var generated = Run("""
            [Page("/guarded")]
            public sealed class GuardedPage : StatelessComponent
            {
                private readonly SiteOptions _options;

                public GuardedPage(SiteOptions options)
                {
                    _options = options ?? throw new ArgumentNullException(nameof(options));
                }

                public override VisualNode Build(ComponentContext context) => new Text(_options.Title, TypeRole.BodyM);
            }
            """);

        var message = generated.Escapes.Should().ContainSingle().Subject.GetMessage();
        message.Should().Contain("checked while the page is constructed")
            .And.Contain("options ?? throw new ArgumentNullException(nameof(options))");
    }

    [Fact]
    public void AChildsNullCheck_StoresTheValue_SinceItArrivesWithTheChild()
    {
        var generated = Run("""
            public sealed class NameTag : StatelessComponent
            {
                private readonly SiteIdentity _identity;

                public NameTag(SiteIdentity identity)
                {
                    _identity = identity ?? throw new ArgumentNullException(nameof(identity));
                }

                public override VisualNode Build(ComponentContext context) => new Text(_identity.DisplayName, TypeRole.BodyM);
            }

            [Page("/tag")]
            public sealed class TagPage(SiteIdentity identity) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => new NameTag(identity);
            }
            """);

        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("TagPage", "identity", "CapturedParameter", "DisplayName"));
    }

    [Fact]
    public void AValueWalkedThroughItsOwnMembers_FailsTheBuild_InsteadOfBeingFollowedForever()
    {
        var generated = Run("""
            public sealed class Step
            {
                public Step? Next { get; set; }
                public string Name { get; set; } = "";
            }

            [Page("/walk")]
            public sealed class WalkPage : StatefulComponent
            {
                private Step _step;

                public WalkPage(Step step) { _step = step; }

                private void Advance() => SetState(() => { if (_step.Next is not null) _step = _step.Next; });

                public override VisualNode Build(ComponentContext context) => new Text(_step.Name, TypeRole.BodyM);
            }
            """);

        generated.Escapes.Should().ContainSingle().Which.GetMessage().Should().Contain("walked through a chain");
    }

    [Fact]
    public void AValueHandedToTheBasesConstructor_IsFollowedThere()
    {
        var generated = Run("""
            public abstract class SecurePage(SiteIdentity? identity) : StatelessComponent
            {
                protected string Greeting => identity is null ? "guest" : identity.DisplayName;
            }

            [Page("/secure")]
            public sealed class SecureAccountPage(SiteIdentity? identity) : SecurePage(identity)
            {
                public override VisualNode Build(ComponentContext context) => new Text(Greeting, TypeRole.BodyM);
            }
            """);

        generated.Errors.Should().BeEmpty();
        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(
            $"[assembly: {Attribute}(\"Shop.SecureAccountPage\", \"Shop.SecurePage\", \"identity\", {Kind}.CapturedParameter, Projection = \"DisplayName\")]");
    }

    [Fact]
    public void ACoalesce_ATernary_AndReferenceEquals_AreFollowed()
    {
        var generated = Run("""
            [Page("/guest")]
            public sealed class GuestPage(SiteIdentity? identity) : StatelessComponent
            {
                private static readonly SiteIdentity Guest = new() { DisplayName = "guest" };

                public override VisualNode Build(ComponentContext context)
                {
                    var who = identity ?? Guest;
                    var shown = ReferenceEquals(identity, null) ? Guest : identity;
                    return new Text($"{who.DisplayName} {shown.IsAdmin}", TypeRole.BodyM);
                }
            }
            """);

        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("GuestPage", "identity", "CapturedParameter", "DisplayName,IsAdmin"));
    }

    [Fact]
    public void AValuePassedToAServerOnlyClass_FailsTheBuild()
    {
        var generated = Run("""
            [ServerOnly]
            public static class Audit
            {
                public static string Who(SiteIdentity identity) => identity.Authority;
            }

            [Page("/audit")]
            public sealed class AuditPage(SiteIdentity identity) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => new Text(Audit.Who(identity), TypeRole.BodyM);
            }
            """);

        generated.Escapes.Should().ContainSingle().Which.GetMessage().Should().Contain("which runs on the server");
        generated.Manifest.Should().NotContain("Authority");
    }

    [Fact]
    public void AStructThatHandsOutAnObjectThroughAGetter_IsReadIntoNotWrittenWhole()
    {
        // Written whole, the struct would carry its public properties, the identity among them.
        var generated = Run("""
            public struct Summary
            {
                public string Title => "summary";
                public SiteIdentity Identity => new();
            }

            public sealed class Report
            {
                public Summary Summary { get; set; }
            }

            [Page("/report")]
            public sealed class ReportPage(Report report) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => new Text(report.Summary.Title, TypeRole.BodyM);
            }
            """);

        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("ReportPage", "report", "CapturedParameter", "Summary.Title"));
    }

    [Fact]
    public void AReferencedStructThatKeepsAnObjectInAPrivateField_IsReadIntoNotWrittenWhole()
    {
        // A struct from metadata shows no private field, so only its public getter says what it holds.
        var library = CSharpCompilation.Create("Sessions",
            [CSharpSyntaxTree.ParseText("""
                namespace Sessions;

                public sealed class Account
                {
                    public string Secret { get; set; } = "";
                }

                public readonly struct Session
                {
                    private readonly Account _account;
                    public Session(Account account) { _account = account; }
                    public Account Account => _account;
                    public string Title => "session";
                }

                public sealed class SessionStore
                {
                    public Session Current { get; set; }
                }
                """)],
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var image = new MemoryStream();
        library.Emit(image).Success.Should().BeTrue();

        var generated = Run("""
            [Page("/session")]
            public sealed class SessionPage(Sessions.SessionStore store) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => new Text(store.Current.Title, TypeRole.BodyM);
            }
            """, referenced: MetadataReference.CreateFromImage(image.ToArray()));

        generated.Errors.Should().BeEmpty();
        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("SessionPage", "store", "CapturedParameter", "Current.Title"));
    }

    [Theory]
    [InlineData("identity is SiteIdentity")]
    [InlineData("identity is SiteIdentity found && found.IsAdmin")]
    [InlineData("identity is SiteIdentity { IsAdmin: true }")]
    public void ATestForAType_FailsTheBuild_SinceWhatCrossesIsAPlainCopy(string test)
    {
        // The twin tests a type with instanceof, which a projection, built as no class, never passes.
        var generated = Run($$"""
            [Page("/typed")]
            public sealed class TypedPage(SiteIdentity? identity) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => new Text({{test}} ? "a" : "b", TypeRole.BodyM);
            }
            """);

        generated.Escapes.Should().ContainSingle().Which.GetMessage().Should().Contain("tested for a type");
    }

    [Fact]
    public void AnEmptyPropertyPattern_AndAVarPattern_AskOnlyWhetherItIsThere()
    {
        var generated = Run("""
            [Page("/there")]
            public sealed class TherePage(SiteIdentity? identity) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text(identity is { } ? "here" : identity is var none && none is null ? "none" : "?", TypeRole.BodyM);
            }
            """);

        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("TherePage", "identity", "CapturedParameter", ""));
    }

    private const string Report = """
        public struct Totals
        {
            public long Seed { get; set; }
            public long Count => Seed * 6;
        }

        public struct Position
        {
            public int X;
        }

        public sealed class Report
        {
            public Totals Totals { get; set; }
            public Totals? Maybe { get; set; }
            public Position Position { get; set; }
            public byte[] Bytes { get; set; } = [];
            public System.Collections.Generic.List<string> Tags { get; set; } = new();
        }
        """;

    [Fact]
    public void AStructIsReadDownToItsScalars_SinceWhatTheSerializerWritesOfOneIsNotWhatItHolds()
    {
        // Whole, the computed long would cross untyped and the public field not at all: each read
        // reaches a scalar instead, coerced by its own type.
        var generated = Run(Report + """

            [Page("/report")]
            public sealed class ReportPage(Report report) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text($"{report.Totals.Count * 2} {report.Position.X}", TypeRole.BodyM);
            }
            """);

        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("ReportPage", "report", "CapturedParameter", "Position.X,Totals.Count"));
    }

    [Fact]
    public void AByteArray_NeitherCrossesWholeNorHasItsLengthRead()
    {
        // Whole, the bytes crossed as base64 text, whose length is not theirs; and an array's Length is
        // a .NET member, which the browser reads from its own array.
        var generated = Run(Report + """

            [Page("/bytes")]
            public sealed class BytesPage(Report report) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text($"{report.Bytes.Length}", TypeRole.BodyM);
            }
            """);

        generated.Escapes.Should().ContainSingle().Which.GetMessage().Should().Contain("a member of a .NET type");
    }

    [Fact]
    public void AValueCrossingWhole_CarriesWhatAPatternReadsBeneathIt()
    {
        // Tags crosses whole for the index, and `{ Tags.Count: > 0 }` meets the same leaf first: Roslyn
        // shapes it as `{ Tags: { Count: > 0 } }`. A deeper path would replace the list with its count.
        var generated = Run(Report + """

            [Page("/tags")]
            public sealed class TagsPage(Report report) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text(report is { Tags.Count: > 0 } ? report.Tags[0] : "", TypeRole.BodyM);
            }
            """);

        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("TagsPage", "report", "CapturedParameter", "Tags"));
    }

    [Fact]
    public void ANullableStructsValue_IsTheStructItself_AndHasValueAsksWhetherItIsThere()
    {
        var generated = Run(Report + """

            [Page("/maybe")]
            public sealed class MaybePage(Report report) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text(report.Maybe.HasValue ? $"{report.Maybe.Value.Count}" : "none", TypeRole.BodyM);
            }
            """);

        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("MaybePage", "report", "CapturedParameter", "Maybe.Count"));
    }

    [Fact]
    public void ANullableStructMatchedByAPattern_IsReadAsTheStruct()
    {
        // A pattern over a nullable reads the struct's own members: C# refuses `{ Maybe.Value.Count: … }`.
        var generated = Run(Report + """

            [Page("/matched")]
            public sealed class MatchedPage(Report report) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text(report is { Maybe.Count: > 0 } || report.Maybe is { Seed: > 0 } ? "some" : "none", TypeRole.BodyM);
            }
            """);

        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("MatchedPage", "report", "CapturedParameter", "Maybe.Count,Maybe.Seed"));
    }

    private const string Shelf = """
        public sealed class Shelf
        {
            public System.Collections.Generic.KeyValuePair<string, long> Pair { get; set; }
            public System.Collections.Generic.HashSet<string> Roles { get; set; } = new();
            public System.Collections.Generic.List<Product> Items { get; set; } = new();
            public System.Collections.Generic.Dictionary<string, long> Prices { get; set; } = new();
            public System.Collections.Generic.IReadOnlyList<long> Scores { get; set; } = [];
        }
        """;

    [Fact]
    public void APairIsNoLeaf_AndReadingItsValueFailsTheBuild()
    {
        // Whole, the pair's long crossed as the string no spec coerces, into BigInt arithmetic.
        var generated = Run(Shelf + """

            [Page("/pair")]
            public sealed class PairPage(Shelf shelf) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text($"{shelf.Pair.Value * 2}", TypeRole.BodyM);
            }
            """);

        generated.Errors.Should().BeEmpty();
        generated.Escapes.Should().ContainSingle().Which.GetMessage().Should().Contain("a member of a .NET type");
    }

    [Fact]
    public void ASetOfScalars_CrossesWhole_AndItsCodeReadsItAsASet()
    {
        // The boundary rebuilds the browser's Set from the array the server writes (#516), so the set
        // crosses whole as a list does, and its Count and Contains run in the browser.
        var generated = Run(Shelf + """

            [Page("/roles")]
            public sealed class RolesPage(Shelf shelf) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text(shelf.Roles.Count > 0 && shelf.Roles.Contains("admin") ? "admin" : "user", TypeRole.BodyM);
            }
            """);

        generated.Errors.Should().BeEmpty();
        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("RolesPage", "shelf", "CapturedParameter", "Roles"));
    }

    [Fact]
    public void TheCountOfAListOfObjects_FailsTheBuild_ReadOrMatched()
    {
        // Projected, it crossed as `{ count: 3 }` into a twin that reads an array's `length`.
        var generated = Run(Shelf + """

            [Page("/items")]
            public sealed class ItemsPage(Shelf shelf) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text(shelf.Items.Count > 0 || shelf is { Items.Count: > 1 } ? "some" : "none", TypeRole.BodyM);
            }
            """);

        generated.Errors.Should().BeEmpty();
        generated.Escapes.Select(stop => stop.GetMessage()).Should().HaveCount(2)
            .And.OnlyContain(message => message.Contains("a member of a .NET type"));
    }

    [Fact]
    public void ADictionaryAndASequenceOfScalars_CrossWhole()
    {
        var generated = Run(Shelf + """

            [Page("/prices")]
            public sealed class PricesPage(Shelf shelf) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text($"{shelf.Prices["a"] * 2} {shelf.Scores.Count} {shelf.Scores[0]}", TypeRole.BodyM);
            }
            """);

        generated.Errors.Should().BeEmpty();
        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("PricesPage", "shelf", "CapturedParameter", "Prices,Scores"));
    }

    [Fact]
    public void AReadThroughABaseTheDeclaredTypeHides_FailsTheBuild()
    {
        // The page holds a BrandedOptions, so the server binds `Title` there, to the member that hides
        // the one the local's type reads.
        var generated = Run("""
            public class BaseOptions { public string Title { get; set; } = ""; }
            public sealed class BrandedOptions : BaseOptions { public new string Title { get; set; } = ""; }

            [Page("/branded")]
            public sealed class BrandedPage(BrandedOptions options) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    BaseOptions view = options;
                    return new Text(view.Title, TypeRole.BodyM);
                }
            }
            """);

        generated.Escapes.Should().ContainSingle().Which.GetMessage().Should().Contain("binds a member the declared type hides");
    }

    [Fact]
    public void AReadThroughAnInterfaceOrAnOverride_BindsTheSameMember()
    {
        var generated = Run("""
            public interface ITitled { string Title { get; } }
            public class BaseOptions : ITitled { public virtual string Title { get; set; } = ""; }
            public sealed class BrandedOptions : BaseOptions { public override string Title { get; set; } = ""; }

            [Page("/titled")]
            public sealed class TitledPage(BrandedOptions options) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    ITitled titled = options;
                    BaseOptions view = options;
                    return new Text($"{titled.Title} {view.Title}", TypeRole.BodyM);
                }
            }
            """);

        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("TitledPage", "options", "CapturedParameter", "Title"));
    }

    [Fact]
    public void AComponentThatIsNoPage_ReceivesWhatItsParentPasses_AndIsNotRefused()
    {
        var generated = Run("""
            public sealed class RoleBadge(SiteIdentity identity) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text(identity.IsInRole("admin") ? "admin" : "user", TypeRole.BodyM);
            }
            """);

        generated.Reported.Should().BeEmpty();
        generated.Manifest.Should().BeEmpty();
    }

    [Fact]
    public void APageRoutedWithMapPage_IsAPageToo()
    {
        var generated = Run("""
            public sealed class AccountView(SiteIdentity? identity) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text(identity is null ? "sign in" : "account", TypeRole.BodyM);
            }

            """,
            // The route extension as the Server declares it, which this compilation does not reference,
            // called the way an app's Program does.
            """
            using Shop;

            namespace eQuantic.UI.Server;

            public sealed class Endpoints { }

            public static class UIExtensions
            {
                public static Endpoints MapPage<TPage>(this Endpoints endpoints, string route) => endpoints;
            }

            public static class Routes
            {
                public static void Map(Endpoints endpoints) => endpoints.MapPage<AccountView>("/account");
            }
            """);

        generated.Errors.Should().BeEmpty();
        generated.Manifest.Should().Contain(Projected("AccountView", "identity", "CapturedParameter", ""));
    }

    [Fact]
    public void APrefetchingPage_SendsItsDataWhole_AndItsServerValueAsAProjection()
    {
        var generated = Run("""
            [Page("/dashboard")]
            public sealed class DashboardPage(SiteIdentity? identity) : StatelessComponent, IServerPrefetch
            {
                private long _count;

                [ServerOnly]
                public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
                {
                    _count = 3;
                    return Task.CompletedTask;
                }

                public override VisualNode Build(ComponentContext context) =>
                    new Text(identity is null ? "" : $"{_count}", TypeRole.BodyM);
            }
            """);

        generated.Errors.Should().BeEmpty();
        generated.Reported.Should().BeEmpty();
        generated.Manifest
            .Should().Contain($"[assembly: {Attribute}(\"Shop.DashboardPage\", \"Shop.DashboardPage\", \"_count\", {Kind}.Field)]")
            .And.Contain(Projected("DashboardPage", "identity", "CapturedParameter", ""))
            .And.NotContain($"\"identity\", {Kind}.CapturedParameter)]", "a server value never crosses whole");
    }
}
