using eQuantic.UI.Compiler.Tests.ReferencedProjects;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A Server Action is known by its SYMBOL, as <c>[ServerOnly]</c> is since #1140 (#1148). The parser read
/// the method's own attribute lists by name, so an alias of the attribute and the defining half of a
/// partial method marked nothing it could see: the method crossed with its body, no stub was written,
/// and the build refused the body's file access as client code (EQ2112).
/// </summary>
public sealed class ServerActionBySymbolTests
{
    private const string Body = "System.Threading.Tasks.Task.FromResult(System.IO.File.Exists(\"x\"))";

    private static string Page(string action) =>
        $$"""
        using eQuantic.UI.Primitives;
        using Rpc = eQuantic.UI.Primitives.ServerActionAttribute;
        namespace App.Screens;

        public sealed partial class Home : StatefulComponent
        {
        {{action}}

            public override VisualNode Build(ComponentContext context) =>
                Text("plain", TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
        }
        """;

    public static TheoryData<string, string, string> Spellings() => new()
    {
        { "an alias", $"    [Rpc] public System.Threading.Tasks.Task<bool> Archived() => {Body};", "Home/Archived" },
        { "an alias naming the action", $"    [Rpc(Name = \"Stored\")] public System.Threading.Tasks.Task<bool> Archived() => {Body};", "Home/Stored" },
        { "a partial method's definition",
            $"    [ServerAction] public partial System.Threading.Tasks.Task<bool> Archived();\n\n    public partial System.Threading.Tasks.Task<bool> Archived() => {Body};",
            "Home/Archived" },
        { "the plain spelling", $"    [ServerAction] public System.Threading.Tasks.Task<bool> Archived() => {Body};", "Home/Archived" },
    };

    [Theory]
    [MemberData(nameof(Spellings))]
    public void AServerAction_CrossesAsItsStub_HoweverItIsMarked(string spelling, string action, string id)
    {
        using var solution = new Solution().App("App", globalUsings: SharedLibrary.Factories, files: [("Screens/Home.cs", Page(action))]);
        var built = solution.Build();

        built.Failed.Should().BeFalse($"{spelling} marks a Server Action, whose body never crosses:\n{built.Errors}");
        var twin = built.Module("Home");
        twin.Should().Contain($"'{id}'", "the stub calls the action").And.NotContain("exists", "the body stays on the server");
        twin!.Split($"'{id}'").Should().HaveCount(2, "a partial method is one action");
    }

    [Theory]
    [MemberData(nameof(Spellings))]
    public void AServerAction_CrossesAsItsStub_CompiledOnItsOwn(string spelling, string action, string id)
    {
        // The playground's mode: nothing resolves, and the attribute binds to an error type that still
        // carries its name, the alias target's for an alias.
        var compiled = new ComponentCompiler().CompileSource(Page(action));

        compiled.Should().ContainSingle(spelling).Which.TypeScript.Should().Contain($"'{id}'").And.NotContain("exists");
    }
}
