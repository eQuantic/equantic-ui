using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A record member starts as its declaration says, on both sides (#385).
/// </summary>
public class RecordInitializerConformanceTests
{
    private const string Prelude = """
        public enum Level { Low = 1, High = 2 }

        public record Fields
        {
            public string Log = "x";
            public int N = 7;
            public decimal Total = 1.50m;
            public long Big = 9007199254740993L;
            public string Quoted = "it's";
            public char Mark = 'z';
            public float Ratio = 0.1f;
            public double Scale = 2.5;
            public Level Tone = Level.High;
            public int Plain;
            public string None;
            public void Add() { Log += "b"; }
        }

        public record Mixed(int Id)
        {
            public string Label = "item";
        }

        public record Props
        {
            public decimal Price { get; init; } = 1.5m;
            public long Count { get; init; } = 5;
            public float Weight { get; init; } = 0.1f;
            public System.Collections.Generic.List<string> Tags { get; init; } = new();
            public int[] Slots { get; init; } = [1, 2];
        }

        public record Positional(decimal Price = 2.5m, long Count = 3, string Name = "n");

        public record Tagged(int Id)
        {
            public string Tag = "#" + Id;
        }

        public record Seeded
        {
            public static int Seed = 3;
            public int Value = Seed * 2;
        }
        """;

    [SkippableTheory]
    [InlineData("return new Fields().Log;")]                                                   // "x"
    [InlineData("return new Fields().N;")]                                                     // 7
    [InlineData("return (new Fields().Total + 1m).ToString();")]                               // "2.50"
    [InlineData("return (new Fields().Big + 1L).ToString();")]                                 // "9007199254740994"
    [InlineData("return new Fields().Quoted;")]                                                // "it's"
    [InlineData("return new Fields().Mark.ToString();")]                                       // "z"
    [InlineData("return (double)new Fields().Ratio;")]                                         // 0.10000000149011612
    [InlineData("return new Fields().Scale;")]                                                 // 2.5
    [InlineData("return new Fields().Tone.ToString();")]                                       // "High"
    [InlineData("return new Fields().Plain;")]                                                 // 0
    [InlineData("return new Fields().None == null;")]                                          // true
    [InlineData("var f = new Fields(); f.Add(); return f.Log;")]                               // "xb"
    [InlineData("return new Mixed(4).Label + new Mixed(4).Id;")]                               // "item4"
    [InlineData("return new Fields { N = 3 }.Log + new Fields { N = 3 }.N;")]                  // "x3"
    [InlineData("return new Fields { Plain = 1 }.Total.ToString();")]                          // "1.50"
    [InlineData("return (new Props().Price + 1m).ToString();")]                                // "2.5"
    [InlineData("return (new Props().Count + 1L).ToString();")]                                // "6"
    [InlineData("return (double)new Props().Weight;")]                                         // 0.10000000149011612
    [InlineData("var p = new Props(); p.Tags.Add(\"a\"); return p.Tags.Count;")]               // 1
    [InlineData("return new Props().Slots.Length;")]                                           // 2
    [InlineData("return (new Positional().Price + 1m).ToString();")]                           // "3.5"
    [InlineData("return (new Positional().Count + 1L).ToString();")]                           // "4"
    [InlineData("return new Positional(Name: \"m\").Price.ToString();")]                       // "2.5"
    // An initializer may read a positional parameter, and one that reads a static reads it live.
    [InlineData("return new Tagged(4).Tag;")]                                                  // "#4"
    [InlineData("return new Seeded().Value;")]                                                 // 6
    [InlineData("Seeded.Seed = 5; return new Seeded().Value;")]                                // 10
    // Each construction runs the initializer again: a collection is its own, never shared.
    [InlineData("var a = new Props(); var b = new Props(); a.Tags.Add(\"x\"); return b.Tags.Count;")] // 0
    // `with` copies what the construction wrote, and changes only what it names.
    [InlineData("var f = new Fields() with { N = 1 }; return f.Log + f.N;")]                   // "x1"
    public void ARecordMember_StartsAsItsDeclarationSays(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
