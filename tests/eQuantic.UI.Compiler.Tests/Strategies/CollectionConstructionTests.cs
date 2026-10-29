using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// `new List&lt;T&gt;(x)` means two OPPOSITE things depending on what `x` is, and the difference is
/// invisible in the syntax. Sizing a list ahead — the most ordinary optimisation there is — used to
/// emit the capacity AS the list: `let lines = 12;` and then `lines.push(...)`, which throws in the
/// browser and says nothing at build time. Found in a document model that rebuilt its lines.
/// <para>
/// Written against a real compilation rather than a bare expression: which meaning applies is a
/// question for the resolved constructor, and a harness with no symbols cannot ask it.
/// </para>
/// </summary>
public class CollectionConstructionTests
{
    // TypeScript: the element type an empty collection is annotated with is for the type checker,
    // and plain JavaScript carries no annotation at all.
    private static string Convert(string body) => TestHelper.ConvertCodeBlock(body, typeAnnotations: true);

    [Fact]
    public void ACapacityMakesAnEMPTYCollection()
    {
        var js = Convert("""
            var source = new List<string>();
            var sized = new List<string>(source.Count + 1);
            var fixedSize = new List<string>(16);
            """);

        // A capacity is a hint, not the contents, but C# evaluates it and the constructor refuses a negative
        // one, so one that is not a constant the list takes is handed to the runtime first.
        js.Should().Contain("let sized: string[] = ($eq.collections.listCapacity(source.length + 1), []);",
            "a capacity is a hint, not the contents, and is evaluated and checked as the constructor does");
        js.Should().Contain("let fixedSize: string[] = [];", "a constant the list takes has nothing to evaluate or refuse");
    }

    [Fact]
    public void ASourceCollectionIsCOPIED_NotAliased()
    {
        var js = Convert("""
            var source = new List<string>();
            var copy = new List<string>(source);
            """);

        js.Should().Contain("let copy: string[] = $eq.collections.listCopy(source);",
            "C# copies the source — handing back the same array would make every later Add "
            + "mutate what it was built from — and refuses a null one by its parameter's name (#726)");
    }

    /// <summary>
    /// An empty list of tuples is annotated with the TS tuple of its elements: a tuple crosses as an array
    /// literal, and left to inference the list's pushes made it an array of arrays, which the code block's
    /// stretches, returned as a list of pairs, could not be (#1063).
    /// </summary>
    [Fact]
    public void AnEmptyListOfTuples_IsAnnotatedWithTheTuple()
    {
        Convert("var pairs = new List<(int From, int To)>();").Should().Contain("let pairs: [number, number][] = [];");
        Convert("var named = new List<(string Name, bool On)>();").Should().Contain("let named: [string, boolean][] = [];");
        Convert("var keyed = new List<(KeyValuePair<string, int> Entry, int At)>();")
            .Should().Contain("let keyed = [];", "an element with no TS name leaves the list to inference");
    }

    /// <summary>
    /// A nullable element holds its null, in a tuple's slot and as a list's item, a union parenthesised as
    /// an array's element (Copilot on #1068): written without it, pushing the null C# allowed failed the type
    /// check.
    /// </summary>
    [Fact]
    public void ANullableElement_IsAnnotatedWithItsNull()
    {
        Convert("var pairs = new List<(string? Name, int At)>();").Should().Contain("let pairs: [string | null, number][] = [];");
        Convert("var names = new List<string?>();").Should().Contain("let names: (string | null)[] = [];");
        Convert("var counts = new List<int?>();").Should().Contain("let counts: (number | null)[] = [];");
        Convert("var rows = new List<List<int?>>();").Should().Contain("let rows: (number | null)[][] = [];");
        Convert("var seen = new HashSet<string?>();").Should().Contain("let seen: Set<string | null> = ");
    }

