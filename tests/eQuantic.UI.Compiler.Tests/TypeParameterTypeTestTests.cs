using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A type test against a type parameter is refused with EQ2016, naming the parameter (#1846). The browser carries no
/// type arguments at run time: a closed construction is marked with them as TEXT (<c>$eq.closing</c>), and a test of
/// "is a T" takes a subclass, which no text can answer. So the test fell to <c>PatternConverter.TypeCheck</c>'s last
/// line, <c>x != null</c>, a guessed translation, true for any value: a factory of one recognizer said it made every
/// one. CLAUDE.md's rule is the other way round: a construct the model cannot answer is a build error.
/// </summary>
public class TypeParameterTypeTestTests
{
    private static List<CompilationError> Errors(string body) =>
        new ComponentCompiler().CompileSource($$"""
            using System;

            public sealed class Shape { }

            public sealed class Maker<T> where T : class
            {
                {{body}}
            }
            """, "Maker.cs").SelectMany(result => result.Errors).ToList();

    [Theory]
    [InlineData("public bool Makes(object value) => value is T;")]
    [InlineData("public T? Cast(object value) => value as T;")]
    [InlineData("public string Name(object value) => value switch { T made => \"made\", _ => \"other\" };")]
    [InlineData("public bool Holds(object value) => value is T { } made && made != null;")]
    [InlineData("public bool Makes<TOther>(object value) where TOther : class => value is TOther;")]
    [InlineData("public string Guard<TError>(Action run) where TError : Exception { try { run(); return \"ran\"; } catch (TError) { return \"caught\"; } }")]
    public void ATypeTestAgainstATypeParameter_IsRefused_NamingIt(string member)
    {
        var refused = Errors(member).Where(error => error.Code == "EQ2016").ToList();

        refused.Should().ContainSingle("the test is refused once, at the type it names");
        refused[0].Message.Should().MatchRegex("'T(Other|Error)?'", "the message names the type parameter");
        refused[0].Message.Should().Contain("type arguments", "and says why the browser cannot answer it");
    }

    [Fact]
    public void ATypeTestAgainstAClass_IsNotRefused() =>
        Errors("public bool IsShape(object value) => value is Shape;").Should().NotContain(error => error.Code == "EQ2016");
}
