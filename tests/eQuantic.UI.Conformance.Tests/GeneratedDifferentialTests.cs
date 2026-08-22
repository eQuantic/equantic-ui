using System.Linq;
using System.Text;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Fuzzlyn-style differential coverage: small GENERATED C# programs in the supported subset,
/// executed on .NET (Roslyn scripting) and as transpiled JS (embedded Bun), results compared.
/// Hand-written cases can only re-confirm what someone already thought of; the generator walks
/// combinations nobody wrote down — operator interplay, control flow over accumulators, string
/// building through branches. Deterministic by construction (own xorshift PRNG — System.Random's
/// sequence is not contractual across .NET versions), so a failure names its seed and program,
/// reproduces anywhere, and its minimized form belongs in a permanent conformance case.
/// The grammar deliberately AVOIDS the documented divergences (default-context int overflow,
/// double formatting, zero divisors) — those are known and pinned elsewhere; this hunts unknowns.
/// </summary>
public class GeneratedDifferentialTests
{
    private const int CasesPerBatch = 30;

    [SkippableTheory]
    [InlineData(0xE0501u)]
    [InlineData(0xE0502u)]
    [InlineData(0xE0503u)]
    [InlineData(0xE0504u)]
    // Four more batches once lists and LINQ chains joined the grammar: the surface the generator
    // walks is much wider than it was, so the permanent sweep grew with it.
    [InlineData(0xE0505u)]
    [InlineData(0xE0506u)]
    [InlineData(0xE0507u)]
    [InlineData(0xE0508u)]
    public void GeneratedPrograms_MatchDotNet(uint batchSeed)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");

