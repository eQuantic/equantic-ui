using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Every dictionary is the runtime's dictionary class (#435), whose members these pin: `dict` is a
/// <c>Dictionary&lt;string, string&gt;</c> property of the helper's class, read through <c>this</c>.
/// What each answers is proved on both sides by DictionaryEnumerationConformanceTests; these hold the
/// shape and the single evaluation.
/// </summary>
public class DictionaryStrategyTests
{
    [Fact]
    public void ContainsKey_AsksTheClass()
    {
        TestHelper.ConvertExpression("dict.ContainsKey(\"key\")").Should().Be("this.dict.has('key')");
    }

    /// <summary>The lowering names the receiver twice, in the question and in the read, and
    /// `this.dict` is a PROPERTY, whose getter could count its calls, so the writer binds it once.
    /// A miss writes default(TValue) to the out: null for a string.</summary>
    [Fact]
    public void TryGetValue_WithOutVar_BindsTheReceiverOnce_AndAMissWritesTheDefault()
    {
        TestHelper.ConvertExpression("dict.TryGetValue(\"key\", out var value)").Should().Be(
            "(($0) => ($0.has('key') ? ((value = $0.get('key')), true) : ((value = null), false)))(this.dict)");
    }

    /// <summary>A key read through a property is bound once too, after the receiver.</summary>
    [Fact]
    public void TryGetValue_WithVariable_BindsTheReceiverAndTheKeyOnce()
    {
        TestHelper.ConvertExpression("dict.TryGetValue(str, out var result)").Should().Be(
            "(($0, $1) => ($0.has($1) ? ((result = $0.get($1)), true) : ((result = null), false)))(this.dict, this.str)");
    }

    /// <summary><c>Remove(key, out value)</c> moves the value into the out and frees the key's slot;
    /// the one-argument Remove ignored the out it did not have.</summary>
    [Fact]
    public void Remove_WithAnOut_MovesTheValueOut()
    {
        TestHelper.ConvertExpression("dict.Remove(\"key\", out var value)").Should().Be(
            "(($0) => ($0.has('key') ? ((value = $0.get('key')), $0.delete('key')) : ((value = null), false)))(this.dict)");
    }

    [Theory]
    [InlineData("dict.Add(\"key\", \"value\")", "this.dict.set('key', 'value')")]
    [InlineData("dict.Add(str, str)", "this.dict.set(this.str, this.str)")]
    [InlineData("dict.Remove(\"key\")", "this.dict.delete('key')")]
    [InlineData("dict.Clear()", "this.dict.clear()")]
    [InlineData("dict.Keys", "this.dict.keys()")]
    [InlineData("dict.Values", "this.dict.values()")]
    [InlineData("dict.Count", "this.dict.size")]
    // Membership through the keys is the dictionary's own, and so is their count.
    [InlineData("dict.Keys.Contains(str)", "this.dict.has(this.str)")]
    [InlineData("dict.Keys.Count", "this.dict.size")]
    [InlineData("dict.Values.Count", "this.dict.size")]
    [InlineData("dict.TryAdd(str, str)", "this.dict.tryAdd(this.str, this.str)")]
    [InlineData("dict.ContainsValue(str)", "this.dict.containsValue(this.str)")]
    public void Member_IsTheClasss(string csharp, string js)
    {
        TestHelper.ConvertExpression(csharp).Should().Be(js);
    }

    [Fact]
    public void Foreach_WalksTheClass_ItsKeysAndItsPairs()
    {
        TestHelper.ConvertStatement("foreach (var key in dict.Keys) { Console.WriteLine(key); }")
            .Should().Contain("for (const key of this.dict.keys())");
        TestHelper.ConvertStatement("foreach (var (k, v) in dict) { Console.WriteLine(k + v); }")
            .Should().Contain("for (const [k, v] of this.dict)");
        TestHelper.ConvertStatement("foreach (var pair in dict) { Console.WriteLine(pair.Key); }")
            .Should().Contain("for (const pair of this.dict)");
    }

