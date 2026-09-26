namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Canonical JS paths for the runtime <c>$eq</c> namespace that the transpiler emits for .NET-compat
/// helpers. Emitting any of these requires a single import of <see cref="Import"/> from
/// <c>@equantic/runtime</c> (resolved by the page's import map) — a strategy signals that by adding
/// <see cref="Import"/> to <c>UsedHelpers</c>. One import per module instead of N loose helper imports,
/// and the <c>$eq.*</c> form can never collide with a user identifier in the generated scope.
/// </summary>
public static class Eq
{
    /// <summary>The single symbol imported from <c>@equantic/runtime</c> when any <c>$eq.*</c> is emitted.</summary>
    public const string Import = "$eq";

    /// <summary>
    /// Stamps a node with the source span that constructed it and returns the SAME node — emitted
    /// only by a design-mode compilation, so production output never mentions it. Two args: the node
    /// and its origin string (see <c>VisualNode.Origin</c>).
    /// </summary>
    public const string Origin = "$eq.origin";

    /// <summary>A resx accessor, resolved at CALL time against the active UI culture (Track L D2:
    /// rewritten, never inlined — an inlined accessor bakes the build machine's culture into the
    /// bundle). Two args: the catalog id and the resx key.</summary>
    public const string Str = "$eq.str";

