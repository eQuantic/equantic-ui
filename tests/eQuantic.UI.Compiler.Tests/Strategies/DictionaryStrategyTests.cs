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
    [InlineData("dict.Add(\"key\", \"value\")", "this.dict.add('key', 'value')")]  // Add refuses a key already there (#440)
    [InlineData("dict.Add(str, str)", "this.dict.add(this.str, this.str)")]
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

    /// <summary>A sorted dictionary's and an <c>IDictionary</c>'s <c>TryAdd</c> is <c>CollectionExtensions</c>'
    /// extension, which went out under that class's name, a ReferenceError in the browser (#864), and so
    /// did the extension's static form.</summary>
    [Fact]
    public void TryAdd_ThroughCollectionExtensions_IsTheClassOwn()
    {
        TestHelper.ConvertCodeBlock("var s = new SortedDictionary<string, int>(); s.TryAdd(\"a\", 1);")
            .Should().Contain("s.tryAdd('a', 1)");
        TestHelper.ConvertCodeBlock("IDictionary<string, string> d = new Dictionary<string, string>(); CollectionExtensions.TryAdd(d, \"a\", \"b\");")
            .Should().Contain("d.tryAdd('a', 'b')");
    }

    /// <summary>An extension of the app's own under a lookup's name stays the app's call: a lookup is the
    /// collections' own by the method's type, where TryAdd and GetValueOrDefault went to the runtime
    /// class's whatever they bound to (found in review, #864). TryGetValue is every dictionary's instance
    /// member, which no extension takes from it.</summary>
    [Fact]
    public void AnExtensionOfTheApps_UnderALookupsName_StaysTheApps()
    {
        var js = TestHelper.ConvertClass("""
                public bool Fill(SortedDictionary<string, int> s) => s.TryAdd("a", 1);
                public int Read(SortedDictionary<string, int> s) => s.GetValueOrDefault("a");
            }

            public static class Ledger
            {
                public static bool TryAdd(this SortedDictionary<string, int> s, string key, int value) => false;
                public static int GetValueOrDefault(this SortedDictionary<string, int> s, string key) => -1;
            """);

        js.Should().Contain("Ledger.tryAdd(s, 'a', 1)").And.Contain("Ledger.getValueOrDefault(s, 'a')");
    }

    /// <summary>The app's extension stays the app's in System.Collections.Generic too: the namespace
    /// is text an app may write, so the method's type must be the framework's own, a dictionary or
    /// CollectionExtensions declared outside the source (Copilot, #919).</summary>
    [Fact]
    public void AnExtensionOfTheApps_InTheCollectionsNamespace_StaysTheApps()
    {
        var js = TestHelper.ConvertClass("""
                public bool Fill(SortedDictionary<string, int> s) => s.TryAdd("a", 1);
                public int Read(SortedDictionary<string, int> s) => s.GetValueOrDefault("a");
            }

            namespace System.Collections.Generic
            {
                public static class Ledger
                {
                    public static bool TryAdd(this SortedDictionary<string, int> s, string key, int value) => false;
                    public static int GetValueOrDefault(this SortedDictionary<string, int> s, string key) => -1;
                }
            """);

        js.Should().Contain("Ledger.tryAdd(s, 'a', 1)").And.Contain("Ledger.getValueOrDefault(s, 'a')");
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
    [InlineData("TestClass", "$eq.collections.dictionary(null, 'item')")]
    [InlineData("List<int>", "$eq.collections.dictionary(null, 'item')")]
    [InlineData("object", "$eq.collections.dictionary(null, 'own')")]
    [InlineData("IComparable", "$eq.collections.dictionary(null, 'own')")]
    public void AKey_IsFound_AsItsDefaultComparerFindsIt(string key, string factory)
    {
        var js = TestHelper.ConvertStatement($"var d = new Dictionary<{key}, int>();");
        js.Should().Contain(factory);
    }

    /// <summary>A key of a type parameter is the value's to decide, as <c>object</c> is, by its
    /// <c>IEquatable&lt;T&gt;</c> over its own type first, which .NET's default comparer settles at run
    /// time (<c>'gen'</c>): by <c>'own'</c>, a generic dictionary never asked it.</summary>
    [Fact]
    public void AKeyOfATypeParameter_IsFoundByItsOwnEquality()
    {
        var ts = TestHelper.ConvertClass("""
            public int Count<T>(T a, T b) { var d = new Dictionary<T, int>(); d[a] = 1; d[b] = 2; return d.Count; }
            """, "Keys");
        ts.Should().Contain("$eq.collections.dictionary(null, 'gen')");
    }

    [Theory]
    // A capacity sizes the dictionary as .NET's (#463); a copy copies, refusing a null source by the
    // name of the parameter it binds to (#726); an initializer seeds after what is copied.
    [InlineData("new Dictionary<string, string>(16)", "$eq.collections.dictionary(16)")]
    [InlineData("new Dictionary<string, string>(dict)", "$eq.collections.dictionary(this.dict, undefined, 'dictionary')")]
    [InlineData("new Dictionary<string, string>(dict) { [\"a\"] = \"b\" }", "$eq.collections.dictionary(this.dict, undefined, 'dictionary').set('a', 'b')")]  // an indexer overwrites what the copy has
    [InlineData("new Dictionary<string, int> { { \"a\", 1 }, { \"b\", 2 } }", "$eq.collections.dictionary().add('a', 1).add('b', 2)")]  // one Add per entry, in order
    [InlineData("new SortedDictionary<int, string>()", "$eq.collections.sortedDictionary(null, 'value')")]
    [InlineData("new SortedList<int, string>(dictionaryOfInts)", "$eq.collections.sortedList(dictionaryOfInts, 'value', 'dictionary')")]
    [InlineData("new SortedDictionary<string, int>()", "$eq.collections.sortedDictionary(null, 'text')")]
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

    /// <summary>
    /// A comparer is the fence's to judge (EQ2007), never this strategy's: one that changes equality is
    /// refused once, by the fence, and one that asks for what the runtime classes already do builds, a
    /// sorted one asking for the order it names. This test pinned #443's refusal of EVERY comparer with
    /// EQ1004, <c>StringComparer.Ordinal</c> included, and a site's build stopped on it (#577).
    /// </summary>
    [Fact]
    public void AComparer_IsTheFencesToJudge()
    {
        TestHelper.DiagnosticsFor("var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);")
            .Select(d => d.Code).Should().Contain("EQ2007").And.NotContain("EQ1004");
        TestHelper.DiagnosticsFor("var d = new SortedDictionary<string, int>(StringComparer.Ordinal);")
            .Should().BeEmpty();
        TestHelper.ConvertExpression("new SortedDictionary<string, int>(StringComparer.Ordinal)")
            .Should().EndWith("(null, 'value')");
    }
}
