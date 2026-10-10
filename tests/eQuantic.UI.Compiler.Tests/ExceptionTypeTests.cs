using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// An exception carries the types it is, its type arguments and those of the types it is nested in
/// among them, which a typed <c>catch</c>, <c>is</c> and its name read. JavaScript erases a type
/// parameter, so an exception type named where a type argument it carries is one is refused (EQ2013):
/// <c>Make.Of&lt;int&gt;()</c> threw an exception that carried <c>T</c> itself, and
/// <c>catch (Failed&lt;int&gt;)</c> missed it (Copilot's second review of #708).
/// </summary>
public class ExceptionTypeTests
{
    private const string Source = """
        using System;
        namespace App
        {
            public class Failed<T> : Exception { public Failed() : base("failed") { } }
            public static class Make { public static Exception Of<T>() => new Failed<T>(); }
            public class Cache<T> { public class Missing : Exception { } public void Get() => throw new Missing(); }
            public static class Catcher
            {
                public static string Run<T>(Exception e) { try { throw e; } catch (Failed<T>) { return "T"; } catch (Exception) { return "other"; } }
            }
            public static class Closed
            {
                public static string Run()
                {
                    try { throw new Failed<int>(); } catch (Failed<string>) { return "string"; } catch (Failed<int>) { return "int"; }
                }
            }
        }
        """;

    private static CompilationResult Compile(string name) =>
        new ComponentCompiler().CompileSource(Source, "Probe.cs").Single(result => result.ComponentName == name);

    [Theory]
    [InlineData("Make", "'App.Failed<T>' is built")]
    [InlineData("Cache", "'App.Cache<T>.Missing' is built")]
    [InlineData("Catcher", "'App.Failed<T>' is tested")]
    public void AnExceptionTypeNamedWhereATypeArgumentIsATypeParameter_IsRefused(string name, string named)
    {
        var result = Compile(name);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Code == "EQ2013" && error.Message.Contains(named) && error.Message.Contains("'T'"),
            "the browser cannot tell which type T is, so the exception would carry, or the test compare, T itself");
    }

    [Fact]
    public void AnExceptionTypeOfConcreteTypeArguments_IsBuiltAndTested()
    {
        var result = Compile("Closed");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("App.Failed`1[System.Int32]").And.Contain("App.Failed`1[System.String]");
    }
}