    public const string Dec = "$eq.num.dec";
    /// <summary><c>decimal.Parse</c>: the text read by .NET's grammar, under the NumberStyles the
    /// call names, and rounded as .NET's parser rounds it.</summary>
    public const string DecParse = "$eq.num.decParse";
    /// <summary><c>decimal.TryParse</c>: the value, or undefined where Parse throws for the text.</summary>
    public const string DecTryParse = "$eq.num.decTryParse";
    /// <summary>A double into a decimal, to 15 digits by .NET's own steps.</summary>
    public const string DecFromDouble = "$eq.num.decFromDouble";
    /// <summary>A float into a decimal, to 7 digits by .NET's own steps.</summary>
    public const string DecFromSingle = "$eq.num.decFromSingle";
    /// <summary><c>Convert.ToDecimal</c> of a value whose type the call site cannot settle: null
    /// is 0, a string parses, a number is the double (or the single) the site names.</summary>
    public const string DecConvert = "$eq.num.decConvert";
    /// <summary>An integer type's <c>Parse</c>: the text read by .NET's grammar under the call's
    /// NumberStyles or <c>Integer</c>, held as the type named by its tag holds it, or .NET's
    /// exception. Three args: the text, the type's tag (<c>'int'</c>, <c>'ulong'</c>…), the style.</summary>
    public const string IntParse = "$eq.num.intParse";
    /// <summary>An integer type's <c>TryParse</c>: the value, or undefined where Parse throws for the text.</summary>
    public const string IntTryParse = "$eq.num.intTryParse";
    /// <summary><c>Convert.ToInt32(string)</c> and its siblings: a null text is 0, any other reads as Parse does.</summary>
    public const string IntConvert = "$eq.num.intConvert";
    /// <summary><c>double.Parse</c> and <c>float.Parse</c>: the text read by .NET's grammar under
    /// the call's NumberStyles or <c>Float | AllowThousands</c>, a float rounded once from the
    /// digits, or .NET's exception. Three args: the text, <c>'double'</c> or <c>'single'</c>, the style.</summary>
    public const string RealParse = "$eq.num.realParse";
    /// <summary><c>double.TryParse</c> and <c>float.TryParse</c>: the value, or undefined where Parse throws for the text.</summary>
    public const string RealTryParse = "$eq.num.realTryParse";
    /// <summary><c>Convert.ToDouble(string)</c> and <c>Convert.ToSingle(string)</c>: a null text is 0, any other reads as Parse does.</summary>
    public const string RealConvert = "$eq.num.realConvert";
    public const string Long = "$eq.num.long";
    /// <summary>The typed boundary: a server value (SSR state, a Server Action result) coerced
    /// ONCE to its runtime type, by the spec the compiler computed from the C# type.</summary>
    public const string Hydrate = "$eq.hydrate";
    public const string Round = "$eq.math.round";
    /// <summary>MathF.Round's arithmetic: the scaling and the dividing back in single precision.</summary>
    public const string RoundSingle = "$eq.math.roundSingle";
    /// <summary>Round's overload with a mode and no digits, which reads the mode before the value.</summary>
    public const string RoundWithMode = "$eq.math.roundWithMode";
    /// <summary>MathF.Round's overload with a mode and no digits, in single precision.</summary>
    public const string RoundSingleWithMode = "$eq.math.roundSingleWithMode";
    /// <summary>A checked arithmetic result — the value, or the OverflowException C# throws.</summary>
    public const string Checked = "$eq.num.checked";
    /// <summary>C#'s integer <c>/</c> for a width a plain number carries: the throws .NET throws for a
    /// zero divisor and for <c>int.MinValue / -1</c>.</summary>
    public const string IntDiv = "$eq.num.intDiv";
    /// <summary>C#'s integer <c>%</c>, with the same throws.</summary>
    public const string IntRem = "$eq.num.intRem";
    /// <summary>C#'s <c>/</c> for a long, with .NET's DivideByZeroException and OverflowException.</summary>
    public const string LongDiv = "$eq.num.longDiv";
    /// <summary>C#'s <c>%</c> for a long, with the same throws.</summary>
    public const string LongRem = "$eq.num.longRem";
    /// <summary>A long (a BigInt) as the single .NET's conversion answers: rounded ONCE, from all
    /// 64 bits, never through the double.</summary>
    public const string SingleFromLong = "$eq.num.singleFromLong";
    /// <summary>A float as text: the shortest decimal that reads back as the same single.</summary>
    public const string Single = "$eq.num.single";
    /// <summary>A double's text as .NET writes it: the shortest digits, in .NET's notation.</summary>
    public const string Double = "$eq.num.double";
    /// <summary><c>Convert.ToInt32(value, fromBase)</c> and its seven siblings: an integer read from
    /// text in base 2, 8, 10 or 16 as .NET reads it, a base other than 10 reading the type's bits.</summary>
    public const string FromBase = "$eq.num.fromBase";
    /// <summary><c>Convert.ToString(value, toBase)</c>: an integer written in base 2, 8, 10 or 16, a
    /// negative one as its bits in any base but 10.</summary>
    public const string ToBase = "$eq.num.toBase";
    /// <summary>Substring that refuses an out-of-range index, the way .NET does.</summary>
    public const string Substring = "$eq.text.substring";
    /// <summary><c>char.IsWhiteSpace</c> over .NET's set, which JavaScript's <c>\s</c> is not: it leaves
    /// U+0085 NEXT LINE and takes U+FEFF. The runtime's <c>utils/white-space</c> keeps the one list.</summary>
    public const string IsWhiteSpace = "$eq.text.isWhiteSpace";
    /// <summary>Whether a string holds more than white space: <c>string.IsNullOrWhiteSpace</c> is its
    /// negation, reading its argument once, and a predicate that proves a string where it answers
    /// true and nothing where it answers false, as <c>[NotNullWhen(false)]</c> does in C#.</summary>
    public const string HasNonWhiteSpace = "$eq.text.hasNonWhiteSpace";
    /// <summary><c>string.Trim()</c> of .NET's white space.</summary>
    public const string Trim = "$eq.text.trim";
    /// <summary><c>string.TrimStart()</c> of .NET's white space.</summary>
    public const string TrimStart = "$eq.text.trimStart";
    /// <summary><c>string.TrimEnd()</c> of .NET's white space.</summary>
    public const string TrimEnd = "$eq.text.trimEnd";
    /// <summary><c>string.Split()</c> with no separator: every white space character is one, and the
    /// empty entries between two of them stay.</summary>
    public const string SplitOnWhiteSpace = "$eq.text.splitOnWhiteSpace";
    /// <summary>A dictionary's indexer read, which throws for a key that is not there.</summary>
    public const string MapGet = "$eq.mapGet";
    /// <summary>A dictionary's indexer write, through its <c>set</c>, answering the value written.</summary>
    public const string MapSet = "$eq.mapSet";
    /// <summary>LINQ Zip — pairs stop with the shorter sequence.</summary>
    public const string Zip = "$eq.zip";
    /// <summary>LINQ <c>Max</c>, by the ordering of the type it answers: an empty sequence of a value
    /// type throws, a NaN is passed over, a null is skipped.</summary>
    public const string LinqMax = "$eq.linq.max";
    /// <summary>LINQ <c>Min</c>, by the ordering of the type it answers: a NaN wins.</summary>
    public const string LinqMin = "$eq.linq.min";
    /// <summary>LINQ <c>ToDictionary</c> into the runtime's dictionary class, refusing a null key and a
    /// key twice.</summary>
    public const string LinqToDictionary = "$eq.linq.toDictionary";
    /// <summary>Where each text element (an extended grapheme cluster, UAX #29) of a string begins:
    /// <c>StringInfo.ParseCombiningCharacters</c>, answered by the platform's segmenter.</summary>
    public const string TextElementStarts = "$eq.text.textElementStarts";
    /// <summary>How many UTF-16 units the text element at an index spans:
    /// <c>StringInfo.GetNextTextElementLength</c>.</summary>
    public const string NextTextElementLength = "$eq.text.nextTextElementLength";
    /// <summary>A character's general category, as its <c>UnicodeCategory</c> member crosses:
    /// <c>CharUnicodeInfo.GetUnicodeCategory</c> and <c>char.GetUnicodeCategory</c>.</summary>
    public const string UnicodeCategory = "$eq.text.unicodeCategory";
    public const string Format = "$eq.text.format";
    public const string StringFormat = "$eq.text.stringFormat";
    /// <summary><c>string.Compare</c> by a <c>StringComparison</c>: a null first, a culture comparison
    /// by the platform's collator, an ordinal one answering .NET's difference.</summary>
    public const string StringCompare = "$eq.text.compare";
    /// <summary><c>string.Compare</c> over two ranges in the current culture, clamped and checked as
    /// <c>CompareInfo</c> checks them.</summary>
    public const string StringCompareRange = "$eq.text.compareRange";
    /// <summary><c>string.Compare</c> over two ranges by a <c>StringComparison</c>, and
    /// <c>string.CompareOrdinal</c> over two ranges, whose checks run in their own order.</summary>
    public const string StringCompareRangeBy = "$eq.text.compareRangeBy";
    /// <summary><c>string.Equals(a, b, comparisonType)</c>.</summary>
    public const string StringEquals = "$eq.text.equals";
    /// <summary><c>string.Join(separator, value, startIndex, count)</c>: the range, checked.</summary>
    public const string StringJoinRange = "$eq.text.joinRange";
    /// <summary><c>string.Format(CultureInfo.InvariantCulture, …)</c>: every placeholder in the
    /// invariant culture.</summary>
    public const string StringFormatInvariant = "$eq.text.stringFormatInvariant";
    /// <summary>A float boxed for <c>string.Format</c>, with its kind: the formatter writes its own
    /// digits, not those of the double underneath.</summary>
    public const string AsSingle = "$eq.text.asSingle";
    public const string StringBuilder = "$eq.text.stringBuilder";
    public const string DateTime = "$eq.time.dateTime";
    public const string TimeSpan = "$eq.time.timeSpan";
    public const string DateTimeOffset = "$eq.time.dateTimeOffset";
    public const string ParseEnum = "$eq.enums.parse";

