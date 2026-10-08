using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A type is imported by the symbol a name binds, however much of its namespace the C# spells (#625):
/// inside <c>Falei.Web.Chat</c>, <c>Portal.Fold.Text(n)</c> wrote <c>Fold.text(n)</c> and imported
/// nothing, so the page threw <c>Fold is not defined</c>, while <c>Fold.Text(n)</c> under a using worked.
/// A using alias named it as <c>F</c>, which nothing defines. An enum's member through its namespace is
/// its value, and imports no module. Run through the module graph an app's build writes, where each type
/// is a module of its own.
/// </summary>
public class TypeImportConformanceTests
{
    private const string Source = """
        using F = Falei.Web.Portal.Fold;

        namespace Falei.Web.Portal
        {
            public static class Fold { public static string Text(int n) => "f" + n; public static readonly int Max = 3; }
            public class Tally { public int N = 1; public static int Zero => 0; }
            public enum Mood { Calm, Loud }
        }
        namespace Falei.Web.Chat
        {
            public static class Room
            {
                public static string Partial() => Portal.Fold.Text(2) + "|" + Portal.Fold.Max + "|" + new Portal.Tally().N + "|" + Portal.Tally.Zero;
                public static string Full() => Falei.Web.Portal.Fold.Text(1) + "|" + global::Falei.Web.Portal.Tally.Zero;
                public static string Aliased() => F.Text(4) + "|" + F.Max;
                public static string Spelled() => Portal.Mood.Loud + "|" + Falei.Web.Portal.Mood.Calm;
            }
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("a static class and a class named through part of their namespace", "return Falei.Web.Chat.Room.Partial();"),
        ("a static class named through its whole namespace, and through global::", "return Falei.Web.Chat.Room.Full();"),
        ("a static class named through a using alias", "return Falei.Web.Chat.Room.Aliased();"),
        ("an enum's members named through their namespace", "return Falei.Web.Chat.Room.Spelled();"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ATypeNamedThroughItsNamespace_IsImported(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);
}
