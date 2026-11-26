using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A static generic class keeps its statics per closed type (#1855): its own members reach them through
/// <c>this</c>, the closed type they were reached through, and other code through the closed type it names,
/// <c>$eq.statics(Cache, 'int')</c>. The twin declares no type parameters, which TypeScript refuses a static member
/// to read (TS2302), and a type argument known only at run time names no closed type (EQ2018).
/// </summary>
public class StaticGenericClassTests
{
    private const string Cache = """
        using System;
        using System.Collections.Generic;
        namespace NS;
        public static class Cache<T>
        {
            public static T? Last;
            private static readonly List<T> _all = new();
            public static T Keep(T value) { Last = value; _all.Add(value); return Echo(value); }
            private static T Echo(T value) => value;
        }
        """;

    private static IReadOnlyList<CompilationResult> Compile(string source) =>
        [.. new ComponentCompiler().CompileSource(source, "Probe.cs")];

    [Fact]
    public void ItsOwnMembers_ReachTheirClosedTypeThroughThis()
    {
        var cache = Compile(Cache).Single(result => result.ComponentName == "Cache").TypeScript;

        cache.Should().Contain("export class Cache {", "a static class declares no type parameters")
            .And.Contain("this.last = value;").And.Contain("return this.echo(value);")
            .And.Contain("return this.$init().last;").And.NotContain("Cache.");
        cache.Should().EndWith("type T = any;\n", "the members name T, an alias of any after the class");
    }

    [Fact]
    public void OtherCode_ReachesTheClosedTypeItNames()
    {
        var user = Compile(Cache + """
            public sealed class User
            {
                public int Use() { Cache<int>.Keep(1); Func<string, string> keep = Cache<string>.Keep; keep("a"); return Cache<List<int>>.Last?.Count ?? 0; }
            }
            """).Single(result => result.ComponentName == "User");

        user.Errors.Should().BeEmpty();
        user.TypeScript.Should().Contain("import { Cache } from \"./Cache\";")
            .And.Contain("$eq.statics(Cache, 'int').keep(1);")
            .And.Contain("$eq.delegates.bound($0, $0.keep))($eq.statics(Cache, 'string'))")
            .And.Contain("$eq.statics(Cache, 'global::System.Collections.Generic.List<int>').last");
    }

    [Fact]
    public void ATypeArgumentKnownOnlyAtRunTime_NamesNoClosedType()
    {
        var results = Compile(Cache + """
            public sealed class User
            {
                public int Open<U>(U u) { Cache<U>.Keep(u); return Cache<U>.Last is null ? 0 : 1; }
            }
            """);

        results.Single(result => result.ComponentName == "User").Errors
            .Should().HaveCount(2).And.OnlyContain(error => error.Code == "EQ2018" && error.Message.Contains("'NS.Cache<U>'"));
    }

    [Fact]
    public void ATypeDeclaredInsideIt_IsReachedAsATypeFromOutside()
    {
        var results = Compile("""
            namespace NS;
            public static class Cache<T>
            {
                public sealed class Inner { public static int Size => 3; }
            }
            public sealed class User { public int Use() => Cache<int>.Inner.Size; }
            """);

        var user = results.Single(result => result.ComponentName == "User");
        user.Errors.Should().BeEmpty();
        user.TypeScript.Should().Contain("Cache$Inner.size").And.NotContain("$eq.statics", "a type declared inside it is its own module, not one of its statics");
    }

    [Fact]
    public void ItsOwnMethodGroup_IsBoundToItsClosedType_AndImportsWhatBindsIt()
    {
        var pick = Compile("""
            using System;
            namespace NS;
            public static class Pick<T>
            {
                private static T Echo(T value) => value;
                public static Func<T, T> Picker => Echo;
            }
            """).Single(result => result.ComponentName == "Pick").TypeScript;

        pick.Should().Contain("$eq.delegates.bound(this, this.echo)")
            .And.Contain("import { $eq } from \"@equantic/runtime\";", "nothing else in the class names $eq");
    }

    [Fact]
    public void ANamespaceQualifiedClosedType_IsItsOwn()
    {
        var user = Compile(Cache + """
            public sealed class User { public int Use() { global::NS.Cache<int>.Keep(1); return NS.Cache<string>.Last?.Length ?? 0; } }
            """).Single(result => result.ComponentName == "User");

        user.Errors.Should().BeEmpty();
        user.TypeScript.Should().Contain("$eq.statics(Cache, 'int').keep(1);").And.Contain("$eq.statics(Cache, 'string').last");
    }

    [Fact]
    public void AStaticClassInAGenericType_KeepsItsStaticsOnItsClass()
    {
        var results = Compile("""
            namespace NS;
            public class Outer<T>
            {
                public static class Counter { public static int N; public static int Bump() => ++N; }
                public static class Typed<U> { public static int N; public static int Bump() => ++N; }
            }
            public sealed class User { public int Use() => Outer<int>.Counter.Bump() + Outer<int>.Typed<string>.Bump(); }
            """);

        results.SelectMany(result => result.Errors).Should().BeEmpty();
        results.Single(result => result.ComponentName == "User").TypeScript.Should().NotContain("$eq.statics");
        results.Should().OnlyContain(result => !result.TypeScript.Contains("this.n"),
            "a class nested in a generic type keeps the statics on its class, as before");
    }

    [Fact]
    public void ATypeDeclaredInsideIt_HasNoClosedTypeOfIt()
    {
        var results = Compile("""
            namespace NS;
            public static class Cache<T>
            {
                public static int Hits;
                public sealed class Probe { public int Read() => Hits; }
            }
            """);

        results.SelectMany(result => result.Errors).Should().ContainSingle(error => error.Code == "EQ2018");
    }
}
