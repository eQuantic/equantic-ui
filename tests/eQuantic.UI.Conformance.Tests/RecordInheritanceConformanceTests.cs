using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for record inheritance (<c>record Dog(...) : Animal(Name)</c>) and generic records
/// (<c>record Box&lt;T&gt;(T Value)</c>) — emitted as JS classes (<c>extends</c>/super, generics erased).
/// </summary>
public class RecordInheritanceConformanceTests
{
    private const string Animals =
        "public record Animal(string Name) { public string Speak() => Name + \" makes a sound\"; } " +
        "public record Dog(string Name, string Breed) : Animal(Name) { public string Describe() => Breed + \" named \" + Name; }";

    [SkippableTheory]
    [InlineData("new Dog(\"Rex\", \"Lab\").Name")]              // "Rex" (inherited member)
    [InlineData("new Dog(\"Rex\", \"Lab\").Breed")]             // "Lab"
    [InlineData("new Dog(\"Rex\", \"Lab\").Describe()")]        // "Lab named Rex"
    [InlineData("new Dog(\"Rex\", \"Lab\").Speak()")]           // "Rex makes a sound" (inherited method)
    [InlineData("new Dog(\"Rex\", \"Lab\") == new Dog(\"Rex\", \"Lab\")")]    // true
    [InlineData("new Dog(\"Rex\", \"Lab\") == new Dog(\"Rex\", \"Poodle\")")] // false
    public void RecordInheritance_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, Animals);
    }

    private const string Boxes = "public record Box<T>(T Value) { public T Get() => Value; }";

    [SkippableTheory]
    [InlineData("new Box<int>(5).Get()")]               // 5
    [InlineData("new Box<string>(\"hi\").Value")]       // "hi"
    [InlineData("new Box<int>(5) == new Box<int>(5)")]  // true
    public void GenericRecord_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, Boxes);
    }
}
