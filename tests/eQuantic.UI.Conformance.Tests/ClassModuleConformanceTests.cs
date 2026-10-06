using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A class that declares no member is still a type the browser can name (#423): a class that takes
/// everything from its interface's defaults, an empty class over a base, an abstract class with nothing
/// in it and the class over it. Each had no module, so a module that constructed one named a class
/// nothing defined. Run through the module graph an app's build writes, so the module that constructs
/// them has to import them too.
/// </summary>
public class ClassModuleConformanceTests
{
    private const string Source = """
        public interface IGreeting { string Greet() => "hello"; }
        public class Mute : IGreeting { }

        public abstract class ChainBase { public string Say() => "chain"; }
        public class Echo : ChainBase { }

        public abstract class Message { }
        public class Ping : Message { }

        public class Marker { }

        public partial class Hollow { }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("an empty class takes its interface's default", "return ((IGreeting)new Mute()).Greet();"),
        ("an empty class over a base with a twin", "return new Echo().Say();"),
        ("an empty class over an empty abstract base", "return new Ping() is Message;"),
        ("an empty class is its own type", "object m = new Marker(); return m is Marker && !(m is Ping);"),
        ("two empty instances are two objects", "return ReferenceEquals(new Marker(), new Marker());"),
        ("a partial class declared once and empty is a class", "object h = new Hollow(); return h is Hollow && !(h is Marker);"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClassThatDeclaresNoMember_IsAModule(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);
}
