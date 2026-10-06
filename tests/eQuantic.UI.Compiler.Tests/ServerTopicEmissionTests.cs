using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A <c>ServerTopic&lt;T&gt;</c> built in a component carries the hydration spec of its payload's type
/// into the browser (#291): <c>T</c> is erased in JavaScript, and its parameter's
/// <c>[HydratesTypeArgument]</c> tells eqc to hand the twin the spec a Server Action's result is revived
/// with, after the constructor's arguments. The capability reaches the component by name, as every
/// other one does.
/// </summary>
public class ServerTopicEmissionTests
{
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using eQuantic.UI.Primitives;

        public sealed record Quote(string Symbol, decimal Price);

        public sealed class Ticker(IServerEvents? events) : StatefulComponent
        {
            private static readonly ServerTopic<Quote> Prices = new("prices");
            private static ServerTopic<Quote> Room(string id) => new($"room:{id}");
            private static readonly ServerTopic<decimal> Rate = new ServerTopic<decimal>("rate");
            private static readonly ServerTopic<string> Note = new("note");
            private static readonly ServerTopic<List<long>> Ids = new("ids");

            private IDisposable? _prices;
            private decimal _price;

            protected override void OnMount() =>
                _prices = events?.Subscribe(Room("a"), quote => SetState(() => _price = quote.Price));

            protected override void OnUnmount() => _prices?.Dispose();

            public override VisualNode Build(ComponentContext context) =>
                new Text($"{_price} {Prices} {Rate} {Note} {Ids}", TypeRole.BodyM, null);
        }
        """;

    private static string Ticker()
    {
        var tree = CSharpSyntaxTree.ParseText(Source, ParseDefaults.Options, path: "Ticker.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)TestReferences.Of(path))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Ticker", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var compiler = new ComponentCompiler { TypeAnnotations = true };
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(Source, "Ticker.cs").Single(r => r.ComponentName == "Ticker");
        result.Success.Should().BeTrue(string.Join("\n", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }

    [Fact]
    public void ARecordPayload_CarriesItsTwin_AsTheSpec()
    {
        var ts = Ticker();
        ts.Should().Contain("new ServerTopic('prices', Quote)");
        ts.Should().MatchRegex(@"import \{[^}]*\bServerTopic\b[^}]*\} from ""@equantic/runtime""");
        ts.Should().NotContain("./ServerTopic", "the vocabulary's twin is the runtime's, not a sibling module");
    }

    [Fact]
    public void ATargetTypedTopic_CarriesTheSpecOfItsTarget() =>
        Ticker().Should().Contain("new ServerTopic(`room:${id}`, Quote)");

    [Fact]
    public void AScalarPayload_CarriesItsTag_AndOneThatNeedsNoRevival_CarriesNull()
    {
        var ts = Ticker();
        ts.Should().Contain("new ServerTopic('rate', 'decimal')");
        ts.Should().Contain("new ServerTopic('note', null)");
        ts.Should().Contain("new ServerTopic('ids', ['long'])");
    }

    [Fact]
    public void TheCapability_IsResolvedByName_AndSubscribedOnTheRuntimesObject()
    {
        var ts = Ticker();
        ts.Should().Contain("$eq.services.resolve('IServerEvents')");
        ts.Should().MatchRegex(@"\.subscribe\(Ticker\.room\('a'\), ");
    }
}
