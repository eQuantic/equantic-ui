using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A type that relies on a default interface member keeps it, on both sides (#414). JavaScript has
/// no interfaces, and eqc wrote no default into the twins of the types that take one:
/// <c>PlainTextLanguage</c> relies on <c>ICodeLanguage.Rules</c>, its twin had no <c>rules</c>, and
/// every plain-text <c>CodeBlock</c> threw on <c>indentWidth</c> in the browser.
/// </summary>
public class DefaultInterfaceMemberConformanceTests
{
    private const string Prelude = """
        public interface IShape
        {
            string Name { get; }
            double Area();
            int Sides => 0;
            string Describe() => Name + ":" + Area() + ":" + Sides;
            string Shout() => Describe().ToUpperInvariant();
        }

        public interface ILabelled : IShape
        {
            string IShape.Describe() => "[" + Name + "]";
        }

        public record Circle(double R) : IShape
        {
            public string Name => "circle";
            public double Area() => 3 * R * R;
        }

        public record Square(double S) : IShape
        {
            public string Name => "square";
            public double Area() => S * S;
            public int Sides => 4;
        }

        public record Tag(string Text) : ILabelled
        {
            public string Name => Text;
            public double Area() => 0;
        }

        public readonly record struct Dot(int X) : IShape
        {
            public string Name => "dot" + X;
            public double Area() => 0;
        }

        public record Plate(double S) : IShape
        {
            public string Name => "plate";
            public double Area() => S;
        }

        public record Tile(double S) : Plate(S);

        public interface ICounter
        {
            int Count { get; set; }
            int Step => 1;
            void Bump() => Count = Count + Step;
        }

        public record Tally : ICounter
        {
            public int Count { get; set; }
        }

        public interface IGreeter
        {
            string Who { get; }
            string Greet() => Wrap("hi " + Who);
            private string Wrap(string text) => "<" + text + ">";
        }

        public record Greeter(string Who) : IGreeter;

        public interface IGreet
        {
            string Hello() => "hi";
        }

        public record Nobody : IGreet;

        public interface IMark
        {
            string Mark(string suffix = "!") => "m" + suffix;
        }

        public record Marker : IMark
        {
            public string Tag(string suffix = "?") => "t" + suffix;
        }

        public interface ISized
        {
            int Width { get; set; }
            int Half { get => Width / 2; set => Width = value * 2; }
        }

        public record Panel : ISized
        {
            public int Width { get; set; }
        }
        """;

    [SkippableTheory]
    // A default property and a default method, read through the interface.
    [InlineData("IShape c = new Circle(2); return c.Describe();")]                    // "circle:12:0"
    [InlineData("IShape c = new Circle(1); return c.Sides;")]                         // 0
    // A default that calls another default, and a member the type declares over the default.
    [InlineData("IShape s = new Square(3); return s.Describe();")]                    // "square:9:4"
    [InlineData("IShape s = new Square(3); return s.Shout();")]                       // "SQUARE:9:4"
    // A derived interface's override of a base interface's member wins over the base's default.
    [InlineData("IShape t = new Tag(\"x\"); return t.Describe() + t.Shout();")]      // "[x][X]"
    // A struct takes the default as a record does.
    [InlineData("IShape d = new Dot(7); return d.Describe();")]                       // "dot7:0:0"
    // A derived record inherits what its base's twin carries.
    [InlineData("IShape t = new Tile(2); return t.Describe();")]                      // "plate:2:0"
    // A default that writes a member the type implements.
    [InlineData("ICounter k = new Tally(); k.Bump(); k.Bump(); return k.Count;")]     // 2
    // A default that calls a private member of its interface, which no type implements.
    [InlineData("IGreeter g = new Greeter(\"ana\"); return g.Greet();")]              // "<hi ana>"
    // A record with no member of its own takes every member from its interface (found in review).
    [InlineData("IGreet n = new Nobody(); return n.Hello();")]                        // "hi"
    // An optional parameter keeps its default, on a default method and on the record's own.
    [InlineData("IMark m = new Marker(); return m.Mark() + m.Mark(\"#\") + new Marker().Tag();")] // "m!m#t?"
    // A default property writes through its setter.
    [InlineData("ISized s = new Panel(); s.Half = 5; return s.Width + \"|\" + s.Half;")]   // "10|5"
    public void ADefaultInterfaceMember_ReachesTheTypesTwin(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
