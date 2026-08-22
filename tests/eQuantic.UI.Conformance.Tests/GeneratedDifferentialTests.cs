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

    /// <summary>
    /// Types the generated programs use. An enum is a member-NAME string at run time and a
    /// record compares by VALUE; neither can be declared inside a generated statement block, so
    /// they live here and the grammar leans on them.
    /// </summary>
    private const string Prelude = """
        public enum Suit { Clubs, Hearts, Spades }
        public sealed record Card(int Rank, Suit Suit);
        """;

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
                ConformanceRunner.AssertStatementsSameAsDotNet(program, Prelude);
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
        private readonly List<string> _longs = [];
        private readonly List<string> _decimals = [];
        private readonly List<string> _chars = [];
        private readonly List<string> _suits = [];
        private readonly List<string> _nullables = [];
        private readonly List<string> _dates = [];
        private readonly List<string> _spans = [];
        private readonly List<string> _maybeDates = [];
        private readonly List<string> _maps = [];

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

            // The COMPAT types, which JavaScript does not have and every divergence so far has
            // lived in: a long is a BigInt, a decimal is a Decimal object, a char is a 1-length
            // string that promotes to its code unit. Values stay small — the documented overflow
            // limits are pinned elsewhere and are not what this hunts.
            for (var i = 0; i < 1 + Pick(2); i++)
            {
                var name = $"L{i}";
                program.Append($"long {name} = {Pick(40)}L; ");
                _longs.Add(name);
            }
            for (var i = 0; i < 1 + Pick(2); i++)
            {
                var name = $"m{i}";
                program.Append($"decimal {name} = {Pick(100)}.{Pick(90) + 10}m; ");
                _decimals.Add(name);
            }
            var charName = "c0";
            program.Append($"char {charName} = '{(char)('a' + Pick(26))}'; ");
            _chars.Add(charName);

            // An ENUM crosses as its member NAME, and a NULLABLE has to keep "no value" apart
            // from zero — two more places the two runtimes do not agree by default.
            var suitName = "u0";
            program.Append($"Suit {suitName} = Suit.{Suits[Pick(3)]}; ");
            _suits.Add(suitName);
            var maybeName = "q0";
            program.Append($"int? {maybeName} = {(Pick(2) == 0 ? "null" : Pick(30).ToString())}; ");
            _nullables.Add(maybeName);

            // DATES are runtime CLASSES on the other side, so every operator on them is a
            // method call and every comparison had to be taught to LIFT — which is where the
            // crash of #15 lived. They are never PRINTED here: a date rendered without a
            // culture is a documented divergence, pinned elsewhere, and would drown this.
            for (var i = 0; i < 1 + Pick(2); i++)
            {
                var name = $"d{i}";
                program.Append($"DateTime {name} = new DateTime(20{10 + Pick(15)}, {1 + Pick(12)}, {1 + Pick(28)}); ");
                _dates.Add(name);
            }
            var spanName = "t0";
            program.Append($"TimeSpan {spanName} = TimeSpan.FromMinutes({1 + Pick(600)}); ");
            _spans.Add(spanName);
            var maybeDate = "nd0";
            program.Append($"DateTime? {maybeDate} = {(Pick(2) == 0 ? "null" : $"new DateTime(20{10 + Pick(15)}, {1 + Pick(12)}, {1 + Pick(28)})")}; ");
            _maybeDates.Add(maybeDate);

            // A DICTIONARY is a plain object on the other side, so its keys are strings there
            // whatever they are here, and enumerating it goes through $eq.entries.
            var mapName = "map0";
            program.Append($"var {mapName} = new Dictionary<string, int> {{ "
                + string.Join(", ", Enumerable.Range(0, 2 + Pick(3))
                    .Select(i => $"[\"k{i}\"] = {Pick(50)}"))
                + " }; ");
            _maps.Add(mapName);

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
            // The fold stays INSIDE an int. Mixing by 31 overflows in a handful of steps once
            // the grammar grew, and an int overflowing in the default context is a documented
            // divergence pinned elsewhere — reaching it here would make every program fail for
            // the one reason this generator is meant not to hunt.
            program.Append("var acc = 17; ");
            foreach (var n in _ints) program.Append($"acc = (acc * 31 + {n}) % 1000003; ");
            foreach (var b in _bools) program.Append($"acc = (acc * 2 + ({b} ? 1 : 0)) % 1000003; ");
            foreach (var xs in _lists) program.Append($"acc = (acc * 7 + {IntChain(xs)}) % 1000003; ");
            // A char folds through its CODE UNIT, the way C# promotes it.
            foreach (var c in _chars) program.Append($"acc = (acc * 5 + {c}) % 1000003; ");
            foreach (var q in _nullables) program.Append($"acc = (acc * 3 + ({q} ?? -1)) % 1000003; ");
            // A date is OBSERVED through its parts and its comparisons, never its text.
            foreach (var d in _dates)
                program.Append($"acc = (acc * 11 + {d}.Year + {d}.Month * 31 + {d}.Day + {d}.DayOfYear) % 1000003; ");
            foreach (var t in _spans)
                program.Append($"acc = (acc * 13 + (int){t}.TotalMinutes + {t}.Hours) % 1000003; ");
            foreach (var n in _maybeDates)
                program.Append($"acc = (acc * 17 + ({n}.HasValue ? {n}.Value.Day : -1)) % 1000003; ");
            foreach (var m in _maps)
                program.Append($"acc = (acc * 19 + {m}.Count + {m}.Values.Sum() + ({m}.ContainsKey(\"k1\") ? 7 : 0)) % 1000003; ");
            var fold = new StringBuilder("return $\"{acc}");
            foreach (var s in _strings) fold.Append($"|{{{s}}}");
            // A long and a decimal are OBSERVED as text: their runtime representations differ from
            // JavaScript's numbers, so printing them is what compares the value and not a coercion.
            foreach (var l in _longs) fold.Append($"|{{{l}}}");
            foreach (var m in _decimals) fold.Append($"|{{{m}}}");
            foreach (var c in _chars) fold.Append($"|{{{c}}}");
            foreach (var u in _suits) fold.Append($"|{{{u}}}");
            fold.Append("\";");
            program.Append(fold);

            return program.ToString();
        }

        private string Statement()
        {
            switch (Pick(24))
            {
                case 20:
                    return $"foreach (var pair in {MapVar()}) {{ {IntVar()} = ({IntVar()} + pair.Value + pair.Key.Length) % 9973; }} ";
                case 21:
                    return $"if ({MapVar()}.TryGetValue(\"k{Pick(4)}\", out var found{_names})) {{ {IntVar()} += found{_names++}; }} ";
                case 22:
                {
                    // A THROW that is caught: the exception TYPE and message differ across the
                    // two runtimes, so only whether the catch ran is observed.
                    // Substring and a missing dictionary key both throw on BOTH sides. An array
                    // index out of range does not — that one is a documented limit, pinned in the
                    // conversion gaps, and reaching it here would make every program fail for a
                    // reason this instrument is not hunting.
                    var thrower = Pick(2) == 0
                        ? $"var bad = {StringVar()}.Substring({20 + Pick(20)});"
                        : $"var bad = {MapVar()}[\"missing{Pick(9)}\"];";
                    return $"try {{ {thrower} {IntVar()} += 1; }} catch {{ {IntVar()} -= 1; }} ";
                }
                case 23:
                    return $"{StringVar()} = ({StringVar()} + \"{new string((char)('a' + Pick(26)), 1 + Pick(3))}\")"
                           + $".Replace(\"{(char)('a' + Pick(26))}\", \"{(char)('a' + Pick(26))}\").Trim().PadLeft({Pick(9)}, '.'); ";
                case 16:
                    return $"if ({DateVar()} < {DateVar()}.AddDays({1 + Pick(400)})) {{ {IntVar()} += 3; }} ";
                case 17:
                    return $"{IntVar()} += ({DateVar()}.AddMonths({Pick(15)}) - {DateVar()}).Days % 97; ";
                case 18:
                    // The LIFTED comparison: an absent operand ANSWERS, it does not throw.
                    return $"if ({MaybeDateVar()} == null || {MaybeDateVar()} > {DateVar()}) {{ {IntVar()} += 5; }} "
                           + $"if ({MaybeDateVar()} == {MaybeDateVar()}) {{ {IntVar()} += 1; }} ";
                case 19:
                    return $"{SpanVar()} = {SpanVar()} + TimeSpan.FromMinutes({1 + Pick(120)}); ";
                case 12:
                    // A switch EXPRESSION over an enum: member names on one side, strings on the other.
                    return $"{IntVar()} += {SuitVar()} switch {{ Suit.Clubs => 1, Suit.Hearts => 2, _ => 3 }}; ";
                case 13:
                    return $"{SuitVar()} = {BoolExpr(1)} ? Suit.{Suits[Pick(3)]} : {SuitVar()}; ";
                case 14:
                    // A record compares by VALUE, and a property pattern binds through it.
                {
                    // A fresh NAME per statement: a pattern variable is scoped to the enclosing
                    // block in C#, so two of these with one name is CS0128 — the generator
                    // failing to compile its own program, which is not what it hunts.
                    var card = $"card{_names++}";
                    return $"var {card} = new Card({Pick(10)}, {SuitVar()}); "
                           + $"if ({card} is {{ Rank: > 4 }} big{_names}) {{ {IntVar()} += big{_names}.Rank; }} "
                           + $"if ({card} == new Card({card}.Rank, {card}.Suit)) {{ {IntVar()} += 1; }} ";
                }
                case 15:
                {
                    var bound = $"got{_names++}";
                    return $"if ({NullableVar()} is int {bound}) {{ {IntVar()} += {bound}; }} else {{ {IntVar()} -= 2; }} ";
                }
                case 8:
                    return $"{LongVar()} = {LongVar()} {(Pick(2) == 0 ? "+" : "-")} {Pick(30)}L; ";
                case 9:
                    return $"{DecVar()} = {DecVar()} {(Pick(2) == 0 ? "+" : "*")} {1 + Pick(5)}.{Pick(9)}m; ";
                case 10:
                    // A char compared and promoted: both are places C# and JavaScript disagree
                    // unless the compiler says which one it means.
                    return $"if ({CharVar()} > 'm') {{ {IntVar()} += {CharVar()} - 'a'; }} else {{ {IntVar()} -= 1; }} ";
                case 11:
                    return $"{StringVar()} += {(Pick(2) == 0 ? LongVar() : DecVar())}.ToString(); ";
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

        /// <summary>Fresh names for the variables a pattern binds — see case 14.</summary>
        private int _names;

        private static readonly string[] Suits = ["Clubs", "Hearts", "Spades"];

        private string MapVar() => _maps[Pick(_maps.Count)];

        private string DateVar() => _dates[Pick(_dates.Count)];
        private string SpanVar() => _spans[Pick(_spans.Count)];
        private string MaybeDateVar() => _maybeDates[Pick(_maybeDates.Count)];

        private string SuitVar() => _suits[Pick(_suits.Count)];
        private string NullableVar() => _nullables[Pick(_nullables.Count)];

        private string LongVar() => _longs[Pick(_longs.Count)];
        private string DecVar() => _decimals[Pick(_decimals.Count)];
        private string CharVar() => _chars[Pick(_chars.Count)];

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

            return Pick(8) switch
            {
                0 => $"({StringExpr(depth - 1)} + {StringExpr(depth - 1)})",
                5 => $"{StringExpr(depth - 1)}.PadRight({1 + Pick(6)}, '-')",
                6 => $"({StringExpr(depth - 1)} + {StringExpr(depth - 1)}).Substring({Pick(2)})",
                7 => $"{StringExpr(depth - 1)}.Replace('{(char)('a' + Pick(26))}', '{(char)('a' + Pick(26))}')",
                1 => $"({StringExpr(depth - 1)} + {IntExpr(depth - 1)})",
                2 => $"{StringExpr(depth - 1)}.ToUpper()",
                3 => $"({BoolExpr(depth - 1)} ? {StringExpr(depth - 1)} : {StringExpr(depth - 1)})",
                _ => $"$\"[{{{IntExpr(depth - 1)}}}]\"",
            };
        }
    }
}
