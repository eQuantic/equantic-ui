using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A snippet compiled on its own has no project's compilation, so an exception class of the app's gets no
/// module there and is built as the runtime's exception (#708). What it declares is out of reach, and that
/// failed in the browser: <c>Failure.Count++</c> named a class nothing wrote, <c>f.Code</c> read nothing and
/// <c>f.Describe()</c> was not a function. Now it fails the build (#754).
/// </summary>
public sealed class AnExceptionInASnippetTests
{
    private const string Failure =
        """
        public class Failure : Exception
        {
            public static int Count;
            public int Code = 7;
            public Failure(string message) : base(message) { }
            public string Describe() => Message;
        }
        """;

    private static IReadOnlyList<string> Errors(string body)
    {
        var source = $$"""
            using System;
            {{Failure}}
            public class User
            {
                public string Run()
                {
                    {{body}}
                }
            }
            """;
        return [.. new ComponentCompiler().CompileSource(source).SelectMany(result => result.Errors)
            .Where(error => error.Code != null).Select(error => $"{error.Code}: {error.Message}")];
    }

    [Theory]
    [InlineData("Failure.Count++; return \"\";", "'Failure.Count'")]
    [InlineData("try { throw new Failure(\"m\"); } catch (Failure f) { return f.Code.ToString(); }", "'Failure.Code'")]
    [InlineData("try { throw new Failure(\"m\"); } catch (Failure f) { return f.Describe(); }", "'Failure.Describe'")]
    public void AMemberTheClassDeclares_FailsTheBuild_WhereTheBrowserFailedIt(string body, string member) =>
        Errors(body).Should().ContainSingle().Which.Should().StartWith("EQ2010").And.Contain(member);

    [Theory]
    [InlineData("try { throw new Failure(\"m\"); } catch (Failure f) { return f.Message; }")]
    [InlineData("try { throw new Failure(\"m\"); } catch (Exception e) { return e.Message; }")]
    public void WhatTheRuntimesExceptionCarries_StillBuilds(string body) =>
        Errors(body).Should().BeEmpty();
}
