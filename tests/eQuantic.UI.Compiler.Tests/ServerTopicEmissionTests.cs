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

        public sealed record Notice(string Text)
        {
            public string Shout() => Text.ToUpperInvariant();
        }

        public sealed class Ticker(IServerEvents? events) : StatefulComponent
        {
            private static readonly ServerTopic<Quote> Prices = new("prices");
            private static ServerTopic<Quote> Room(string id) => new($"room:{id}");
            private static readonly ServerTopic<decimal> Rate = new ServerTopic<decimal>("rate");
            private static readonly ServerTopic<string> Note = new("note");
            private static readonly ServerTopic<List<long>> Ids = new("ids");
            private static readonly ServerTopic<Notice> Notices = new("notices");

            private IDisposable? _prices;
            private decimal _price;

            protected override void OnMount() =>
                _prices = events?.Subscribe(Room("a"), quote => SetState(() => _price = quote.Price));

            protected override void OnUnmount() => _prices?.Dispose();

            public override VisualNode Build(ComponentContext context) =>
                new Text($"{_price} {Prices} {Rate} {Note} {Ids} {Notices}", TypeRole.BodyM, null);
        }
        """;

    private static string Ticker()
    {
        var result = Compile(Source, "Ticker");
        result.Success.Should().BeTrue(string.Join("\n", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }

    private static CompilationResult Compile(string source, string component)
    {
        var path = component + ".cs";
        var tree = CSharpSyntaxTree.ParseText(source, ParseDefaults.Options, path: path);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(file => file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(file => (MetadataReference)TestReferences.Of(file))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create(component, [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var compiler = new ComponentCompiler { TypeAnnotations = true };
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, path).Single(r => r.ComponentName == component);
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

    /// <summary>
    /// A record none of whose members needs coercion still carries its twin: the payload is rebuilt on
    /// it, so the record's methods, equality and <c>with</c> work there. Its spec was null, and the
    /// payload arrived as the plain object JSON made of it (#647).
    /// </summary>
    [Fact]
    public void ARecordPayloadWhoseMembersNeedNoCoercion_StillCarriesItsTwin() =>
        Ticker().Should().Contain("new ServerTopic('notices', Notice)");

    [Fact]
    public void TheCapability_IsResolvedByName_AndSubscribedOnTheRuntimesObject()
    {
        var ts = Ticker();
        ts.Should().Contain("$eq.services.resolve('IServerEvents')");
        ts.Should().MatchRegex(@"\.subscribe\(Ticker\.room\('a'\), ");
    }

    /// <summary>
    /// A topic built where its payload's type is a type parameter is refused (EQ2013): the type is
    /// erased there, so the twin would get no spec and every payload would arrive unrevived, with a
    /// green build. The helper is the natural way to factor topics, which is why it is fenced.
    /// </summary>
    [Fact]
    public void ATopicBuiltWhereItsPayloadTypeIsATypeParameter_IsRefused()
    {
        const string source = """
            using eQuantic.UI.Primitives;

            public sealed record Quote(string Symbol, decimal Price);

            public sealed class Feed : StatelessComponent
            {
                private static ServerTopic<T> Topic<T>(string name) => new(name);
                private static readonly ServerTopic<Quote> Prices = Topic<Quote>("prices");

                public override VisualNode Build(ComponentContext context) =>
                    new Text(Prices.Name, TypeRole.BodyM, null);
            }
            """;

        var result = Compile(source, "Feed");

        result.Errors.Should().Contain(error => error.Code == "EQ2013" && error.Message.Contains("'T'"),
            "the type argument is erased where the topic is built");
    }

    /// <summary>
    /// A topic that crosses the wire, a Server Action's result or a page's state, is rebuilt on its
    /// twin with its payload's spec. It arrived as the plain object EqJson wrote, without the spec, and
    /// handed every payload through unrevived.
    /// </summary>
    [Fact]
    public void ATopicThatCrossesTheWire_IsRebuiltWithItsPayloadsSpec()
    {
        const string source = """
            using System.Threading.Tasks;
            using eQuantic.UI.Primitives;

            public sealed record Quote(string Symbol, decimal Price);

            public sealed record Notice(string Text)
            {
                public string Shout() => Text.ToUpperInvariant();
            }

            public sealed class Lobby : StatefulComponent
            {
                private ServerTopic<Quote>? _room;

                [ServerAction]
                public Task<ServerTopic<Quote>> Join(string code) => Task.FromResult(new ServerTopic<Quote>($"room:{code}"));

                [ServerAction]
                public Task<Notice> Latest() => Task.FromResult(new Notice("hello"));

                public override VisualNode Build(ComponentContext context) =>
                    new Text(_room?.Name ?? "", TypeRole.BodyM, null);
            }
            """;

        var result = Compile(source, "Lobby");
        result.Success.Should().BeTrue(string.Join("\n", result.Errors.Select(e => e.Message)));

        result.TypeScript.Should().Contain("{ of: ServerTopic, members: {}, typeArguments: [Quote] }");
        result.TypeScript.Should().MatchRegex(@"\$eq\.hydrate\(await [^;]*\, Notice\)",
            "a Server Action's record result is rebuilt on its twin, coerced members or not");
        result.TypeScript.Should().MatchRegex(@"import \{[^}]*\bServerTopic\b[^}]*\} from ""@equantic/runtime""");
    }
}
