using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Parser;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Scenarios;

/// <summary>
/// Static members declared ON a component class. Call sites are already qualified with the class name
/// (<c>Users.initials(...)</c>, see InvocationStrategy), so the member has to land on the CLASS — emitted
/// as an instance method it sits on the prototype and the call dies at runtime with
/// "X.initials is not a function".
/// </summary>
public class StaticMemberEmissionTests
{
    private static string EmitComponent(string source)
    {
        var component = new ComponentParser().ParseSource(source).Single();
        return new TypeScriptEmitter().Emit(component);
    }

    [Fact]
    public void PrivateStaticHelper_EmitsAsStaticMember()
    {
        var ts = EmitComponent(@"
using eQuantic.UI.Core;

namespace Sample;

public class Users : StatelessComponent
{
    private static string Initials(string name) => name.Substring(0, 1);

    public override Component Build(BuildContext context)
    {
        return new Text(Initials(""Ada""));
    }
}");

        ts.Should().Contain("static initials(", "a static helper must land on the class, not the prototype");
    }

    [Fact]
    public void PrivateStaticHelper_CarriesItsTranspiledBody()
    {
        var ts = EmitComponent(@"
using eQuantic.UI.Core;

namespace Sample;

public class Users : StatelessComponent
{
    private static string Initials(string name) => name.Substring(0, 1);

    public override Component Build(BuildContext context)
    {
        return new Text(Initials(""Ada""));
    }
}");

        // An emitted-but-empty static would fail just as silently as a missing one.
        ts.Should().Contain("static initials(");
        ts.Should().Contain("return $eq.text.substring(name, 0, 1);");
    }

    /// <remarks>
    /// The CALL SITE's `Users.initials(...)` qualification comes from InvocationStrategy reading
    /// `symbol.IsStatic`, so it needs a semantic model with real references — which a bare ParseSource
    /// has not got (it falls back to `this.`). That half is covered end-to-end by building a sample;
    /// what these tests pin is the emission half, where the member was missing entirely.
    /// </remarks>
    [Fact]
    public void InstanceHelper_StaysOnThePrototype()
    {
        var ts = EmitComponent(@"
using eQuantic.UI.Core;

namespace Sample;

public class Users : StatelessComponent
{
    private string Initials(string name) => name.Substring(0, 1);

    public override Component Build(BuildContext context)
    {
        return new Text(Initials(""Ada""));
    }
}");

        ts.Should().Contain("initials(");
        ts.Should().NotContain("static initials(", "an instance helper must NOT become a static member");
    }
}
