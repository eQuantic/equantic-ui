using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// A type of an assembly the build has no source for, a package, has no module and no twin, so the
/// browser has no name for it (#822). A call of one of its members was refused (EQ2004), and every
/// other way of naming the class built with no diagnostic and wrote it: a static read was
/// <c>Status.label</c> and a construction <c>new Gauge('a')</c>, a ReferenceError at the page's first
/// render. Each is now the refusal a call gets, naming the member, the type and the assembly, built as
/// the SDK builds a solution that references a package (<see cref="Solution.Package"/>). What names no
/// class keeps building: a constant and an enum's member, which are their values, and <c>nameof</c>.
/// </summary>
public class APackagesTypeTests
{
    private const string Vendor =
        """
        namespace Vendor;

        public static class Status
        {
            public static string Label => "label";
            public static string Field = "field";
            public static readonly string Ready = "ready";
            public const int Answer = 42;
            public static int Count => 7;
            public static string Make() => "made";
        }

        public class Gauge
        {
            public Gauge(string name) { Name = name; }
            public Gauge() { Name = "w"; }
            public string Name { get; set; }
            public static Gauge Default { get; } = new("d");
            public class Inner { public static string Value => "inner"; }
        }

        public record Money(decimal Amount, string Currency);

        public struct Spot
        {
            public Spot(int x) { X = x; }
            public int X { get; }
        }

        public delegate int Scale(int __target);

        public class GaugeCollection<T> { }

        public interface IStatus { public static string Label => "ready"; }

        public static class Guid { public static string Empty => "e"; }

        public static class DateTime { public static string Now => "n"; }

        public enum Level { Low, High }

        public class Box<T> { public static string Empty => "empty"; }
        """;

    private static string Home(string expression, string usings = "using Vendor;", string members = "") =>
        $$"""
        using eQuantic.UI.Primitives;
        {{usings}}
        namespace App.Screens;

        public sealed class Home : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                Text({{expression}}, TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
        {{members}}
        }
        """;

    private static Solution With(string home) =>
        new Solution()
            .Package("Vendor", ("Vendor.cs", Vendor))
            .App("App", references: ["Vendor"], globalUsings: SharedLibrary.Factories, files: [("Screens/Home.cs", home)]);

    /// <summary>Each way a page names a member or a construction of a package's type, and the member the
    /// refusal names.</summary>
    [Theory]
    // A static property, a field and a read-only field, through a using and qualified.
    [InlineData("Status.Label", "using Vendor;", "'Vendor.Status.Label'")]
    [InlineData("Vendor.Status.Label", "", "'Vendor.Status.Label'")]
    [InlineData("Status.Field", "using Vendor;", "'Vendor.Status.Field'")]
    [InlineData("Status.Ready", "using Vendor;", "'Vendor.Status.Ready'")]
    [InlineData("Gauge.Default.Name", "using Vendor;", "'Vendor.Gauge.Default'")]
    // A nested type's static, and a generic type's.
    [InlineData("Gauge.Inner.Value", "using Vendor;", "'Vendor.Gauge.Inner.Value'")]
    [InlineData("Box<int>.Empty", "using Vendor;", "'Vendor.Box<int>.Empty'")]
    // Reached bare through `using static`: a read and a call.
    [InlineData("Label", "using static Vendor.Status;", "'Vendor.Status.Label'")]
    [InlineData("Make()", "using static Vendor.Status;", "'Vendor.Status.Make'")]
    // A construction: explicit, qualified, with an initializer, a record and a struct.
    [InlineData("new Gauge(\"a\").Name", "using Vendor;", "'Vendor.Gauge'")]
    [InlineData("new Vendor.Gauge(\"a\").Name", "", "'Vendor.Gauge'")]
    [InlineData("new Gauge { Name = \"x\" }.Name", "using Vendor;", "'Vendor.Gauge'")]
    [InlineData("new Money(1m, \"EUR\").Currency", "using Vendor;", "'Vendor.Money'")]
    [InlineData("new Spot(1).X.ToString()", "using Vendor;", "'Vendor.Spot'")]
    // A collection of the package's, which no spelling makes the browser's array.
    [InlineData("new GaugeCollection<int>().ToString()", "using Vendor;", "'Vendor.GaugeCollection<int>'")]
    // A static named Count, which no collection's table answers, and an interface's static (Copilot's second
    // review of #958).
    [InlineData("Status.Count.ToString()", "using Vendor;", "'Vendor.Status.Count'")]
    [InlineData("IStatus.Label", "using Vendor;", "'Vendor.IStatus.Label'")]
    // A static named like a member a shortcut answers by its name, Guid.Empty and DateTime.Now (#961).
    [InlineData("Vendor.Guid.Empty", "", "'Vendor.Guid.Empty'")]
    [InlineData("Vendor.DateTime.Now", "", "'Vendor.DateTime.Now'")]
    // A static method's group.
    [InlineData("((System.Func<string>)Status.Make)()", "using Vendor;", "'Vendor.Status.Make'")]
    public void ANameOfAPackagesType_FailsTheBuild_NamingTheMemberAndTheAssembly(string expression, string usings, string member)
    {
        using var solution = With(Home(expression, usings));
        var built = solution.Build();

        built.Failed.Should().BeTrue("the browser has no name for a package's type");
        built.ErrorsIn("App").Should().ContainSingle()
            .Which.Should().StartWith(solution.FileOf("App", "Screens/Home.cs") + "(")
            .And.Contain("error EQ2004").And.Contain(member).And.Contain("'Vendor'");
        built.Module("Home").Should().BeNull("a refused page writes no module");
    }

