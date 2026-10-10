using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A record, a struct and a value tuple compare each member as <c>EqualityComparer&lt;T&gt;.Default</c>
/// compares its type, so an array member by REFERENCE, as .NET does (#554): a record's twin compared
/// every member with <c>$eq.equals</c>, which walks an array element by element, so two records holding
/// two arrays of the same items were equal. The static type says which members are arrays, so the
/// comparison is written per member; a tuple seen only as <c>object</c> is an array on this side and
/// keeps comparing by its elements, which the wiki's SupportedFeatures page says.
/// </summary>
public class ArrayMemberEqualityConformanceTests
{
    private const string Prelude = """
        public record Items(int[] Values);
        public record Named(string Name, int[] Values);
        public record struct Pair(int[] Left, int Right);
        public record Listed(List<int> Values);
        public record Viewed(IReadOnlyList<int> Values);
        public record Paired(KeyValuePair<int, int[]>? Value);
        """;

    [SkippableTheory]
    // Two arrays of the same items are two arrays.
    [InlineData("return new Items(new[] { 1 }) == new Items(new[] { 1 });")]                                       // false
    [InlineData("var a = new[] { 1 }; return new Items(a) == new Items(a);")]                                      // true: the same array
    [InlineData("return new Named(\"x\", new[] { 1 }).Equals(new Named(\"x\", new[] { 1 }));")]                    // false
    [InlineData("var a = new[] { 1 }; return new Named(\"x\", a).Equals(new Named(\"x\", a));")]                   // true
    [InlineData("var a = new[] { 1 }; return new Named(\"x\", a) == new Named(\"y\", a);")]                        // false: the other member decides
    [InlineData("return new Pair(new[] { 1 }, 2).Equals(new Pair(new[] { 1 }, 2));")]                             // false: a record struct
    [InlineData("var a = new[] { 1 }; return new Pair(a, 2) == new Pair(a, 2);")]                                  // true
    // A class member by its own Equals, which a list does not override: by reference. (The prelude
    // emits records only, so a class of the case's own cannot be written here.)
    [InlineData("return new Listed(new List<int> { 1 }) == new Listed(new List<int> { 1 });")]                     // false
    [InlineData("var l = new List<int> { 1 }; return new Listed(l) == new Listed(l);")]                            // true
    // A tuple's Equals compares each element by its type's comparer.
    [InlineData("return (new[] { 1 }, 1).Equals((new[] { 1 }, 1));")]                                            // false
    [InlineData("var a = new[] { 1 }; return (a, 1).Equals((a, 1));")]                                             // true
    [InlineData("return (1, \"a\").Equals((1, \"a\"));")]                                                         // true: no array, by value
    // A member typed by a collection's interface holds a list, found by reference, alone or in a
    // tuple, where its elements were compared: no tuple implements IReadOnlyList<int>.
    [InlineData("return new Viewed(new List<int> { 1 }) == new Viewed(new List<int> { 1 });")]                     // false
    [InlineData("var l = new List<int> { 1 }; return new Viewed(l) == new Viewed(l);")]                            // true
    [InlineData("IReadOnlyList<int> a = new List<int> { 1 }, b = new List<int> { 1 }; return (a, 1).Equals((b, 1));")] // false
    [InlineData("IReadOnlyList<int> a = new List<int> { 1 }; return (a, 1).Equals((a, 1));")]                     // true
    // A tuple of another arity is another type, whose elements past the receiver's were not read.
    [InlineData("var a = new[] { 1 }; return (a, 1).Equals((object)(a, 1, 2));")]                                 // false
    [InlineData("var a = new[] { 1 }; return (a, 1).Equals((object)(a, 1));")]                                    // true
    // A null pair, a Nullable one, is equal only to another, where reading it threw.
    [InlineData("return new Paired(null) == new Paired(null);")]                                                  // true
    [InlineData("return new Paired(null) == new Paired(new KeyValuePair<int, int[]>(1, null));")]                 // false
    public void AnArrayMember_ComparesByReference(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
