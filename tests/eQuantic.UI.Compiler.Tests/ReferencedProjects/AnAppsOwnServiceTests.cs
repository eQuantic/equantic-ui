using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// A component that takes an app's own service through its constructor gets it in the browser as it
/// does on the server and on Photon (#1335). The app writes the ordinary .NET registration once, in
/// <c>Program.cs</c>, and eqc reads the same line for the browser: every place browser code resolves
/// the service builds the class the registration names.
/// <para>
/// Before, the browser's container held the SDK's capabilities and nothing of the app's, so the
/// Wallet's screens rendered on the server and threw at hydration with "WalletApp needs
/// IWalletLedger, and this target has none", a message pointing at a host API that did not exist.
/// </para>
/// </summary>
public sealed class AnAppsOwnServiceTests : IClassFixture<AnAppsOwnServiceTests.BuiltOnce>
{
    private const string Rates =
        """
        namespace Wallet;

        public interface IRates
        {
            decimal Of(string currency);
        }

        public sealed class Rates : IRates
        {
            public decimal Of(string currency) => currency == "EUR" ? 1m : 0.9m;
        }
        """;

    private const string Ledger =
        """
        namespace Wallet;

        public interface ILedger
        {
            decimal Balance { get; }
        }

        public sealed class Ledger(IRates rates) : ILedger
        {
            public decimal Balance => 120m * rates.Of("EUR");
        }
        """;

    /// <summary>A second class of the ledger's interface, for an app that registers two.</summary>
    private const string OtherLedger =
        """

        public sealed class OtherLedger : ILedger
        {
            public decimal Balance => 0m;
        }
        """;

    /// <summary>Server code behind an interface of the app's: it writes a file.</summary>
    private const string Mail =
        """
        namespace Wallet;

        public interface IMail
        {
            void Send(string to);
        }

        public sealed class SmtpMail : IMail
        {
            public void Send(string to) => File.AppendAllText("outbox.txt", to);
        }
        """;

    /// <summary>A component of the library that takes the service itself, placed by the page.</summary>
    private const string BalanceCard =
        """
        using eQuantic.UI.Primitives;

        namespace Wallet;

        public sealed class BalanceCard(ILedger ledger) : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                Text(ledger.Balance.ToString(), TypeRole.Title, context.Theme.TextPrimary, maxLines: 1);
        }
        """;

    private const string Home =
        """
        using eQuantic.UI.Components;
        using eQuantic.UI.Primitives;
        using Wallet;

        namespace App.Screens;

        public sealed class Home(ILedger ledger) : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                Column(gap: Space.S4, children: [
                    Text(ledger.Balance > 0 ? "in credit" : "overdrawn", TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1),
                    new BalanceCard(ledger),
                ]);
        }
        """;

    /// <summary>The registrations, as an app writes them: top-level statements in Program.cs.</summary>
    private const string Program =
        """
        using Microsoft.Extensions.DependencyInjection;
        using Wallet;

        var services = new ServiceCollection();
        services.AddSingleton<ILedger, Ledger>();
        services.AddTransient<IRates, Rates>();
        services.AddScoped<IMail, SmtpMail>();
        """;

    private static Solution Solution(string program = Program, string home = Home, string? ledger = null) =>
        new Solution()
            .Library("Wallet", globalUsings: SharedLibrary.Factories, files:
            [
                ("Rates.cs", Rates),
                ("Ledger.cs", ledger ?? Ledger),
                ("Mail.cs", Mail),
                ("BalanceCard.cs", BalanceCard),
            ])
            .App("App", references: ["Wallet"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Program.cs", program),
                ("Screens/Home.cs", home),
            ]);

    private readonly Built _built;

    public AnAppsOwnServiceTests(BuiltOnce once) => _built = once.Built;

    /// <summary>The solution built once for the class, and removed after its last test.</summary>
    public sealed class BuiltOnce : IDisposable
    {
        private readonly Solution _solution = Solution();

        public BuiltOnce() => Built = _solution.Build();

