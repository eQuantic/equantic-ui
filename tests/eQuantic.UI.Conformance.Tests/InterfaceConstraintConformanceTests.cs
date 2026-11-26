using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A generic class whose type parameter is constrained to an interface, and a class implementing it, built as an app
/// builds them, one module per type, with the members read through <c>T</c> (#1878). The interface's twin is a module of
/// its own that the generic class imports for its constraint (<c>T extends IShaped</c>), and it holds a value of the
/// interface's name beside the type, so the import resolves in the bundle and in plain JavaScript alike. The JavaScript
/// the members run is the one it was: what the twin changes is what the runtime's <c>tsc</c> accepts.
/// </summary>
public class InterfaceConstraintConformanceTests
{
    private const string Source = """
        public interface IShaped
        {
            int Area { get; }
            string Describe(string unit);
        }

        public sealed class Square : IShaped
        {
            private readonly int _side;
            public Square(int side) { _side = side; }
            public int Area => _side * _side;
            public string Describe(string unit) => Area + unit;
        }

        public sealed class Measured<T> where T : IShaped
        {
            private readonly T _shape;
            public Measured(T shape) { _shape = shape; }
            public string Report() => _shape.Describe("cm") + "/" + _shape.Area;
        }
        """;

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AGenericConstrainedToAnInterface_ReadsItsMembersThroughT(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, ("report", "return new Measured<Square>(new Square(3)).Report();"));
}
