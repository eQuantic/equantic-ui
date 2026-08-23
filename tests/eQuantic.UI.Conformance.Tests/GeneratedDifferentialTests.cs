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
        public readonly struct Money
        {
            public readonly int Cents;
            public Money(int cents) { Cents = cents; }
            public static Money operator +(Money a, Money b) => new Money(a.Cents + b.Cents);
            public static Money operator -(Money a, Money b) => new Money(a.Cents - b.Cents);
            public static Money operator *(Money a, int k) => new Money(a.Cents * k);
            public static Money operator -(Money a) => new Money(-a.Cents);
            public static bool operator >(Money a, Money b) => a.Cents > b.Cents;
            public static bool operator <(Money a, Money b) => a.Cents < b.Cents;
            public static implicit operator Money(int cents) => new Money(cents);
            public static explicit operator int(Money m) => m.Cents;
            public override string ToString() => $"${Cents}";
        }
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

    /// <summary>
    /// No generated program declares one name twice. Found the hard way: a StringBuilder named
    /// `sb0` shadowed the `sbyte sb0`, so the fold added the builder and the program died in the
    /// JS engine — which reads as a compiler bug and is the generator writing invalid input.
    /// Nothing else catches it cheaply: the harness only says the run failed.
    /// </summary>
    [Fact]
    public void NoGeneratedProgram_DeclaresOneNameTwice()
    {
        // A LOOP variable is scoped to its loop, so two sibling `for (var i = ...)` are legal C#
        // and not a clash — only block-level declarations can shadow each other here.
        var declaration = new System.Text.RegularExpressions.Regex(
            @"(?<!for \()(?<!foreach \()\b(?:var|int|uint|long|ulong|short|ushort|byte|sbyte|float|double|decimal|char|bool|string|Money|Suit|DateTime|TimeSpan)\s+([A-Za-z_]\w*)\s*(?:=|\()");

        var clashes = new List<string>();
        for (var index = 0; index < 400; index++)
        {
            var program = new ProgramGenerator(unchecked(0xD00Du * 2654435761u + (uint)index)).Generate();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (System.Text.RegularExpressions.Match match in declaration.Matches(program))
            {
                var name = match.Groups[1].Value;
                if (!seen.Add(name)) clashes.Add($"index={index} name={name}");
            }
        }

        Assert.True(clashes.Count == 0,
            "a generated program declares a name twice, so it tests the generator and not the "
            + "compiler: " + string.Join(", ", clashes.Take(8)));
    }

    /// <summary>
    /// The DEEP sweep, off by default because it takes minutes: hundreds of fresh seeds, run when
    /// the grammar has just been widened. The committed batches above are the permanent net and
    /// are sized for CI; this is the hunt. Run with EQ_DEEP_SWEEP=1, and give a finding a
    /// permanent conformance case of its own rather than another seed here.
    /// </summary>
    [SkippableFact]
    public void DeepSweep_FindsNothingNew()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        Skip.If(Environment.GetEnvironmentVariable("EQ_DEEP_SWEEP") != "1", "Deep sweep is opt-in.");

        var programs = int.TryParse(Environment.GetEnvironmentVariable("EQ_DEEP_SWEEP_COUNT"), out var n) ? n : 1500;
        var failures = new List<string>();
        for (var index = 0; index < programs; index++)
        {
            var seed = unchecked(0x5EEDu * 2654435761u + (uint)index * 2246822519u);
            var program = new ProgramGenerator(seed).Generate();
            try
            {
                ConformanceRunner.AssertStatementsSameAsDotNet(program, Prelude);
            }
            catch (Exception ex)
            {
                failures.Add($"index={index} seed=0x{seed:X}\n  program: {program}\n  {FirstLine(ex.Message)}");
                if (failures.Count >= 5) break;
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} divergence(s) in {programs} programs:\n"
            + string.Join("\n\n", failures));
    }

    /// <summary>
    /// What the grammar actually WRITES, asserted against the list of what it claims to cover.
    /// A generator that stopped reaching a construct looks exactly like a generator that found no
    /// bug in it: this suite returned 0 divergences over 3600 programs while its grammar had never
    /// written a long, a decimal, an enum, a nullable, a record, a dictionary or a try/catch, and
    /// widening it produced findings immediately. A clean run means nothing without this.
    /// </summary>
    [Fact]
    public void TheGrammarWrites_EveryConstructItClaimsToCover()
    {
        // 600 programs, not 200: every widening of the statement switch DILUTES each case,
        // and a rarely-drawn one then reads as absent. Raised once already when the string
        // and range cases pushed Math.Round below the noise of a 200-program corpus.
        var corpus = string.Concat(Enumerable.Range(0, 600)
            .Select(index => new ProgramGenerator(unchecked(0xC0FFEEu * 2654435761u + (uint)index)).Generate()));

        var missing = new[]
        {
            // declarations
            "byte by0", "sbyte sb0", "short sh0", "ushort us0", "uint ui0", "ulong ul0",
            "double db0", "float fl0", "long L0", "decimal m0", "char c0", "Suit u0",
            "int? q0", "DateTime d0", "TimeSpan t0", "Dictionary<string, int>",
            // the constructs that only appear when their statement case is drawn
            "(byte)(", "(sbyte)(", "(short)(", "(ushort)(",
            "u - ", "f + 0.3f", "% 512.0",
            " & ", " | ", " ^ ", "(~", " >>> ", " << ", " >> ",
            "Math.Floor(", "Math.Ceiling(", "Math.Truncate(", "Math.Round(",
            "Math.Sqrt(", "Math.Clamp(", "Math.Min(",
            ".IndexOf(", ".LastIndexOf(", ".Split(", ".Contains(",
            "[^1]", "[..1]", "[1..]", "char.ToUpperInvariant(", ".ToLowerInvariant(", ".Insert(0,",
            "new Money(", "-{0}".Replace("{0}", "mo0"), "> new Money(", "(int)(",
            "var (ta", ") = (tb", "unchecked(", "checked {", "continue outer", "break outer",
            "new HashSet<int>", ".Contains(", "StringBuilder(", ".Append(", "int Fn",
            "try {", "catch {", "switch {", "is {", "foreach (", "while (", "for (",
            "new Card(", "TryGetValue", ".Substring(", "string.Join",
        }.Where(fragment => !corpus.Contains(fragment, StringComparison.Ordinal)).ToList();

        Assert.True(missing.Count == 0,
            "the grammar no longer writes these, so any clean run is silence rather than coverage: "
            + string.Join(", ", missing));
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
        private readonly List<string> _narrow = [];
        private readonly List<string> _unsigned = [];
        private readonly List<string> _ulongs = [];
        private readonly List<string> _doubles = [];
        private readonly List<string> _floats = [];
        private readonly List<string> _monies = [];

        public string Generate()
        {
            var program = new StringBuilder();

            var intCount = 2 + Pick(2);
            for (var i = 0; i < intCount; i++)
            {
                var name = $"n{i}";
                // BOUNDED at birth. The expression is evaluated in full — that is what is under
                // test — and only then brought back into a range where the arithmetic
                // downstream cannot overflow an int. Without this the bitwise operators put
                // values at 2^31 and the fold reaches the default-context overflow, which is
                // a DOCUMENTED divergence: every program would then fail for the one reason
                // this instrument exists not to hunt.
                program.Append($"var {name} = ({IntExpr(2)}) % 100003; ");
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

            // The NARROW integers, whose whole semantics is the wrap. byte/sbyte/short/ushort
            // wrap ALWAYS — no `unchecked` needed, because the cast back to the narrow type is
            // the wrap — so unlike int and long there is no documented divergence to steer round
            // here, and the arithmetic can be pushed straight past the boundary on purpose.
            program.Append($"byte by0 = {Pick(256)}; ");
            _narrow.Add("by0");
            program.Append($"sbyte sb0 = {Pick(255) - 127}; ");
            _narrow.Add("sb0");
            program.Append($"short sh0 = {Pick(2000) - 1000}; ");
            _narrow.Add("sh0");
            program.Append($"ushort us0 = {Pick(65536)}; ");
            _narrow.Add("us0");
            // uint is in the same family — it wraps at 2^32 in the default context — but it does
            // not fit an int, so the fold takes it modulo rather than adding it.
            program.Append($"uint ui0 = {Pick(100000)}u; ");
            _unsigned.Add("ui0");
            // ulong is a BigInt on the other side, like long, and is OBSERVED as text. Its
            // arithmetic stays small: ulong is not in the always-wrap family, so overflowing it
            // in the default context is the documented limit and not what this hunts.
            program.Append($"ulong ul0 = {Pick(100000)}UL; ");
            _ulongs.Add("ul0");

            // FLOATING POINT, which the generator had never written at all. A double is IEEE754
            // on both sides and agrees bit for bit; a FLOAT does not, unless every store rounds
            // to single precision — the FloatStore rule. The factors are deliberately inexact
            // (1.1f, 0.3f) so a missing round shows up as a different number and not a last-bit
            // tie, and the values are bounded by a modulo so the observing cast stays in range.
            program.Append($"double db0 = {1 + Pick(90)}.{10 + Pick(89)}; ");
            _doubles.Add("db0");
            program.Append($"float fl0 = {1 + Pick(90)}.{10 + Pick(89)}f; ");
            _floats.Add("fl0");

            // A type with USER-DEFINED OPERATORS: the twin carries each as a static method, and
            // every site the bound tree shows an operator at has to call it. Its ToString is its
            // own, so it is observed as text as well as through its field.
            program.Append($"Money mo0 = new Money({Pick(500)}); ");
            _monies.Add("mo0");

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
            foreach (var n in _ints) program.Append($"acc = (acc * 31 + {n} % 9973) % 1000003; ");
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
            // A narrow integer promotes to int to be folded, which is what C# does at every use.
            foreach (var w in _narrow) program.Append($"acc = (acc * 23 + {w}) % 1000003; ");
            foreach (var u in _unsigned) program.Append($"acc = (acc * 29 + (int)({u} % 9973u)) % 1000003; ");
            // A double and a float are observed through a SCALED TRUNCATION, never printed: the
            // text of a floating-point number is a documented divergence and would drown this.
            // 1024 is exact in both, so the scaling adds no error of its own.
            foreach (var d in _doubles) program.Append($"acc = (acc * 31 + (int)({d} * 1024)) % 1000003; ");
            foreach (var f in _floats) program.Append($"acc = (acc * 37 + (int)({f} * 1024)) % 1000003; ");
            foreach (var mo in _monies) program.Append($"acc = (acc * 41 + (int){mo} % 9973) % 1000003; ");
            var fold = new StringBuilder("return $\"{acc}");
            foreach (var s in _strings) fold.Append($"|{{{s}}}");
            // A long and a decimal are OBSERVED as text: their runtime representations differ from
            // JavaScript's numbers, so printing them is what compares the value and not a coercion.
            foreach (var l in _longs) fold.Append($"|{{{l}}}");
            foreach (var m in _decimals) fold.Append($"|{{{m}}}");
            foreach (var u in _ulongs) fold.Append($"|{{{u}}}");
            foreach (var c in _chars) fold.Append($"|{{{c}}}");
            foreach (var u in _suits) fold.Append($"|{{{u}}}");
            // Money prints through its OWN ToString, which the twin has to carry too.
            foreach (var mo in _monies) fold.Append($"|{{{mo}}}");
            fold.Append("\";");
            program.Append(fold);

            return program.ToString();
        }

        private string Statement()
        {
            switch (Pick(40))
            {
                case 33:
                    // Every operator the struct declares, at the sites the bound tree shows them:
                    // binary, unary, the scale by an int, the implicit conversion FROM an int and
                    // the explicit one back. Bounded, because Money's arithmetic is int arithmetic
                    // and its overflow is the documented divergence, not a finding.
                    return $"{MoneyVar()} = new Money(((int)({MoneyVar()} + new Money({Pick(300)})) * {1 + Pick(4)}) % 100003); "
                           + $"if ({MoneyVar()} > new Money({Pick(400)})) {{ {MoneyVar()} = -{MoneyVar()}; }} "
                           + $"{MoneyVar()} = new Money(Math.Abs((int)({MoneyVar()} - {Pick(200)})) % 100003); ";
                case 34:
                {
                    // TUPLES: the deconstruction, the named members, and the swap that has to
                    // evaluate the right side BEFORE it assigns either name.
                    var a = $"ta{_names}";
                    var b = $"tb{_names++}";
                    return $"var ({a}, {b}) = ({IntExpr(1)} % 9973, {IntExpr(1)} % 9973); "
                           + $"({a}, {b}) = ({b}, {a}); "
                           + $"{IntVar()} = ({IntVar()} + {a} - {b}) % 100003; ";
                }
                case 35:
                    // An UNCHECKED block, where an int wraps on purpose. The rule is the
                    // compiler's (ArithmeticContext) and the grammar had never written the block.
                    return $"{IntVar()} = unchecked({IntVar()} * 1000003 + {Pick(99)}) % 100003; ";
                case 36:
                    // A CHECKED block, where the same arithmetic THROWS on both sides. Only
                    // whether the catch ran is observed, since the exception text differs.
                    return $"try {{ checked {{ var over{_names} = int.MaxValue; over{_names++} = over{_names - 1} + {1 + Pick(9)}; }} {IntVar()} += 1; }} "
                           + $"catch {{ {IntVar()} -= 1; }} ";
                case 37:
                {
                    // LABELLED break and continue, which lower 1:1 to JavaScript labels. A fresh
                    // label per statement: two of one name in a block is a compile error.
                    var label = $"outer{_names++}";
                    return $"{label}: for (var li = 0; li < 3; li++) {{ for (var lj = 0; lj < 3; lj++) "
                           + $"{{ if (lj == 2) continue {label}; if (li == 2) break {label}; {IntVar()} = ({IntVar()} + li * 10 + lj) % 100003; }} }} ";
                }
                case 38:
                {
                    // A HASHSET, whose whole point is that the duplicate does not land.
                    var set = $"hs{_names++}";
                    return $"var {set} = new HashSet<int> {{ {Pick(9)}, {Pick(9)}, {Pick(9)}, {Pick(9)} }}; "
                           + $"{IntVar()} = ({IntVar()} + {set}.Count + ({set}.Contains({Pick(9)}) ? 5 : 0)) % 100003; ";
                }
                case 39:
                {
                    // A STRINGBUILDER and a LOCAL FUNCTION, neither of which the grammar wrote.
                    // `bld`, not `sb`: `sb0` is the SBYTE. A generated name that shadows a
                    // declared one makes the fold add the wrong variable, and the program dies in
                    // the JS engine with no hint that the generator wrote it wrong.
                    var sb = $"bld{_names}";
                    var fn = $"Fn{_names++}";
                    return $"int {fn}(int v) => (v * {1 + Pick(5)} + {Pick(9)}) % 9973; "
                           + $"var {sb} = new System.Text.StringBuilder(); "
                           + $"{sb}.Append(\"{(char)('a' + Pick(26))}\").Append({fn}({IntVar()})); "
                           + $"{StringVar()} += {sb}.ToString(); ";
                }
                case 29:
                {
                    // STRING work that answers a NUMBER, which is where the two runtimes have to
                    // agree on indices rather than on text. Only the ORDINAL overloads: IndexOf
                    // and Contains over a char are ordinal by definition, while their string
                    // cousins follow the culture in .NET and would be a divergence about
                    // collation and not about translation.
                    var ch = (char)('a' + Pick(26));
                    return $"{IntVar()} = ({IntVar()} + {StringVar()}.IndexOf('{ch}') "
                           + $"+ {StringVar()}.Split('-').Length "
                           + $"+ {StringVar()}.LastIndexOf('{ch}') "
                           + $"+ ({StringVar()}.Contains('{ch}') ? 3 : 0)) % 100003; ";
                }
                case 30:
                    // INDEX FROM END and RANGE over an array. Arrays here hold at least two
                    // items, so `[^1]`, `[..1]` and `[1..]` are all in bounds — an index out of
                    // range is a documented limit and would be the instrument testing itself.
                    return $"{IntVar()} = ({IntVar()} + {ListVar()}[^1] + {ListVar()}[..1].Length "
                           + $"+ {ListVar()}[1..].Sum()) % 100003; ";
                case 31:
                    // A RANGE over a string lowers to Substring, and `[^1]` on a string is a
                    // char — so this also checks that a char CONCATENATES as text and not as its
                    // code unit, which is the direction C# and JavaScript disagree by default.
                    return $"{StringVar()} = {StringVar()}[..1] + {StringVar()}[^1] "
                           + $"+ char.ToUpperInvariant({CharVar()}); ";
                case 32:
                    return $"{StringVar()} = {StringVar()}.ToLowerInvariant().TrimEnd('.').Insert(0, \"{(char)('a' + Pick(26))}\"); ";
                case 24:
                {
                    // The WRAP itself, pushed past the boundary on purpose. The cast back to the
                    // narrow type is where C# truncates the bits, and it is the RESULT type that
                    // decides — not the operands, which promoted to int to do the arithmetic.
                    // ONE variable: the target, the cast and the operand must be the same width,
                    // or the statement wraps somewhere the test did not mean.
                    var (narrow, type) = Narrow();
                    return $"{narrow} = ({type})({narrow} * {3 + Pick(60)} + {Pick(200)}); ";
                }
                case 25:
                    // uint wraps at 2^32 with no cast and no `unchecked`, and the subtraction is
                    // the direction that wraps DOWNWARD through zero.
                    return $"{UnsignedVar()} = {UnsignedVar()} * {3 + Pick(9)}u - {Pick(50000)}u; ";
                case 26:
                    // A double: bit-identical arithmetic on both sides, bounded so the observing
                    // cast stays inside an int.
                    return $"{DoubleVar()} = ({DoubleVar()} * {1 + Pick(3)}.{10 + Pick(89)} + {Pick(9)}.{10 + Pick(89)}) % 512.0; ";
                case 28:
                {
                    // The EXACT members of the numeric BCL, composed rather than called alone —
                    // the 135 hand-written cases each exercise one, and what nobody wrote down is
                    // what happens when the result of one feeds the next. Sqrt, the roundings and
                    // the comparisons are IEEE-exact on both sides; the transcendental family is
                    // deliberately absent, because a last-bit tie there is noise and not a finding.
                    var d = DoubleVar();
                    var inner = $"{d} * {1 + Pick(4)}.{10 + Pick(89)}";
                    return Pick(7) switch
                    {
                        0 => $"{d} = Math.Floor({inner}) % 512.0; ",
                        1 => $"{d} = Math.Ceiling({inner}) % 512.0; ",
                        2 => $"{d} = Math.Truncate({inner}) % 512.0; ",
                        3 => $"{d} = Math.Round({inner}, {Pick(4)}) % 512.0; ",
                        4 => $"{d} = Math.Sqrt(Math.Abs({inner})) % 512.0; ",
                        5 => $"{d} = Math.Clamp({inner}, 1.5, 480.25); ",
                        _ => $"{d} = Math.Min(Math.Max({inner}, 2.75), 500.5); ",
                    };
                }
                case 27:
                    // A float: every STORE rounds to single precision, including the one the
                    // multiplication feeds. Inexact factors, so a missing round is visible.
                    return $"{FloatVar()} = ({FloatVar()} * 1.1f + 0.3f) % 128.0f; ";
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
                    return $"if ({BoolExpr(2)}) {{ {IntVar()} = ({IntVar()} + {IntExpr(1)}) % 100003; }} "
                           + $"else {{ {IntVar()} = ({IntVar()} - {IntExpr(1)}) % 100003; }} ";
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

        /// <summary>A narrow variable and the type it was declared with, together — the cast in a
        /// wrapping assignment has to name the SAME type, or the value wraps at the wrong width
        /// and the case tests something nobody chose.</summary>
        private (string Name, string Type) Narrow()
        {
            var name = _narrow[Pick(_narrow.Count)];
            return (name, name switch
            {
                "by0" => "byte",
                "sb0" => "sbyte",
                "sh0" => "short",
                _ => "ushort",
            });
        }

        private string MoneyVar() => _monies[Pick(_monies.Count)];

        private string UnsignedVar() => _unsigned[Pick(_unsigned.Count)];
        private string DoubleVar() => _doubles[Pick(_doubles.Count)];
        private string FloatVar() => _floats[Pick(_floats.Count)];

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

            return Pick(14) switch
            {
                0 => $"({IntExpr(depth - 1)} + {IntExpr(depth - 1)})",
                1 => $"({IntExpr(depth - 1)} - {IntExpr(depth - 1)})",
                2 => $"({IntExpr(depth - 1)} * {Pick(5)})",
                3 => $"({IntExpr(depth - 1)} / {2 + Pick(8)})",   // literal, never zero
                4 => $"({IntExpr(depth - 1)} % {2 + Pick(8)})",   // literal, never zero
                5 => $"Math.Max({IntExpr(depth - 1)}, {IntExpr(depth - 1)})",
                6 => $"Math.Abs({IntExpr(depth - 1)} - {Pick(30)})",
                // BITWISE, which the grammar had never written. JavaScript's operators coerce to
                // int32 and C#'s int IS int32, so the two agree by construction — which is exactly
                // why a disagreement here would mean the value reached the operator already wrong.
                8 => $"({IntExpr(depth - 1)} & {IntExpr(depth - 1)})",
                9 => $"({IntExpr(depth - 1)} | {Pick(1000)})",
                10 => $"({IntExpr(depth - 1)} ^ {Pick(1000)})",
                11 => $"(~{IntExpr(depth - 1)})",
                // A shift COUNT is masked to 5 bits on both sides. `>>` propagates the sign in C#
                // and in JavaScript; `>>>` is the logical one in both.
                12 => $"({IntExpr(depth - 1)} {(Pick(2) == 0 ? "<<" : ">>")} {Pick(16)})",
                13 => $"({IntExpr(depth - 1)} >>> {1 + Pick(15)})",
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