    /// <summary>
    /// An empty collection expression in a typed local is the same empty list written the newer way, and is
    /// annotated as <c>new List&lt;T&gt;()</c> is (#1086): written <c>let found = [];</c>, TypeScript inferred
    /// <c>any[]</c> and, in strict mode, refused it once a branch assigned the local. A set is the runtime's
    /// typed helper, and an enumerable may be handed something that is not an array.
    /// </summary>
    [Fact]
    public void AnEmptyCollectionExpression_IsAnnotatedWithItsItem()
    {
        Convert("IReadOnlyList<int> found = [];").Should().Contain("let found: number[] = [];");
        Convert("IList<string?> names = [];").Should().Contain("let names: (string | null)[] = [];");
        Convert("List<bool> flags = [];").Should().Contain("let flags: boolean[] = [];");
        Convert("int[] none = [];").Should().Contain("let none: number[] = [];");
        Convert("IReadOnlyList<int> some = [1, 2];").Should().Contain("let some = [1, 2];", "inference reads the items");
        Convert("IEnumerable<int> sequence = [];").Should().Contain("let sequence = [];");
        // A collection declared nullable keeps its null (Copilot on #1091): `items = null` was refused.
        Convert("int[]? items = []; items = null;").Should().Contain("let items: number[] | null = [];");
        Convert("IReadOnlyList<string?>? names = [];").Should().Contain("let names: (string | null)[] | null = [];");
    }

    /// <summary>
    /// An empty collection expression whose target is a class of the runtime's is that class, built as
    /// <c>new()</c> builds it, with the capacity <c>with(…)</c> hands the constructor where the class keeps
    /// one (#1144). <c>Dictionary&lt;string, int&gt; ids = []</c> went out as <c>let ids = [];</c>, whose
    /// first write threw. A dictionary's local degrades to <c>any</c>, as <c>new()</c>'s does.
    /// </summary>
    [Fact]
    public void AnEmptyCollectionExpression_BuildsTheRuntimesClass()
    {
        Convert("Dictionary<string, int> ids = [];").Should().Contain("let ids: any = $eq.collections.dictionary();");
        Convert("Dictionary<string, int> sized = [with(10)];").Should().Contain("let sized: any = $eq.collections.dictionary(10);");
        Convert("Dictionary<string, int> named = [with(capacity: 3)];").Should().Contain("let named: any = $eq.collections.dictionary(3);");
        Convert("SortedDictionary<int, int> sorted = [];").Should().Contain("let sorted: any = $eq.collections.sortedDictionary(");
        Convert("SortedList<int, int> list = [with(8)];").Should().Contain("let list: any = $eq.collections.sortedList(")
            .And.NotContain("sortedList(8", "a sorted one's capacity decides nothing, and its construction drops it");
        Convert("Queue<int> pending = [with(4)];").Should().Contain("let pending = $eq.collections.queue(4);");
        Convert("Stack<int> undo = [];").Should().Contain("let undo = $eq.collections.stack();");
        Convert("LinkedList<int> chain = [];").Should().Contain("let chain = $eq.collections.linkedList();");
        Convert("var ids = new Dictionary<string, int>();").Should().Contain("let ids: any = $eq.collections.dictionary();",
            "the construction it is the newer spelling of");
    }

    /// <summary>
    /// A field, a property and an argument take the same class (#1144): the target is what the expression
    /// is converted to wherever it is written.
    /// </summary>
    [Fact]
    public void AnEmptyCollectionExpression_BuildsTheRuntimesClass_WhereverItIsWritten()
    {
        var ts = TypeScriptOf("""
            using System.Collections.Generic;
            public class Interner
            {
                private readonly Dictionary<string, int> _ids = [];
                public Queue<string> Pending { get; } = [];
                public int Count(Dictionary<string, int> seen) => seen.Count;
                public int Fresh() => Count([]);
            }
            """, "Interner");

        ts.Should().NotContain("= [];").And.NotContain("count([])");
        ts.Should().Contain("$eq.collections.dictionary()").And.Contain("$eq.collections.queue()");
    }

    /// <summary>
    /// None of these classes has the <c>Add</c> a collection expression's element is added by, so an
    /// element reaches one only through an <c>Add</c> of the app's own, which nothing here writes: the
    /// build refuses it (EQ1004), where an array was written (#1144).
    /// </summary>
    [Theory]
    [InlineData("public static void Add(this Queue<int> queue, int value) => queue.Enqueue(value);",
        "public Queue<int> Make() => [1, 2];")]
    [InlineData("public static void Add(this Dictionary<string, int> d, KeyValuePair<string, int> pair) => d.Add(pair.Key, pair.Value);",
        "public Dictionary<string, int> Make() => [new(\"a\", 1)];")]
    public void AnElementAddedByAnExtension_IsRefused(string extension, string member)
    {
        var errors = new ComponentCompiler().CompileSource($$"""
            using System.Collections.Generic;
            public static class Adds
            {
                {{extension}}
            }
            public class Producer
            {
                {{member}}
            }
            """, "Probe.cs").SelectMany(result => result.Errors).Select(error => error.Code);

        errors.Should().Contain("EQ1004");
    }

