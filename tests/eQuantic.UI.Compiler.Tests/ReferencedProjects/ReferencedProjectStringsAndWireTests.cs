using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// What crosses with a referenced project's type beside its module (#624): the strings it reads from
/// its own project's catalogue, and the shape a value of it is read back by when the server answers
/// one.
/// <para>
/// The strings are where one compilation per project could go wrong in silence. A read is written as
/// the catalogue's id and the key, and the id was the class's simple name while that was unique in
/// the compilation the file is compiled in. An app's <c>Strings</c> and a library's <c>Strings</c>
/// are each the only one in their own compilation, so both were <c>Strings</c>, and one's
/// <c>Title</c> replaced the other's in every catalog: the page showed one project's text in both
/// places. The id is the build's now, decided once over the app and every project it references.
/// </para>
/// </summary>
public sealed class ReferencedProjectStringsAndWireTests : IClassFixture<ReferencedProjectStringsAndWireTests.BuiltOnce>
{
    private readonly Built _built;

    public ReferencedProjectStringsAndWireTests(BuiltOnce once) => _built = once.Built;

    public sealed class BuiltOnce : IDisposable
    {
        private readonly Solution _solution = SharedLibrary.Solution();

        public BuiltOnce() => Built = _solution.Build();

        internal Built Built { get; }

        public void Dispose() => _solution.Dispose();
    }

    [Fact]
    public void TwoCataloguesOfOneClassName_AreReadByTwoIds()
    {
        _built.Module("Home").Should().Contain("$eq.str('App.Resources.Strings', 'Title')",
            "the page reads its own project's Title");
        _built.Module("Wordmark").Should().Contain("$eq.str('Common.Resources.Strings', 'Title')",
            "the library's component reads the library's Title")
            .And.Contain("$eq.str('Common.Resources.Strings', 'Brand')");
    }

    [Theory]
    [InlineData("neutral", "One page", "A mark", "Shared")]
    [InlineData("pt-BR", "Uma página do Brasil", "Uma marca do Brasil", "Compartilhada")]
    [InlineData("pt-PT", "Uma página de Portugal", "Uma marca de Portugal", "Partilhada")]
    public void EachCatalogHoldsBothTitles_EachItsOwnProjectsText(string culture, string appTitle, string libraryTitle, string brand)
    {
        var catalog = _built.Catalog(culture);

        catalog.Should().Contain("App.Resources.Strings/Title", appTitle);
        catalog.Should().Contain("Common.Resources.Strings/Title", libraryTitle);
        catalog.Should().Contain("Common.Resources.Strings/Brand", brand);
    }

    [Theory]
    [InlineData("neutral")]
    [InlineData("pt-BR")]
    [InlineData("pt-PT")]
    public void AReferencedProjectsCatalogue_ContributesTheKeysRead_AndNoOthers(string culture)
    {
        // The library's catalogue has a third key, which no reached type reads. A resource outside
        // the app's root used to contribute WHOLE, the rule written for the SDK's own library,
        // whose reads this build cannot see. A referenced project's reads are seen.
        _built.Catalog(culture).Keys.Should().BeEquivalentTo(
            ["App.Resources.Strings/Title", "Common.Resources.Strings/Brand", "Common.Resources.Strings/Title"]);
    }

    [Fact]
    public void AServerActionsAnswer_IsReadBackByTheLibrarysOwnTwin()
    {
        // The stub hands the answer to the record's twin, which a type read from an assembly does
        // not have: there the shape was written out member by member from metadata.
        _built.Module("Home").Should().Contain("$eq.hydrate(await getServerActionsClient().invoke('Home/LoadReceipt', []), Receipt)");
        _built.Module("Receipt").Should().Contain("static get $hydration() { return { total: Money, number: 'long', issuedAt: 'dateTimeOffset' }; }",
            "a long past 2^53 and an instant with its offset are read as what they are, and the total by the Domain record's own twin");
        _built.Module("Money").Should().Contain("amount: 'decimal'");
    }

    [Fact]
    public void ALibrarysCatalogueAlone_KeepsItsSimpleName()
    {
        // An app with no catalogue of its own: the library's Strings is the only one of the build.
        using var solution = SharedLibrary.Solution();
        solution.Write("App", "Screens/Home.cs",
            """
            using eQuantic.UI.Primitives;
            using Common;

            namespace App.Screens;

            public sealed class Home : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => new Wordmark();
            }
            """);
        File.Delete(solution.FileOf("App", "Resources/Strings.Designer.cs"));
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Wordmark").Should().Contain("$eq.str('Strings', 'Brand')");
        built.Catalog("pt-PT").Should().Contain("Strings/Brand", "Partilhada");
    }

    [Fact]
    public void AnAppsCatalogueAlone_KeepsTheIdItHad()
    {
        // A referenced project with no catalogue changes nothing for an app that has one.
        using var solution = new Solution()
            .Library("Common", files: [("Cursor.cs", SharedLibrary.Cursor)])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs",
                    """
                    using eQuantic.UI.Primitives;
                    using App.Resources;
                    using Common;

                    namespace App.Screens;

                    public sealed class Home : StatelessComponent
                    {
                        public override VisualNode Build(ComponentContext context) =>
                            Text(Strings.Title + new Cursor(1).Position, TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
                    }
                    """),
                ("Resources/Strings.Designer.cs", SharedLibrary.Designer("App.Resources", "Title")),
                ("Resources/Strings.resx", SharedLibrary.Resx(("Title", "One page"))),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Home").Should().Contain("$eq.str('Strings', 'Title')");
        built.Catalog("neutral").Should().Contain("Strings/Title", "One page");
    }
}