    /// <summary>
    /// A key is found as <c>EqualityComparer&lt;TKey&gt;.Default</c> finds it, which eqc says to the
    /// factory: by value for a decimal, a date, a tuple or a record, by identity, through the class's
    /// Map, for a string (which overrides Equals, and is a primitive here), a number, an enum and a
    /// Guid, and by the key's OWN equality where the type does not decide: a class, whose subclass may
    /// override Equals (<c>Version</c> does itself), <c>object</c> and an interface, whose value may be
    /// a record or a decimal (found in review, #443).
    /// </summary>
    [Theory]
    [InlineData("decimal", "$eq.collections.dictionary(null, true)")]
    [InlineData("DateTime", "$eq.collections.dictionary(null, true)")]
    [InlineData("TimeSpan", "$eq.collections.dictionary(null, true)")]
    [InlineData("(int, int)", "$eq.collections.dictionary(null, true)")]
    [InlineData("Version", "$eq.collections.dictionary(null, 'own')")]
    [InlineData("string", "$eq.collections.dictionary()")]
    [InlineData("int", "$eq.collections.dictionary()")]
    [InlineData("long", "$eq.collections.dictionary()")]
    [InlineData("char", "$eq.collections.dictionary()")]
    [InlineData("DayOfWeek", "$eq.collections.dictionary()")]
    [InlineData("Guid", "$eq.collections.dictionary()")]
    [InlineData("TestClass", "$eq.collections.dictionary(null, 'own')")]
    [InlineData("object", "$eq.collections.dictionary(null, 'own')")]
    [InlineData("IComparable", "$eq.collections.dictionary(null, 'own')")]
    public void AKey_IsFound_AsItsDefaultComparerFindsIt(string key, string factory)
    {
        var js = TestHelper.ConvertStatement($"var d = new Dictionary<{key}, int>();");
        js.Should().Contain(factory);
    }

    /// <summary>A key of a type parameter is the value's to decide, as <c>object</c> is.</summary>
    [Fact]
    public void AKeyOfATypeParameter_IsFoundByItsOwnEquality()
    {
        var ts = TestHelper.ConvertClass("""
            public int Count<T>(T a, T b) { var d = new Dictionary<T, int>(); d[a] = 1; d[b] = 2; return d.Count; }
            """, "Keys");
        ts.Should().Contain("$eq.collections.dictionary(null, 'own')");
    }

    [Theory]
    // A capacity has no meaning here; a copy copies; an initializer seeds after what is copied.
    [InlineData("new Dictionary<string, string>(16)", "$eq.collections.dictionary()")]
    [InlineData("new Dictionary<string, string>(dict)", "$eq.collections.dictionary(this.dict)")]
    [InlineData("new Dictionary<string, string>(dict) { [\"a\"] = \"b\" }", "$eq.collections.dictionary([...this.dict, ['a', 'b']])")]
    [InlineData("new Dictionary<string, int> { { \"a\", 1 }, { \"b\", 2 } }", "$eq.collections.dictionary([['a', 1], ['b', 2]])")]
    [InlineData("new SortedDictionary<int, string>()", "$eq.collections.sortedDictionary()")]
    [InlineData("new SortedList<int, string>(dictionaryOfInts)", "$eq.collections.sortedList(dictionaryOfInts)")]
    public void Construction_SeedsTheFactory(string csharp, string js)
    {
        var converted = csharp.Contains("dictionaryOfInts")
            ? TestHelper.ConvertCodeBlock($"var dictionaryOfInts = new Dictionary<int, string>(); var sorted = {csharp};")
            : TestHelper.ConvertExpression(csharp);
        converted.Should().Contain(js);
    }

    /// <summary>Every name for a dictionary annotates as `any`: the interfaces and the sorted ones
    /// reached TypeScript verbatim, naming types that exist nowhere there (found in review, #443).</summary>
    [Fact]
    public void EveryDictionaryTypeName_AnnotatesAsAny()
    {
        var ts = TestHelper.ConvertClass("""
            public IDictionary<string, int> A { get; set; } = new Dictionary<string, int>();
            public IReadOnlyDictionary<string, int> B { get; set; } = new Dictionary<string, int>();
            public SortedDictionary<string, int> C { get; set; } = new();
            public SortedList<string, int> D { get; set; } = new();
            public Dictionary<string, int> E { get; set; } = new();
            public int Total(IReadOnlyDictionary<string, int> map, SortedList<string, int> sorted) => map.Count + sorted.Count;
            """, "Holder");
        foreach (var name in new[] { "IDictionary<", "IReadOnlyDictionary<", "SortedDictionary<", "SortedList<", "Dictionary<", "Record<" })
            ts.Should().NotContain(name);
        ts.Should().Contain("map: any").And.Contain("sorted: any");
    }

    /// <summary>A comparer decides how keys compare, which the runtime classes take from eqc alone.</summary>
    [Fact]
    public void AComparer_IsRefused()
    {
        TestHelper.DiagnosticsFor("var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("Dictionary with a comparer"));
        TestHelper.DiagnosticsFor("var d = new SortedDictionary<string, int>(StringComparer.Ordinal);")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("SortedDictionary with a comparer"));
    }
}
