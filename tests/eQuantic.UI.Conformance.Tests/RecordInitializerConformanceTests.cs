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

        public record Zeroed(long Count = default, decimal Total = default);

        public struct Meter
        {
            public decimal Rate = 1.25m;
            public long Ticks { get; set; } = 10;
            public System.Collections.Generic.List<int> Marks = new();
            public Meter() { }
        }

        public record Measure(int Value);

        public record Offset(int X) : Measure(X + 1)
        {
            public int Twice = X * 2;
        }

        public record struct Reading(int Id)
        {
            public string Unit { get; init; } = "kg";
            public float Scale = 0.5f;
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
    // A positional `= default` is the type's own zero, a long's BigInt and a decimal's Decimal (#408).
    [InlineData("return (new Zeroed().Count + 1L).ToString();")]                               // "1"
    [InlineData("return (new Zeroed().Total + 1.5m).ToString();")]                             // "1.5"
    // Each construction runs the initializer again: a collection is its own, never shared.
    [InlineData("var a = new Props(); var b = new Props(); a.Tags.Add(\"x\"); return b.Tags.Count;")] // 0
    // A base clause reads the derived record's parameters, before any member of it is set.
    [InlineData("var o = new Offset(3); return o.Value + \"|\" + o.X + \"|\" + o.Twice;")]       // "4|3|6"
    // `with` copies what the construction wrote, and changes only what it names.
    [InlineData("var f = new Fields() with { N = 1 }; return f.Log + f.N;")]                   // "x1"
    public void ARecordMember_StartsAsItsDeclarationSays(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    // A struct's twin is built by the same constructor, so `new` runs its initializers, and
    // `default` runs none of them (#405 zeroes such a struct member by member).
    [InlineData("return (new Meter().Rate + 1m).ToString();")]                                 // "2.25"
    [InlineData("return (new Meter().Ticks + 1L).ToString();")]                                // "11"
    [InlineData("var m = new Meter(); m.Marks.Add(1); return m.Marks.Count;")]                 // 1
    [InlineData("var a = new Meter(); var b = new Meter(); a.Marks.Add(1); return b.Marks.Count;")] // 0
    [InlineData("return new Meter { Ticks = 2 }.Rate.ToString();")]                            // "1.25"
    [InlineData("return default(Meter).Rate + \"|\" + default(Meter).Ticks + \"|\" + (default(Meter).Marks == null);")] // "0|0|True"
    [InlineData("return new Reading(3).Unit + new Reading(3).Id;")]                            // "kg3"
    [InlineData("return (double)new Reading(1).Scale;")]                                       // 0.5
    [InlineData("return default(Reading).Unit == null;")]                                      // true
    [InlineData("var r = new Reading(2) with { Unit = \"g\" }; return r.Unit + r.Id + r.Scale;")] // "g20.5"
    public void AStructMember_StartsAsItsDeclarationSays(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