        var failures = new List<string>();
        for (var index = 0; index < CasesPerBatch; index++)
        {
            var program = new ProgramGenerator(batchSeed * 2654435761u + (uint)index).Generate();
            try
            {
                ConformanceRunner.AssertStatementsSameAsDotNet(program);
            }
            catch (Exception ex)
            {
                failures.Add($"seed=0x{batchSeed:X} index={index}\n  program: {program}\n  {FirstLine(ex.Message)}");
            }
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count}/{CasesPerBatch} generated programs diverged:\n" + string.Join("\n", failures));
    }

    private static string FirstLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

    /// <summary>
    /// Depth-bounded expression/statement generator over the supported subset. Locals first, then
    /// a handful of mutating statements, then a deterministic fold of every local into one value —
    /// so no generated work is unobservable.
    /// </summary>
    private sealed class ProgramGenerator
    {
        private uint _state;
        public ProgramGenerator(uint seed) => _state = seed == 0 ? 1u : seed;

        private uint Next()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        private int Pick(int exclusiveMax) => (int)(Next() % (uint)exclusiveMax);

        private readonly List<string> _ints = [];
        private readonly List<string> _bools = [];
        private readonly List<string> _strings = [];
        private readonly List<string> _lists = [];

        public string Generate()
        {
            var program = new StringBuilder();

            var intCount = 2 + Pick(2);
            for (var i = 0; i < intCount; i++)
            {
                var name = $"n{i}";
                program.Append($"var {name} = {IntExpr(2)}; ");
                _ints.Add(name);
            }

            var name0 = "f0";
            program.Append($"var {name0} = {BoolExpr(2)}; ");
            _bools.Add(name0);

            var listCount = 1 + Pick(2);
            for (var i = 0; i < listCount; i++)
            {
                var name = $"xs{i}";
                var items = 2 + Pick(4);
                var values = string.Join(", ", Enumerable.Range(0, items).Select(_ => Pick(20).ToString()));
                program.Append($"var {name} = new[] {{ {values} }}; ");
                _lists.Add(name);
            }

            var stringCount = 1 + Pick(2);
            for (var i = 0; i < stringCount; i++)
            {
                var name = $"s{i}";
                program.Append($"var {name} = {StringExpr(2)}; ");
                _strings.Add(name);
            }

            var statements = 2 + Pick(3);
            for (var i = 0; i < statements; i++) program.Append(Statement());

            // The observable fold: every local reaches the result, so nothing generated is dead.
            program.Append("var acc = 17; ");
            foreach (var n in _ints) program.Append($"acc = acc * 31 + {n}; ");
            foreach (var b in _bools) program.Append($"acc = acc * 2 + ({b} ? 1 : 0); ");
            foreach (var xs in _lists) program.Append($"acc = acc * 7 + {IntChain(xs)}; ");
            var fold = new StringBuilder("return $\"{acc}");
            foreach (var s in _strings) fold.Append($"|{{{s}}}");
            fold.Append("\";");
            program.Append(fold);

            return program.ToString();
        }

        private string Statement()
        {
            switch (Pick(8))
            {
                case 6:
                    // A chain whose result feeds an accumulator: the interplay between operators is
                    // what no hand-written case enumerates.
                    return $"{IntVar()} += {IntChain(ListVar())}; ";
                case 7:
                    return $"{StringVar()} += string.Join(\"-\", {Chain(ListVar(), 1 + Pick(2))}); ";
                case 0:
                    return $"if ({BoolExpr(2)}) {{ {IntVar()} += {IntExpr(1)}; }} else {{ {IntVar()} -= {IntExpr(1)}; }} ";
                case 1:
                    return $"for (var i = 0; i < {1 + Pick(4)}; i++) {{ {IntVar()} += i * {1 + Pick(5)}; }} ";
                case 2:
                    return $"{StringVar()} = {BoolExpr(1)} ? {StringVar()} + \"-{(char)('a' + Pick(26))}\" : {StringVar()}.ToUpper(); ";
                case 3:
                {
                    // The SAME variable tested and decremented, and a strictly positive step —
                    // anything else is an infinite loop on BOTH sides (found by this generator's
                    // very first run, against itself).
                    var loopVar = IntVar();
                    return $"while ({loopVar} > {40 + Pick(20)}) {{ {loopVar} -= {7 + Pick(9)}; }} ";
                }
                case 4:
                    return $"{IntVar()} = {BoolExpr(2)} ? {IntExpr(1)} : {IntVar()} % {2 + Pick(7)}; ";
                default:
                    return $"{BoolVar()} = !{BoolVar()} || {BoolExpr(1)}; ";
            }
        }

        private string ListVar() => _lists[Pick(_lists.Count)];

        /// <summary>
        /// A LINQ chain over a list, ending in something the fold can observe as an INT. Only
        /// operators that are total are used: an empty sequence must not throw, so Max and Min go
        /// through DefaultIfEmpty and First is always FirstOrDefault. Values stay small because
        /// Enumerable.Sum is checked in .NET and would throw on overflow rather than diverge —
        /// which would be the generator testing itself, not the compiler.
        /// </summary>
        private string IntChain(string source)
        {
            var chain = Chain(source, Pick(3));
            return Pick(8) switch
            {
                0 => $"{chain}.Count()",
                1 => $"{chain}.Sum()",
                2 => $"{chain}.DefaultIfEmpty(0).Max()",
                3 => $"{chain}.DefaultIfEmpty(0).Min()",
                4 => $"{chain}.Aggregate(7, (a, b) => a % 1000 * 3 + b)",
                5 => $"{chain}.FirstOrDefault()",
                6 => $"{chain}.ElementAtOrDefault({Pick(4)})",
                _ => $"({chain}.Any(x => x % {2 + Pick(4)} == 0) ? 1 : 0)",
            };
        }

        /// <summary>A run of sequence-to-sequence operators. Every one of them is total and keeps
        /// the element type an int, so any two compose and the result is always observable.</summary>
        private string Chain(string source, int links)
        {
            var chain = source;
            for (var i = 0; i < links; i++)
            {
                chain = Pick(10) switch
                {
                    0 => $"{chain}.Where(x => x % {2 + Pick(4)} != {Pick(2)})",
                    1 => $"{chain}.Select(x => x * {1 + Pick(3)} + {Pick(5)})",
                    2 => $"{chain}.Take({Pick(5)})",
                    3 => $"{chain}.Skip({Pick(3)})",
                    4 => $"{chain}.Distinct()",
                    5 => $"{chain}.OrderBy(x => x % {2 + Pick(5)})",
                    6 => $"{chain}.Reverse()",
                    7 => $"{chain}.TakeWhile(x => x < {5 + Pick(15)})",
                    8 => $"{chain}.Append({Pick(20)})",
                    _ => $"{chain}.Concat({ListVar()})",
                };
            }
            return chain;
        }

        private string IntVar() => _ints[Pick(_ints.Count)];
        private string BoolVar() => _bools[Pick(_bools.Count)];
        private string StringVar() => _strings[Pick(_strings.Count)];

        private string IntExpr(int depth)
        {
            if (depth <= 0 || Pick(4) == 0)
                return Pick(3) == 0 && _ints.Count > 0 ? IntVar() : Pick(20).ToString();

            return Pick(8) switch
            {
                0 => $"({IntExpr(depth - 1)} + {IntExpr(depth - 1)})",
                1 => $"({IntExpr(depth - 1)} - {IntExpr(depth - 1)})",
                2 => $"({IntExpr(depth - 1)} * {Pick(5)})",
                3 => $"({IntExpr(depth - 1)} / {2 + Pick(8)})",   // literal, never zero
                4 => $"({IntExpr(depth - 1)} % {2 + Pick(8)})",   // literal, never zero
                5 => $"Math.Max({IntExpr(depth - 1)}, {IntExpr(depth - 1)})",
                6 => $"Math.Abs({IntExpr(depth - 1)} - {Pick(30)})",
                _ => $"({BoolExpr(depth - 1)} ? {IntExpr(depth - 1)} : {IntExpr(depth - 1)})",
            };
        }

        private string BoolExpr(int depth)
        {
            if (depth <= 0 || Pick(4) == 0)
                return Pick(3) == 0 && _bools.Count > 0 ? BoolVar() : (Pick(2) == 0 ? "true" : "false");

            return Pick(6) switch
            {
                0 => $"({IntExpr(depth - 1)} < {IntExpr(depth - 1)})",
                1 => $"({IntExpr(depth - 1)} >= {IntExpr(depth - 1)})",
                2 => $"({IntExpr(depth - 1)} == {IntExpr(depth - 1)})",
                3 => $"({BoolExpr(depth - 1)} && {BoolExpr(depth - 1)})",
                4 => $"({BoolExpr(depth - 1)} || {BoolExpr(depth - 1)})",
                _ => $"!{BoolExpr(depth - 1)}",
            };
        }

        private string StringExpr(int depth)
        {
            if (depth <= 0 || Pick(3) == 0)
                return Pick(3) == 0 && _strings.Count > 0
                    ? StringVar()
                    : $"\"{(char)('a' + Pick(26))}{(char)('a' + Pick(26))}\"";

            return Pick(5) switch
            {
                0 => $"({StringExpr(depth - 1)} + {StringExpr(depth - 1)})",
                1 => $"({StringExpr(depth - 1)} + {IntExpr(depth - 1)})",
                2 => $"{StringExpr(depth - 1)}.ToUpper()",
                3 => $"({BoolExpr(depth - 1)} ? {StringExpr(depth - 1)} : {StringExpr(depth - 1)})",
                _ => $"$\"[{{{IntExpr(depth - 1)}}}]\"",
            };
        }
    }
}