    /// <summary>
    /// A type the app does not declare is named as the mapper spells it, in a tuple's slot and as a list's
    /// item (Copilot on #1068): a Guid crosses as a string, and a list named `Guid` named nothing. A type the
    /// app declares keeps its name, a `Guid` of its own included.
    /// </summary>
    [Fact]
    public void ATypeTheAppDoesNotDeclare_IsNamedAsItCrosses()
    {
        Convert("var ids = new List<(Guid Id, int Index)>();").Should().Contain("let ids: [string, number][] = [];");
        Convert("var plain = new List<Guid>();").Should().Contain("let plain: string[] = [];");

        var own = TypeScriptOf("""
            using System.Collections.Generic;
            public class Guid { public int N = 1; }
            public class Holder
            {
                public int Count()
                {
                    var ids = new List<(Guid Id, int Index)>();
                    return ids.Count;
                }
            }
            """, "Holder");
        own.Should().Contain("let ids: [Guid, number][] = [];", "the app's own Guid is its class");
    }

    /// <summary>
    /// A type parameter is named as its declaration names it (Copilot on #1068): `@class` is declared
    /// `class$`, and a tuple's slot and a list's item said `class`, which TypeScript cannot parse.
    /// </summary>
    [Fact]
    public void ATypeParameter_IsNamedAsItsDeclarationNamesIt()
    {
        var ts = TypeScriptOf("""
            using System.Collections.Generic;
            public class Bag
            {
                public int Count<@class>(@class value)
                {
                    var pairs = new List<(@class Value, int Index)>();
                    var plain = new List<@class>();
                    pairs.Add((value, 1));
                    plain.Add(value);
                    return pairs.Count + plain.Count;
                }
            }
            """, "Bag");
        ts.Should().Contain("let pairs: [class$, number][] = [];").And.Contain("let plain: class$[] = [];");
    }

    /// <summary>
    /// A type parameter declared <c>T?</c> holds its null as a parameter of that type does (Copilot on #1068):
    /// the slot said <c>T</c>, and adding the parameter, a <c>T | null</c>, failed the type check.
    /// </summary>
    [Fact]
    public void ATypeParameterDeclaredNullable_HoldsItsNull()
    {
        var ts = TypeScriptOf("""
            using System.Collections.Generic;
            public class Bag
            {
                public int Count<T>(T? value)
                {
                    var pairs = new List<(T? Value, int Index)>();
                    var plain = new List<T?>();
                    pairs.Add((value, 1));
                    plain.Add(value);
                    return pairs.Count + plain.Count;
                }
            }
            """, "Bag");
        ts.Should().Contain("let pairs: [T | null, number][] = [];").And.Contain("let plain: (T | null)[] = [];");
    }

    /// <summary>
    /// A task held as a value is a promise (Copilot on #1068): the mapper's <c>void</c> is what a method
    /// returning one is awaited to, and pushing <c>Promise.resolve()</c> into a <c>void</c> slot failed the
    /// type check. A name nothing can verify, a Version's, leaves the list to inference, as a declared type
    /// degrades to <c>any</c>.
    /// </summary>
    [Fact]
    public void ATaskIsAPromise_AndANameNothingVerifies_IsLeftToInference()
    {
        Convert("var jobs = new List<(System.Threading.Tasks.Task Work, int At)>();")
            .Should().Contain("let jobs: [Promise<void>, number][] = [];");
        Convert("var tasks = new List<System.Threading.Tasks.Task>();").Should().Contain("let tasks: Promise<void>[] = [];");
        Convert("var versions = new List<System.Version>();").Should().Contain("let versions = [];");
    }

    private static string TypeScriptOf(string source, string type) =>
        new ComponentCompiler().CompileSource(source, "Probe.cs").Single(result => result.ComponentName == type).TypeScript;

    [Fact]
    public void AndTheOrdinaryShapesAreUnchanged()
    {
        Convert("var empty = new List<string>();").Should().Contain("let empty: string[] = [];",
            "an empty array infers `any[]`, so the element type is emitted with it");
        Convert("""var seeded = new List<string> { "a", "b" };""")
            .Should().Contain("let seeded = ['a', 'b'];",
                "a literal with elements infers its own type");
    }
}