    /// <summary>A target-typed construction names the class as surely as one that spells it.</summary>
    [Fact]
    public void ATargetTypedConstructionOfAPackagesType_FailsTheBuild()
    {
        using var solution = With(Home("Pick(new(\"t\"))", members: "    private static string Pick(Gauge w) => w.Name;"));
        var built = solution.Build();

        built.Failed.Should().BeTrue();
        built.ErrorsIn("App").Should().ContainSingle()
            .Which.Should().Contain("error EQ2004").And.Contain("'Vendor.Gauge'").And.Contain("'Vendor'");
    }

    /// <summary>A package of eQuantic's own that is not the SDK's, the eQuantic.Core family an app may
    /// reference, has no twin either: the browser's names are decided by the assembly the SDK ships, not
    /// by a namespace that starts like the SDK's. Its read, its call and its construction were written as
    /// names, because the fences took every "eQuantic." namespace for the framework.</summary>
    [Theory]
    [InlineData("Clock.Zone", "'eQuantic.Core.Timing.Clock.Zone'")]
    [InlineData("Clock.Now()", "'eQuantic.Core.Timing.Clock.Now'")]
    [InlineData("new Clock(\"a\").Name", "'eQuantic.Core.Timing.Clock'")]
    public void ANameOfAnEQuanticPackageTheSdkDoesNotShip_FailsTheBuild(string expression, string member)
    {
        const string Core =
            """
            namespace eQuantic.Core.Timing;

            public class Clock
            {
                public Clock(string name) { Name = name; }
                public string Name { get; }
                public static string Zone => "UTC";
                public static string Now() => "now";
            }
            """;
        using var solution = new Solution()
            .Package("eQuantic.Core", ("Clock.cs", Core))
            .App("App", references: ["eQuantic.Core"], globalUsings: SharedLibrary.Factories,
                files: [("Screens/Home.cs", Home(expression, "using eQuantic.Core.Timing;"))]);
        var built = solution.Build();

        built.Failed.Should().BeTrue("the browser has no twin of a type the SDK does not ship");
        built.ErrorsIn("App").Should().ContainSingle()
            .Which.Should().Contain("error EQ2004").And.Contain(member).And.Contain("'eQuantic.Core'");
        built.Module("Home").Should().BeNull("a refused page writes no module");
    }

    /// <summary>What names no class keeps building: a constant and an enum's member are their values,
    /// and a value's name is text.</summary>
    [Theory]
    [InlineData("Status.Answer.ToString()", "String(42)")]
    [InlineData("Level.High.ToString()", "'High'")]
    [InlineData("nameof(Gauge)", "'Gauge'")]
    public void AValueAPackagesTypeHolds_Builds(string expression, string written)
    {
        using var solution = With(Home(expression));
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Home").Should().Contain(written).And.NotContain("Status.").And.NotContain("Level.");
    }

    /// <summary>A group of a package delegate's Invoke whose parameter is named like the receiver the speculated
    /// call holds: the call is bound with a receiver named apart from every parameter, so it binds the
    /// group's own method (Copilot's review of #958). The parameter hid the receiver, the call bound
    /// nothing, and the group was refused.</summary>
    [Fact]
    public void AGroupOfADelegatesInvoke_BindsWhateverItsParametersAreNamed()
    {
        using var solution = With(Home("Twice(x => x * 2)",
            members: "    private static string Twice(Scale scale) { System.Func<int, int> f = scale.Invoke; return f(3).ToString(); }"));
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        // The receiver held apart from the parameter, never the parameter called with itself.
        built.Module("Home").Should().NotContain("=> __target(__target)").And.Contain("___target(__target)");
    }

    /// <summary>A target-typed construction of a package's collection is refused as the explicit one is: the
    /// fence asks the symbol, and a type whose name holds <c>Collection&lt;</c> was let through and built as an
    /// empty array (Copilot's review of #958).</summary>
    [Fact]
    public void ATargetTypedConstructionOfAPackagesCollection_FailsTheBuild()
    {
        using var solution = With(Home("Count(new()).ToString()",
            members: "    private static int Count(GaugeCollection<int> items) => 1;"));
        var built = solution.Build();

        built.Failed.Should().BeTrue("a package's collection has no twin, whatever its name says");
        built.ErrorsIn("App").Should().ContainSingle()
            .Which.Should().Contain("error EQ2004").And.Contain("'Vendor.GaugeCollection<int>'").And.Contain("'Vendor'");
    }
}
