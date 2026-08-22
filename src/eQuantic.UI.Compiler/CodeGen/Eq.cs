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
    public const string Long = "$eq.num.long";
    /// <summary>The typed boundary: a server value (SSR state, a Server Action result) coerced
    /// ONCE to its runtime type, by the spec the compiler computed from the C# type.</summary>
    public const string Hydrate = "$eq.hydrate";
    public const string Round = "$eq.math.round";
    /// <summary>A checked arithmetic result — the value, or the OverflowException C# throws.</summary>
    public const string Checked = "$eq.num.checked";
    /// <summary>A float as text: the shortest decimal that reads back as the same single.</summary>
    public const string Single = "$eq.num.single";
    /// <summary>LINQ Zip — pairs stop with the shorter sequence.</summary>
    public const string Zip = "$eq.zip";
    public const string Format = "$eq.text.format";
    public const string StringFormat = "$eq.text.stringFormat";
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

    /// <summary>C# <c>with</c> over a runtime VALUE TYPE (TypeStyle, ColorToken) — a hand-written
    /// twin has no generated <c>with</c>, and a spread would drop its prototype and its methods.</summary>
    public const string With = "$eq.withPatch";

    /// <summary>Structural (value) equality for records/structs/tuples — backs ==, Contains, Distinct.</summary>
    public const string Equals = "$eq.equals";

    /// <summary>Dictionary enumeration (foreach / List copy): destructurable pairs with .key/.value.</summary>
    public const string Entries = "$eq.entries";

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

    /// <summary>Factory for a structurally-keyed dictionary (<c>Dictionary&lt;RecordKey, V&gt;</c>).</summary>
    public const string ValueMap = "$eq.collections.valueMap";

    /// <summary>Factory for a value-sorted set (<c>SortedSet&lt;T&gt;</c>).</summary>
    public const string SortedSet = "$eq.collections.sortedSet";
    /// <summary>Factory for a key-sorted dictionary (<c>SortedDictionary&lt;K, V&gt;</c>).</summary>
    public const string SortedDictionary = "$eq.collections.sortedDictionary";
    /// <summary>Factory for a key-sorted list (<c>SortedList&lt;K, V&gt;</c>).</summary>
    public const string SortedList = "$eq.collections.sortedList";
}
