using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A twin's JSON is what the server's <c>System.Text.Json</c> writes under <c>EqJson.Options</c>: its public properties,
/// never its fields. <c>JSON.stringify</c> wrote every own key, so fields reached a server action (#745).
/// </summary>
public class TwinJsonConformanceTests
{
    private const string Source = """
        using System.Text.Json.Serialization;
        public interface ISized { int Size { get; } }
        public class WithAField { public int Total = 7; }
        public class TwoCases { public int value = 1; public int Value = 2; }
        public class FieldAndAuto { public int value = 1; public int Value { get; set; } = 2; }
        public class FieldAndComputed { public int value = 1; public int Value => value * 10; }
        public class FieldAndMethod { public int count = 3; public int Count() => count; }
        public class Explicit : ISized { int size = 4; int ISized.Size => size * 10; }
        public class PlainAuto { public int A { get; set; } = 1; public string B { get; set; } = "b"; }
        public class Hidden { public int Shown { get; set; } = 1; private int Kept { get; set; } = 2; public int Read { private get; set; } = 3; }
        public class Attributed { [JsonIgnore] public int Gone { get; set; } = 1; [JsonPropertyName("other")] public int Named { get; set; } = 2; [JsonInclude] public int Field = 3; }
        public class Conditional { private int _x = 0; public string Empty { get; set; } = null; [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Kept { get; set; } = "k"; [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public int N { get; set; } = 1; [JsonIgnore(Condition = JsonIgnoreCondition.Always)] public int Gone { get; set; } = 2; }
        public class Base { private int _state = 1; public int Inherited { get; set; } = 2; }
        public class Derived : Base { public int Own { get; set; } = 3; }
        public class Primary(int seed) { public int Twice => seed * 2; }
        public record Product(int Id, string Name) { private int _views = 1; public int Views => _views; }
        public struct Point { public int X; public int Y { get; set; } }
        """;

    [SkippableTheory]
    // The rows of #745.
    [InlineData("return new WithAField();")]
    [InlineData("return new TwoCases();")]
    [InlineData("return new FieldAndAuto();")]
    [InlineData("return new FieldAndComputed();")]
    [InlineData("return new FieldAndMethod();")]
    [InlineData("return new Explicit();")]
    // What the contract adds and takes out, a base's members, a primary constructor's parameter.
    [InlineData("return new PlainAuto();")]
    [InlineData("return new Hidden();")]
    [InlineData("return new Attributed();")]
    [InlineData("return new Conditional();")]
    [InlineData("return new Derived();")]
    [InlineData("return new Primary(2);")]
    [InlineData("return new Product(1, \"x\");")]
    [InlineData("return new Point { X = 1, Y = 2 };")]
    public void ATwinsJson_IsWhatTheServerWrites(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ModuleGraph.AssertJsonSameAsDotNet(Source, false, ("case", statements));
    }
}
