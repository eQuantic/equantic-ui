using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A <c>ServerTopic&lt;T&gt;</c> answers in the browser what it answers in .NET (#291): its name, its
/// text, its equality and hash, and its refusal of an empty name. The twin is hand-written and carries
/// the payload's hydration spec beside the name, so two topics of one name and payload type have to be
/// equal there too, the spec of a list (a fresh array at each construction) included.
/// </summary>
public class ServerTopicConformanceTests
{
    [SkippableTheory]
    [InlineData("return new ServerTopic<string>(\"prices\").Name;")]
    [InlineData("return new ServerTopic<decimal>(\"rate\").ToString();")]
    [InlineData("var id = 7; return $\"{new ServerTopic<long>($\"room:{id}\")}\";")]
    [InlineData("return new ServerTopic<System.Collections.Generic.List<long>>(\"ids\") == new ServerTopic<System.Collections.Generic.List<long>>(\"ids\");")]
    [InlineData("return new ServerTopic<System.Collections.Generic.List<long>>(\"ids\").Equals(new ServerTopic<System.Collections.Generic.List<long>>(\"other\"));")]
    [InlineData("return new ServerTopic<decimal>(\"rate\").GetHashCode() == new ServerTopic<decimal>(\"rate\").GetHashCode();")]
    [InlineData("var topics = new System.Collections.Generic.HashSet<ServerTopic<int>> { new(\"a\"), new(\"a\"), new(\"b\") }; return topics.Count;")]
    [InlineData("try { _ = new ServerTopic<string>(\"\"); return \"built\"; } catch (System.ArgumentException e) { return e.Message; }")]
    [InlineData("try { _ = new ServerTopic<string>(null!); return \"built\"; } catch (System.ArgumentNullException e) { return e.Message; }")]
    // A copy keeps the name and the spec it revives with: the twin is built from arguments, so
    // `with` patches a copy of it rather than rebuilding it from one object (#647).
    [InlineData("var rate = new ServerTopic<decimal>(\"rate\"); var copy = rate with { }; return copy == rate && !object.ReferenceEquals(copy, rate);")]
    [InlineData("var ids = new ServerTopic<System.Collections.Generic.List<long>>(\"ids\"); return (ids with { }).Name + (ids with { }).GetHashCode().Equals(ids.GetHashCode());")]
    // The same rule copies the other vocabulary record built only from arguments: its constructor
    // threw on the copy it was handed as its first argument.
    [InlineData("var palette = DataPalette.Default; var copy = palette with { }; return copy == palette && copy.Series.Count == DataPalette.SeriesCeiling;")]
    // The two structs of a reading are never null in C#: a field nobody assigned, `new()` and `default`
    // are their zero, which the browser builds too (#647).
    [InlineData("var connection = default(ServerConnection); return connection.State == ServerConnectionState.Disconnected && connection.LastEventId == null;")]
    [InlineData("return new ServerConnection() == ServerConnection.Disconnected;")]
    [InlineData("var refusal = default(ServerTopicRefusal); return refusal.Reason == ServerTopicRefusalReason.Forbidden && refusal.Topic == null;")]
    [InlineData("return new ServerTopicRefusal().Equals(default(ServerTopicRefusal)) && new ServerTopicRefusal(\"x\", ServerTopicRefusalReason.Unknown).Topic == \"x\";")]
    public void AServerTopicAnswersAsInDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNet(statements);
    }
}
