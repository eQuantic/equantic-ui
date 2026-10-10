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
    /// <summary><c>bool.Parse</c>: "True" or "False" in any case, trimmed of white space and NULs, or
    /// .NET's exception (#402).</summary>
    public const string BoolParse = "$eq.bool.parse";
    /// <summary><c>bool.TryParse</c>: the value, or undefined where Parse throws for the text.</summary>
    public const string BoolTryParse = "$eq.bool.tryParse";
    /// <summary><c>Convert.ToBoolean(string)</c>: a null text is false, any other reads as Parse does.</summary>
    public const string BoolConvert = "$eq.bool.convert";
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

    /// <summary>A sequence as the array the lowered operators work on — see <c>seq</c> in utils/linq.ts.</summary>
    public const string LinqSeq = "$eq.linq.seq";

    /// <summary>A range of a string's or an array's chars, refused where it leaves its source:
    /// <c>new string(char[], int, int)</c> and <c>ToCharArray(int, int)</c>.</summary>
    public const string TextChars = "$eq.text.chars";

    /// <summary>A sequence as a <c>foreach</c> enumerates it where its static type may hide a
    /// string: a string by its code units, anything else as it is.</summary>
    public const string LinqEnumerable = "$eq.linq.enumerable";

    /// <summary>A NEW array of a sequence's elements, as <c>ToList</c> and <c>ToArray</c> make.</summary>
    public const string LinqToArray = "$eq.linq.toArray";
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
    /// <summary>The record text .NET writes (<c>Color { R = 1, G = 2, B = 3, A = 4 }</c>) for a value the
    /// browser holds as plain data, and the empty string for a null one.</summary>
    public const string RecordText = "$eq.text.record";
    /// <summary>The method group <c>value.ToString</c> of a value the browser holds as plain data: a
    /// delegate writing <see cref="RecordText"/> of the value it was made with.</summary>
    public const string RecordTextGroup = "$eq.text.recordGroup";
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
    /// <summary><c>string.Join</c> over any sequence or a params array: each value written as .NET's
    /// <c>ToString</c> writes it, by the conversion the compiler passes, and a null one as nothing.</summary>
    public const string StringJoin = "$eq.text.join";
    /// <summary>A string's own <c>StartsWith(value, comparisonType)</c>.</summary>
    public const string StringStartsWith = "$eq.text.startsWith";
    /// <summary>A string's own <c>EndsWith(value, comparisonType)</c>.</summary>
    public const string StringEndsWith = "$eq.text.endsWith";
    /// <summary>A string's own <c>IndexOf</c> by a comparison, with its start and count, and the char
    /// overload: the range checked as .NET checks it.</summary>
    public const string StringIndexOf = "$eq.text.indexOf";
    /// <summary>A string's own <c>LastIndexOf</c> by a comparison, with its start and count,
    /// normalized as .NET's <c>CompareInfo</c> normalizes them.</summary>
    public const string StringLastIndexOf = "$eq.text.lastIndexOf";
    /// <summary>A string's own <c>IndexOf(char, startIndex[, count])</c>: ordinal, the start and the
    /// count checked as .NET 10 checks a char's search.</summary>
    public const string StringIndexOfChar = "$eq.text.indexOfChar";
    /// <summary>A string's own <c>LastIndexOf(char, startIndex[, count])</c>: ordinal, -1 for an empty
    /// string, and a start that stands on a char of the string.</summary>
    public const string StringLastIndexOfChar = "$eq.text.lastIndexOfChar";
    /// <summary>A string's own <c>Contains(value, comparisonType)</c>, and the char overload.</summary>
    public const string StringContains = "$eq.text.contains";
    /// <summary>A string's own <c>Replace(oldValue, newValue[, comparisonType])</c>: the replacement
    /// is text, never a pattern.</summary>
    public const string StringReplace = "$eq.text.replace";
    /// <summary>A string's own <c>Equals(value, comparisonType)</c>: the static's rule, on an instance
    /// that must exist.</summary>
    public const string StringInstanceEquals = "$eq.text.instanceEquals";
    /// <summary>A string's own <c>CompareTo(strB)</c>: the current culture's comparison.</summary>
    public const string StringCompareTo = "$eq.text.compareTo";
    /// <summary><c>string.Format(CultureInfo.InvariantCulture, …)</c>: every placeholder in the
    /// invariant culture.</summary>
    public const string StringFormatInvariant = "$eq.text.stringFormatInvariant";
    /// <summary>A float boxed for <c>string.Format</c>, with its kind: the formatter writes its own
    /// digits, not those of the double underneath.</summary>
    public const string AsSingle = "$eq.text.asSingle";

    /// <summary>An int, a short, a byte or their unsigned twins on its way into <c>string.Format</c>, boxed
    /// with its kind (<c>'int32'</c>, <c>'int16'</c>…): it rounds a formatted half away from zero (#393),
    /// and <c>X</c> writes a negative one at its type's width (#445).</summary>
    public const string AsInteger = "$eq.text.asInteger";
    public const string StringBuilder = "$eq.text.stringBuilder";

    /// <summary><c>new CancellationTokenSource(delay?)</c> (<c>utils/cancellation.ts</c>).</summary>
    public const string CancellationSource = "$eq.cancellation.source";

    /// <summary><c>CancellationToken.None</c>, which is also <c>default(CancellationToken)</c>.</summary>
    public const string CancellationNone = "$eq.cancellation.none";

    /// <summary><c>new CancellationToken(canceled)</c>: the one cancelled token, or the one that never is.</summary>
    public const string CancellationToken = "$eq.cancellation.token";

    /// <summary><c>CancellationTokenSource.CreateLinkedTokenSource(…)</c>.</summary>
    public const string CancellationLinked = "$eq.cancellation.linked";

    /// <summary><c>default(CancellationTokenRegistration)</c>: the registration of nothing, whose token
    /// is <c>None</c>.</summary>
    public const string CancellationRegistration = "$eq.cancellation.registration";
    public const string DateTime = "$eq.time.dateTime";
    public const string TimeSpan = "$eq.time.timeSpan";
    public const string DateTimeOffset = "$eq.time.dateTimeOffset";
    /// <summary>An enum's text, as .NET writes it: a flags enum's set flags, a nullable one's null.</summary>
    public const string EnumText = "$eq.enums.text";

    /// <summary><c>Enum.Parse</c>, from the enum's shape (<c>utils/enums.ts</c>).</summary>
    public const string EnumParse = "$eq.enums.parse";

    /// <summary><c>Enum.TryParse</c>: the value, or undefined where .NET answers false.</summary>
    public const string EnumTryParse = "$eq.enums.tryParse";

    /// <summary><c>default(TEnum)</c>, as the browser holds it.</summary>
    public const string EnumZero = "$eq.enums.zero";

    /// <summary><c>Enum.GetNames</c>.</summary>
    public const string EnumNames = "$eq.enums.names";


    /// <summary><c>Enum.GetName</c>: the member's name for a value, or null.</summary>

    public const string EnumName = "$eq.enums.name";

    /// <summary>A cast from <c>object</c> to an enum: the boxed member, a boxed number's member, or
    /// .NET's refusal.</summary>
    public const string EnumUnbox = "$eq.enums.unbox";

    /// <summary><c>Enum.GetValues</c>.</summary>
    public const string EnumValues = "$eq.enums.values";

    /// <summary><c>Enum.IsDefined</c>.</summary>
    public const string EnumIsDefined = "$eq.enums.isDefined";

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

    /// <summary>A generic record or struct built as the closed type C# names (<c>new Box&lt;int&gt;(1)</c>),
    /// marked with its type arguments, which its <c>equals</c> compares through <see cref="SameClosure"/>:
    /// one twin class serves every type argument, and <c>Box&lt;int&gt;</c> equalled <c>Box&lt;double&gt;</c> (#651).</summary>
    public const string Closing = "$eq.closing";

    /// <summary>A copy of a generic record or struct, marked as the value it copies was: a struct's
    /// <c>$clone</c> and a record's <c>with</c> build it without the constructor, and an unmarked copy
    /// of a boxed <c>Pair&lt;double&gt;</c> equalled a <c>Pair&lt;int&gt;</c> (#751).</summary>
    public const string ClosingLike = "$eq.closingLike";

    /// <summary>Whether two values of one generic record or struct are of one closed type; an unmarked
    /// value (built in generic code, or rebuilt from the wire) is not taken for another.</summary>
    public const string SameClosure = "$eq.sameClosure";

    /// <summary>A twin's JSON, as System.Text.Json writes the C# value: the twin's own properties, a
    /// property's store (<c>$name</c>) written under the property's name and read through it (#591).
    /// What a twin that keeps a store answers <c>toJSON</c> with.</summary>
    public const string Json = "$eq.json";

    /// <summary>Structural (value) equality for records/structs/tuples — backs ==, Contains, Distinct.
    /// <c>new</c> because this table names JS HELPERS, and one of them is called what
    /// <c>object</c> calls a method — hiding it is the point, and saying so is what stops the
    /// warning travelling to everyone who builds this assembly.</summary>
    public new const string Equals = "$eq.equals";

    /// <summary>The method group <c>value.Equals</c> of a value the browser holds as plain data: a
    /// delegate comparing the value it was made with as <see cref="Equals"/> does.</summary>
    public const string EqualsGroup = "$eq.equalsGroup";

    /// <summary><c>GetHashCode()</c> by .NET's contract: values <see cref="Equals"/> finds equal hash
    /// equal, and a value with its own <c>getHashCode</c> answers it.</summary>
    public const string Hash = "$eq.hash.of";

    /// <summary>An instance <c>GetHashCode()</c> on a receiver that may be null: its hash, and the
    /// refusal .NET's <c>NullReferenceException</c> is where it is null.</summary>
    public const string HashInstance = "$eq.hash.instance";

    /// <summary>The method group <c>value.GetHashCode</c>: a delegate over the receiver, refused where
    /// the delegate is made when the receiver is null; a second argument of <c>true</c> hashes an
    /// array's identity.</summary>
    public const string HashGroup = "$eq.hash.group";

    /// <summary><c>HashCode.Combine(…)</c>: the values' hashes, combined in order.</summary>
    public const string HashCombine = "$eq.hash.combine";

    /// <summary><c>object.GetHashCode()</c> for a class that does not override it: its identity's.</summary>
    public const string HashIdentity = "$eq.hash.identity";

    /// <summary><c>ValueType.GetHashCode()</c>, a struct's members, which its override reaches through
    /// <c>base</c> without calling itself back.</summary>
    public const string HashFields = "$eq.hash.fields";

    /// <summary><c>Guid.Parse</c> and <c>new Guid(string)</c>: the canonical text, or .NET's refusal.</summary>
    public const string GuidParse = "$eq.guid.parse";

    /// <summary><c>Guid.TryParse</c>: the canonical text, or undefined where .NET answers false.</summary>
    public const string GuidTryParse = "$eq.guid.tryParse";

    /// <summary><c>new object()</c>: an identity of its own, which a plain <c>{}</c> is not here, being
    /// an anonymous type compared by its members.</summary>
    public const string NewObject = "$eq.newObject";

    /// <summary>A <c>lock</c> statement's gate, evaluated once and refused when null.</summary>
    public const string LockGate = "$eq.lockGate";

    /// <summary>Membership over a collection whose runtime shape is not knowable statically —
    /// an <c>IReadOnlyCollection&lt;T&gt;</c> is a Set as readily as an array.</summary>
    public const string Contains = "$eq.collections.contains";

    /// <summary><c>HashSet&lt;T&gt;.Add</c>, which answers whether the value was NEW — a JS
    /// <c>Set.add</c> returns the set, so the toggle idiom silently stops removing.</summary>
    public const string SetAdd = "$eq.collections.setAdd";

    /// <summary><c>new HashSet&lt;T&gt;(…)</c>: the runtime's set, its elements found by the element
    /// type's equality and held by slot as .NET's are (#438, #531). Args: the equality, then what the
    /// constructor is handed, if anything.</summary>
    public const string HashSet = "$eq.collections.hashSet";
    /// <summary>A set an initializer or a collection expression builds: made empty, each element added.
    /// Args: the elements, then the equality where it is not identity.</summary>
    public const string HashSetOf = "$eq.collections.hashSetOf";
    /// <summary>A tuple's <c>Equals</c>, generated from its element types' equalities (#425).</summary>
    public const string TupleEquality = "$eq.collections.tupleEquality";
    /// <summary>An anonymous type's <c>Equals</c>, generated from its members' equalities.</summary>
    public const string MemberEquality = "$eq.collections.memberEquality";

    /// <summary><c>List&lt;T&gt;.IndexOf</c> and its ranges, by the element type's equality (#425).</summary>
    public const string ListIndexOf = "$eq.collections.indexOf";
    /// <summary><c>List&lt;T&gt;.LastIndexOf</c> and its ranges, by the element type's equality.</summary>
    public const string ListLastIndexOf = "$eq.collections.lastIndexOf";
    /// <summary><c>Array.IndexOf</c> and its ranges, by the element type's equality.</summary>
    public const string ArrayIndexOf = "$eq.collections.arrayIndexOf";
    /// <summary><c>Array.LastIndexOf</c> and its ranges, by the element type's equality.</summary>
    public const string ArrayLastIndexOf = "$eq.collections.arrayLastIndexOf";
    /// <summary><c>List&lt;T&gt;.Find</c>: the first match, or the element type's default (#488).</summary>
    public const string ListFind = "$eq.collections.find";
    /// <summary><c>List&lt;T&gt;.FindLast</c>: the last match, or the element type's default.</summary>
    public const string ListFindLast = "$eq.collections.findLast";
    /// <summary><c>List&lt;T&gt;.FindIndex</c> over a range, checked as .NET checks it.</summary>
    public const string ListFindIndex = "$eq.collections.findIndex";
    /// <summary><c>List&lt;T&gt;.FindLastIndex</c> over a range, checked as .NET checks it.</summary>
    public const string ListFindLastIndex = "$eq.collections.findLastIndex";
    /// <summary><c>Array.Find</c>, which refuses a null array first.</summary>
    public const string ArrayFind = "$eq.collections.arrayFind";
    /// <summary><c>Array.FindLast</c>.</summary>
    public const string ArrayFindLast = "$eq.collections.arrayFindLast";
    /// <summary><c>Array.FindIndex</c> and its ranges.</summary>
    public const string ArrayFindIndex = "$eq.collections.arrayFindIndex";
    /// <summary><c>Array.FindLastIndex</c> and its ranges.</summary>
    public const string ArrayFindLastIndex = "$eq.collections.arrayFindLastIndex";
    /// <summary><c>List&lt;T&gt;.RemoveAll</c>: .NET's single pass, answering how many it removed.</summary>
    public const string ListRemoveAll = "$eq.collections.removeAll";
    /// <summary><c>List&lt;T&gt;.CopyTo(array)</c> and <c>CopyTo(array, arrayIndex)</c>, into the array handed.</summary>
    public const string ListCopyTo = "$eq.collections.copyTo";
    /// <summary><c>List&lt;T&gt;.CopyTo(index, array, arrayIndex, count)</c>.</summary>
    public const string ListCopyRangeTo = "$eq.collections.copyRangeTo";
    /// <summary>A type's default comparer for a sort or a search: its ordering and .NET's helper for it.</summary>
    public const string SortOrder = "$eq.collections.order";
    /// <summary>A <c>StringComparer</c> handed to a sort or a search, by its <c>StringComparison</c>.</summary>
    public const string StringOrder = "$eq.collections.stringOrder";
    /// <summary>An <c>IComparer&lt;T&gt;</c> value handed to a sort or a search, or the default for a null one.</summary>
    public const string ComparerOrder = "$eq.collections.comparerOrder";
    /// <summary><c>List&lt;T&gt;.Sort()</c>, <c>Sort(IComparer)</c> and <c>Sort(index, count, IComparer)</c>: .NET's introsort.</summary>
    public const string ListSort = "$eq.collections.listSort";
    /// <summary><c>List&lt;T&gt;.Sort(Comparison)</c>.</summary>
    public const string ListSortBy = "$eq.collections.listSortBy";
    /// <summary><c>Array.Sort(array)</c>, <c>(array, IComparer)</c> and <c>(array, index, length, IComparer)</c>.</summary>
    public const string ArraySort = "$eq.collections.arraySort";
    /// <summary><c>Array.Sort(array, Comparison)</c>.</summary>
    public const string ArraySortBy = "$eq.collections.arraySortBy";
    /// <summary><c>List&lt;T&gt;.BinarySearch</c>: .NET's midpoint, the complement of the insertion point for a miss.</summary>
    public const string ListBinarySearch = "$eq.collections.binarySearch";

    /// <summary><c>List&lt;T&gt;.Remove</c>: takes out the first item <c>EqualityComparer&lt;T&gt;.Default</c>
    /// finds equal to the value, and answers whether there was one (#400).</summary>
    public const string ListRemove = "$eq.collections.remove";

    /// <summary><c>EqualityComparer&lt;T&gt;.Default</c> for a type compared by reference or by its own
    /// <c>Equals</c>: identity, NaN equal to NaN, and a twin's own <c>equals</c>.</summary>
    public const string SameItem = "$eq.collections.sameItem";

    /// <summary><c>EqualityComparer&lt;T&gt;.Default</c> for a type that does not decide (<c>object</c>,
    /// an interface, a type parameter): a twin's own <c>equals</c>, a tuple's or an anonymous type's
    /// members, and identity for anything else.</summary>
    public const string SameKey = "$eq.collections.sameKey";

    /// <summary>A <c>KeyValuePair&lt;K, V&gt;</c>'s comparer, from each half's (#421).</summary>
    public const string PairComparer = "$eq.collections.pairComparer";

    /// <summary>The container, for a constructor dependency — the browser's ActivatorUtilities.</summary>
    public const string ResolveService = "$eq.services.resolve";

    /// <summary>How many a collection holds, whichever shape it turned out to be.</summary>
    public const string Count = "$eq.collections.count";

    /// <summary>Factory for a dictionary (<c>Dictionary&lt;K, V&gt;</c> and its interfaces), held by slot
    /// as .NET's is, its keys found by value when its second argument says so.</summary>
    public const string Dictionary = "$eq.collections.dictionary";

    /// <summary><c>new KeyValuePair&lt;K, V&gt;(key, value)</c> and <c>KeyValuePair.Create</c>: the pair a
    /// dictionary yields, which destructures as <c>[key, value]</c> and reads <c>.key</c> and <c>.value</c>.</summary>
    public const string Pair = "$eq.collections.pair";

    /// <summary>Factory for a value-sorted set (<c>SortedSet&lt;T&gt;</c>).</summary>
    public const string SortedSet = "$eq.collections.sortedSet";
    /// <summary>Factory for a key-sorted dictionary (<c>SortedDictionary&lt;K, V&gt;</c>).</summary>
    public const string SortedDictionary = "$eq.collections.sortedDictionary";
    /// <summary>Factory for a key-sorted list (<c>SortedList&lt;K, V&gt;</c>).</summary>
    public const string SortedList = "$eq.collections.sortedList";

    /// <summary><c>new T(message)</c> for an exception type: a JavaScript Error carrying T and every type
    /// it derives from, the most derived first, as T's symbol says. Two args: the chain, the message.</summary>
    public const string ExceptionCreate = "$eq.exceptions.create";

    /// <summary>An exception of a type the runtime throws itself, by its full name, for a lowering that
    /// throws on .NET's behalf. Two args: the type, the message.</summary>
    public const string ExceptionOf = "$eq.exceptions.of";

    /// <summary>Whether a value is of an exception type, by the type's full name: the test a typed
    /// <c>catch</c>, a type pattern and an <c>as</c> write.</summary>
    public const string ExceptionIs = "$eq.exceptions.is";

    /// <summary>A <c>throw</c> expression: throws its one argument, which is evaluated where C#
    /// evaluates the exception, in the caller's own function.</summary>
    public const string Raise = "$eq.exceptions.raise";

    /// <summary>What a <c>throw</c> statement throws when its exception may be null: the exception, or the
    /// NullReferenceException the CLR throws in its place.</summary>
    public const string Thrown = "$eq.exceptions.thrown";

    /// <summary>The TypeInitializationException a type whose static initializers or static constructor
    /// threw throws on every use from then on, the first included. Two args: the type's full name, and
    /// the exception the initializer threw, its InnerException.</summary>
    public const string TypeInitialization = "$eq.exceptions.typeInitialization";

    /// <summary>An exception filter, <c>when (…)</c>: the filter's answer, or false where it throws, as
    /// .NET answers it. One arg: the filter, as a function.</summary>
    public const string ExceptionFilter = "$eq.exceptions.filter";

    /// <summary><c>Enumerable.Range(start, count)</c>, its arguments evaluated once.</summary>
    public const string LinqRange = "$eq.linq.range";

    /// <summary><c>Enumerable.Repeat(element, count)</c>: the one element, count times.</summary>
    public const string LinqRepeat = "$eq.linq.repeat";
}
