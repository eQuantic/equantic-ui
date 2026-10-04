using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Every record and every struct has a twin, whatever it declares (#428). A record that declared only
/// methods, or only computed properties, had none, so `new Animal()` threw "Animal is not defined", and
/// a record over it could not extend it.
/// </summary>
public class RecordTwinConformanceTests
{
    private const string Prelude = """
        public record Animal { public string Kind() => "animal"; }
        public record Dog : Animal;
        public record Puppy : Dog { public string Name() => "pup, " + Kind(); }
        public record Labelled { public string Label => "lbl"; public int Twice => 2 * 21; }
        public record Unit;
        public struct Tool { public int Size() => 3; }
        public record struct Gauge { public static string Unit() => "kg"; }
        """;

    [SkippableTheory]
    [InlineData("return new Animal().Kind();")]                                     // "animal"
    [InlineData("return new Dog().Kind();")]                                        // "animal"
    [InlineData("return new Dog() is Animal;")]                                     // true
    [InlineData("return new Puppy().Name();")]                                      // "pup, animal"
    [InlineData("Animal a = new Puppy(); return a is Dog;")]                        // true
    [InlineData("return new Labelled().Label + new Labelled().Twice;")]             // "lbl42"
    [InlineData("return new Unit() == new Unit();")]                                // true
    [InlineData("return new Tool().Size();")]                                       // 3
    [InlineData("return default(Tool).Size() + Gauge.Unit();")]                     // "3kg"
    public void ARecordOrAStruct_HasATwin_WhateverItDeclares(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
