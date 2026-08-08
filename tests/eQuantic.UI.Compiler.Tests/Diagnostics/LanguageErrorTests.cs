using eQuantic.UI.Compiler;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.Diagnostics;

/// <summary>
/// eqc holds a Roslyn compilation, so it can always answer "does this C# even compile?" — it just
/// never asked. Roslyn's parser is deliberately lenient: given a file with a syntax error it
/// returns a tree ANYWAY, eqc walks it, and out comes JavaScript built from a half-understood
/// program. In an SDK build csc catches it moments later. In a host where eqc is the whole build
/// — the online playground — nothing did, and the author got a blank frame instead of a message.
/// </summary>
public class LanguageErrorTests
{
    private const string ValidComponent = """
        namespace Demo;

        public class Greeting
        {
            public string Say(string name) => $"Hello, {name}";
        }
        """;

    [Fact]
    public void Source_that_compiles_reports_nothing()
    {
        new ComponentCompiler()
            .GetLanguageErrors(ValidComponent, "Greeting.cs")
            .Should().BeEmpty();
    }

    [Fact]
    public void Syntax_error_is_reported_on_its_own_line()
    {
        // The `%%` a real visitor leaves behind by fat-fingering: Roslyn parses the class fine and
        // records the junk as an error, which is exactly the case that used to transpile "green".
        var source = ValidComponent + "\n%%\n";

        var errors = new ComponentCompiler().GetLanguageErrors(source, "Greeting.cs");

        errors.Should().NotBeEmpty();
        errors[0].Code.Should().StartWith("CS");
        errors[0].Line.Should().Be(7, "the junk is on the line after the closing brace, and a marker "
            + "drawn anywhere else points the author at innocent code");
    }

    [Fact]
    public void Unknown_name_is_reported_even_though_the_tree_parses()
    {
        // Parses perfectly, means nothing: only the SEMANTIC half of the model catches this, and
        // it is the half that turns into `Wat is not defined` at mount time.
        var source = ValidComponent.Replace("$\"Hello, {name}\"", "Wat.Nope(name)");

        var errors = new ComponentCompiler().GetLanguageErrors(source, "Greeting.cs");

        errors.Should().Contain(error => error.Code == "CS0103");
    }

    [Fact]
    public void Errors_carry_the_path_they_were_found_in()
    {
        new ComponentCompiler()
            .GetLanguageErrors("class Broken { void M() { int x = ; } }", "Broken.cs")
            .Should().OnlyContain(error => error.SourcePath == "Broken.cs");
    }
}