        internal Built Built { get; }

        public void Dispose() => _solution.Dispose();
    }

    [Fact]
    public void TheBuildSucceeds_AndSaysNothing()
    {
        _built.Failed.Should().BeFalse(_built.Errors);
        _built.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ThePage_BuildsTheClassTheRegistrationNames_WithItsOwnDependencies()
    {
        _built.Module("Home").Should().Contain(
            "$eq.services.resolve($eq.services.provide('ILedger', () => new Ledger("
            + "$eq.services.resolve($eq.services.provide('IRates', () => new Rates(), 'transient'))), 'singleton'))",
            "the ledger is built as the container builds it, its rates resolved the same way, each with its lifetime");
    }

    [Fact]
    public void TheImplementation_IsAModuleThePageImports()
    {
        _built.SiblingImportsOf("Home").Should().Contain(["Ledger", "Rates"]);
        _built.Module("Ledger").Should().Contain("export class Ledger");
        _built.Module("Rates").Should().Contain("export class Rates");
    }

    [Fact]
    public void EveryPlaceThatResolvesIt_CarriesTheRegistration()
    {
        // The card resolves the ledger itself, and whichever of the two constructs first registers
        // it: neither depends on the other having run.
        _built.Module("BalanceCard").Should().Contain("$eq.services.provide('ILedger', () => new Ledger(");
        _built.SiblingImportsOf("BalanceCard").Should().Contain("Ledger");
    }

    [Fact]
    public void AServerOnlyService_NoBrowserCodeResolves_IsNeverCompiled()
    {
        // SmtpMail writes a file, and compiling it for the browser would fail the build. Nothing in
        // the browser resolves IMail, so its registration never crosses.
        _built.Module("SmtpMail").Should().BeNull();
        _built.Errors.Should().NotContain("SmtpMail");
    }

    [Fact]
    public void AClassRegisteredUnderItself_IsHandedToTheConstructorThatTakesIt()
    {
        // .NET's container builds Ledger with the LedgerOptions it holds: so does the browser.
        using var solution = Solution(
            program: Program + "services.AddSingleton<LedgerOptions>();\n",
            ledger: """
                namespace Wallet;

                public interface ILedger
                {
                    decimal Balance { get; }
                }

                public sealed class LedgerOptions
                {
                    public decimal Opening { get; set; } = 120m;
                }

                public sealed class Ledger(IRates rates, LedgerOptions options) : ILedger
                {
                    public decimal Balance => options.Opening * rates.Of("EUR");
                }
                """);
        var built = solution.Build();
        built.Errors.Should().NotContain("EQ2116");
        built.Module("Home").Should().Contain(
            "$eq.services.resolve($eq.services.provide('LedgerOptions', () => new LedgerOptions(), 'singleton'))");
        built.SiblingImportsOf("Home").Should().Contain("LedgerOptions");
    }

    [Fact]
    public void TwoConstructorsTheContainerCannotChooseBetween_AreAnErrorAtTheBuild()
    {
        using var solution = Solution(ledger: """
            namespace Wallet;

            public interface ILedger
            {
                decimal Balance { get; }
            }

            public sealed class Ledger : ILedger
            {
                public Ledger(IRates rates) => Balance = rates.Of("EUR");
                public Ledger(decimal opening) => Balance = opening;
                public decimal Balance { get; }
            }
            """);
        var built = solution.Build();
        built.Errors.Should().Contain("error EQ2116").And.Contain("cannot choose between them");
    }

    [Fact]
    public void TwoInterfacesOfOneName_AreNamedAsSuch_NotAsOneRegisteredTwice()
    {
        using var solution = Solution(program: Program + """
            services.AddSingleton<Other.ILedger, Other.OtherLedger>();

            namespace Other
            {
                public interface ILedger { }
                public sealed class OtherLedger : ILedger { }
            }
            """);
        var built = solution.Build();
        built.Errors.Should().Contain("error EQ2116").And.Contain("more than one interface of that name");
    }

    [Fact]
    public void AServiceAPageOnlyMentions_BringsNoImplementation()
    {
        // Home names IMail as a parameter's type and never resolves it: SmtpMail, which writes a file,
        // must not be compiled for the browser because of a mention.
        using var solution = Solution(home: Home.Replace(
            "public override VisualNode Build",
            "private static string Who(IMail? mail) => mail is null ? \"none\" : \"some\";\n\n    public override VisualNode Build"));
        var built = solution.Build();
        built.Module("SmtpMail").Should().BeNull();
        built.Errors.Should().NotContain("SmtpMail");
    }

    [Fact]
    public void AServiceNobodyRegistered_IsAnErrorAtTheBuild_NamingTheLineToWrite()
    {
        using var solution = Solution(program: "var nothing = 0;\n");
        var built = solution.Build();

        built.Failed.Should().BeTrue();
        built.Errors.Should().Contain("error EQ2116").And.Contain("Home needs ILedger in the browser, and the app registers none")
            .And.Contain("builder.Services.AddSingleton<ILedger, TImplementation>()");
    }

    [Fact]
    public void AServiceTheComponentCanDoWithout_IsHandedOverAsItComes()
    {
        using var solution = Solution(program: "var nothing = 0;\n", home: Home.Replace("Home(ILedger ledger)", "Home(ILedger? ledger)")
            .Replace("ledger.Balance > 0", "ledger is not null").Replace("new BalanceCard(ledger),", ""));
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Home").Should().Contain("$eq.services.resolve('ILedger')");
    }

    [Fact]
    public void AFactoryRegistration_IsServerCode_AndTheBuildSaysWhatToWriteInstead()
    {
        using var solution = Solution(program: Program.Replace(
            "services.AddSingleton<ILedger, Ledger>();", "services.AddSingleton<ILedger>(provider => new Ledger(new Rates()));"));
        var built = solution.Build();

        built.Failed.Should().BeTrue();
        built.Errors.Should().Contain("error EQ2116").And.Contain("only the server can run")
            .And.Contain("Program.cs:5");
    }

    [Fact]
    public void TwoRegistrationsOfTwoClasses_AreAnErrorTheBuildCannotDecide()
    {
        using var solution = Solution(program: Program + "services.AddSingleton<ILedger, OtherLedger>();\n",
            ledger: Ledger + OtherLedger);
        var built = solution.Build();

        built.Failed.Should().BeTrue();
        built.Errors.Should().Contain("error EQ2116").And.Contain("registers it more than once");
    }

    [Fact]
    public void ATryAddBesideAnAdd_IsTheAdd()
    {
        using var solution = Solution(program: Program
            + "Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions"
            + ".TryAddSingleton<ILedger, OtherLedger>(services);\n",
            ledger: Ledger + OtherLedger);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Home").Should().Contain("$eq.services.provide('ILedger', () => new Ledger(",
            "the container ignores a TryAdd of a service already registered");
    }

    [Fact]
    public void AnImplementationThatCannotCross_FailsTheBuildWhereItIsCompiled()
    {
        using var solution = Solution(ledger: Ledger.Replace(
            "public decimal Balance => 120m * rates.Of(\"EUR\");",
            "public decimal Balance => File.Exists(\"ledger.db\") ? 120m * rates.Of(\"EUR\") : 0m;"));
        var built = solution.Build();

        built.Failed.Should().BeTrue("the class the browser would build reads a file, which the client boundary refuses");
        built.ErrorsIn("Wallet").Should().Contain(line => line.Contains("Ledger.cs") && line.Contains("error EQ21"));
    }

    [Fact]
    public void ACapabilityOfTheSdk_IsTheRuntimesToProvide()
    {
        using var solution = Solution(home: Home.Replace("Home(ILedger ledger)", "Home(ILedger ledger, IClock clock)"));
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Home").Should().Contain("$eq.services.resolve('IClock')",
            "the browser registers its own clock, and no registration of the app's is needed or read");
    }
}
