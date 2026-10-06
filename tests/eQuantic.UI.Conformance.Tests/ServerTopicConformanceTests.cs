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
    public void AServerTopicAnswersAsInDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNet(statements);
    }
}
