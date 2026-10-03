using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A dictionary's <c>Add</c>, a collection initializer and the constructor that copies pairs refuse a
/// key already there with .NET's words, where the indexer's write, an object initializer's
/// <c>[key] = value</c> and <c>TryAdd</c> replace or decline as they always did (#440, #395). And a
/// <c>KeyValuePair</c> built by the app is the pair a dictionary yields (#433).
/// </summary>
public class DictionaryAddConformanceTests
{
    [SkippableTheory]
    [InlineData("var d = new Dictionary<string, int>(); d.Add(\"a\", 1); try { d.Add(\"a\", 2); return \"added\"; } catch (Exception e) { return e.Message; }")] // "An item with the same key has already been added. Key: a"
    [InlineData("try { var d = new Dictionary<string, int> { { \"a\", 1 }, { \"a\", 2 } }; return \"built\"; } catch (Exception e) { return e.Message; }")]  // the same words
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1, [\"a\"] = 2 }; return d[\"a\"] + \",\" + d.Count;")]                                     // "2,1"
    [InlineData("var d = new Dictionary<string, int>(); d[\"a\"] = 1; d[\"a\"] = 2; return d[\"a\"] + \",\" + d.TryAdd(\"a\", 3) + \",\" + d[\"a\"];")]      // "2,False,2"
    [InlineData("var src = new List<KeyValuePair<string, int>> { new(\"a\", 1), new(\"a\", 2) }; try { var d = new Dictionary<string, int>(src); return \"built\"; } catch (Exception e) { return e.Message; }")] // the same words
    [InlineData("var d = new SortedDictionary<int, string>(); d.Add(2, \"b\"); try { d.Add(2, \"c\"); return \"added\"; } catch (Exception e) { return e.Message; }")] // "...Key: 2"
    [InlineData("var d = new SortedList<int, string>(); d.Add(2, \"b\"); try { d.Add(2, \"c\"); return \"added\"; } catch (Exception e) { return e.Message; }")]      // "...Key: 2 (Parameter 'key')"
    [InlineData("var d = new SortedList<int, string> { [1] = \"a\", [1] = \"b\" }; return d[1];")]                                                         // "b"
    public void Add_RefusesAKeyAlreadyThere_TheIndexerReplaces(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [InlineData("var p = new KeyValuePair<int, string>(1, \"a\"); return p.Key + p.Value;")]                                         // "1a"
    [InlineData("var p = KeyValuePair.Create(2, \"b\"); var (k, v) = p; return k + v;")]                                            // "2b"
    [InlineData("var p = new KeyValuePair<int, string>(value: \"c\", key: 3); return p.Key + p.Value;")]                            // "3c"
    [InlineData("var p = default(KeyValuePair<int, string>); return p.Key + \"|\" + (p.Value == null);")]                           // "0|True"
    [InlineData("var d = new Dictionary<int, string> { [1] = \"a\" }; return d.Contains(new KeyValuePair<int, string>(1, \"a\"));")] // true
    public void AKeyValuePairBuiltByTheApp_IsThePairADictionaryYields(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