    /// <summary>C# multicast delegates: `+=` composes an invocation list, `-=` drops the last
    /// occurrence. JavaScript has neither, and `+=` emitted literally is string concatenation.</summary>
    public const string CombineDelegate = "$eq.delegates.combine";
    public const string RemoveDelegate = "$eq.delegates.remove";

    /// <summary>Lifted Nullable&lt;T&gt; arithmetic — <c>null</c> if either operand is null.</summary>
    public const string LiftArith = "$eq.nullable.arith";
    /// <summary>Lifted Nullable&lt;T&gt; relational — <c>false</c> if either operand is null.</summary>
    public const string LiftCmp = "$eq.nullable.cmp";
    /// <summary>A lifted Nullable&lt;T&gt; unary operator (<c>++</c>, <c>--</c>, <c>-</c>, <c>~</c>):
    /// <c>null</c> if the operand is null.</summary>
    public const string LiftUnary = "$eq.nullable.unary";

    /// <summary>C#'s non-short-circuit <c>bool | bool</c> and <c>bool &amp; bool</c>: BOTH operands
    /// evaluated, in order, and a bool answered. JavaScript's <c>|</c>/<c>&amp;</c> answer a number,
    /// and <c>||</c>/<c>&amp;&amp;</c> skip the right side — neither is the C# operator.</summary>
    public const string LogicOr = "$eq.logic.or";
    public const string LogicAnd = "$eq.logic.and";

    /// <summary>C# <c>with</c> over a runtime VALUE TYPE (TypeStyle, ColorToken) — a hand-written
    /// twin has no generated <c>with</c>, and a spread would drop its prototype and its methods.</summary>
    public const string With = "$eq.withPatch";

    /// <summary>Structural (value) equality for records/structs/tuples — backs ==, Contains, Distinct.
    /// <c>new</c> because this table names JS HELPERS, and one of them is called what
    /// <c>object</c> calls a method — hiding it is the point, and saying so is what stops the
    /// warning travelling to everyone who builds this assembly.</summary>
    public new const string Equals = "$eq.equals";

    /// <summary>Membership over a collection whose runtime shape is not knowable statically —
    /// an <c>IReadOnlyCollection&lt;T&gt;</c> is a Set as readily as an array.</summary>
    public const string Contains = "$eq.collections.contains";

    /// <summary><c>HashSet&lt;T&gt;.Add</c>, which answers whether the value was NEW — a JS
    /// <c>Set.add</c> returns the set, so the toggle idiom silently stops removing.</summary>
    public const string SetAdd = "$eq.collections.setAdd";

    /// <summary>The container, for a constructor dependency — the browser's ActivatorUtilities.</summary>
    public const string ResolveService = "$eq.services.resolve";

    /// <summary>How many a collection holds, whichever shape it turned out to be.</summary>
    public const string Count = "$eq.collections.count";

    /// <summary>Factory for a dictionary (<c>Dictionary&lt;K, V&gt;</c> and its interfaces), held by slot
    /// as .NET's is, its keys found by value when its second argument says so.</summary>
    public const string Dictionary = "$eq.collections.dictionary";

    /// <summary>Factory for a value-sorted set (<c>SortedSet&lt;T&gt;</c>).</summary>
    public const string SortedSet = "$eq.collections.sortedSet";
    /// <summary>Factory for a key-sorted dictionary (<c>SortedDictionary&lt;K, V&gt;</c>).</summary>
    public const string SortedDictionary = "$eq.collections.sortedDictionary";
    /// <summary>Factory for a key-sorted list (<c>SortedList&lt;K, V&gt;</c>).</summary>
    public const string SortedList = "$eq.collections.sortedList";
}
