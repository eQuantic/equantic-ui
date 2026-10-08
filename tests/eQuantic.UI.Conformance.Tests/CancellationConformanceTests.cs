using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// System.Threading's cancellation, executed on both sides: the C# on .NET, and its translation
/// (<c>$eq.cancellation</c> and the runtime's <c>utils/cancellation.ts</c>) on the embedded engine.
/// The code engine's completion was the first write-once code to cancel a request it no longer
/// wanted (#296), and nothing in the translation knew the trio: <c>new CancellationTokenSource()</c>
/// named a class no module defined. The names are written in full because the harness imports no
/// <c>System.Threading</c>, and a delay is left out: a clock answers differently on each side.
/// </summary>
public class CancellationConformanceTests
{
    private const string Source = "System.Threading.CancellationTokenSource";
    private const string Token = "System.Threading.CancellationToken";
    private const string Registration = "System.Threading.CancellationTokenRegistration";

    private static void SameAsDotNet(string program)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(program
            .Replace("CTR", Registration, StringComparison.Ordinal)
            .Replace("CTS", Source, StringComparison.Ordinal)
            .Replace("CT.", Token + ".", StringComparison.Ordinal)
            .Replace("new CT(", $"new {Token}(", StringComparison.Ordinal)
            .Replace("default(CT)", $"default({Token})", StringComparison.Ordinal)
            .Replace("(CT ", $"({Token} ", StringComparison.Ordinal));
    }

    [SkippableTheory]
    // A token reads its source, and cancelling is once.
    [InlineData("var cts = new CTS(); var t = cts.Token; var before = t.IsCancellationRequested; cts.Cancel(); cts.Cancel(); "
        + "return $\"{before}|{t.IsCancellationRequested}|{cts.IsCancellationRequested}|{t.CanBeCanceled}\";")]
    // The token that never cancels, by its name and as the default.
    [InlineData("return $\"{CT.None.IsCancellationRequested}|{CT.None.CanBeCanceled}|{default(CT).CanBeCanceled}|{default(CT) == CT.None}\";")]
    // `new CancellationToken(canceled)`: the one cancelled token, or the one that never is.
    [InlineData("var on = new CT(true); var off = new CT(false); "
        + "return $\"{on.IsCancellationRequested}{on.CanBeCanceled}{off.IsCancellationRequested}{off.CanBeCanceled}"
        + "{on == new CT(true)}{off == CT.None}\";")]
    public void ASourceAndItsToken_AnswerAsDotNetDoes(string program) => SameAsDotNet(program);

    [SkippableTheory]
    // The last registered runs first, and each once.
    [InlineData("var log = \"\"; var cts = new CTS(); cts.Token.Register(() => log += \"a\"); cts.Token.Register(() => log += \"b\"); "
        + "cts.Token.Register(() => log += \"c\"); cts.Cancel(); cts.Cancel(); return log;")]
    // Registered after the cancellation, a callback runs at once, and its registration is the default
    // one, whose token is the one that never cancels; registered before, it holds the source's token.
    [InlineData("var cts = new CTS(); cts.Cancel(); var ran = false; var r = cts.Token.Register(() => ran = true); "
        + "var live = new CTS(); var l = live.Token.Register(() => { }); "
        + "return $\"{ran}|{r.Token == CT.None}|{r.Token.CanBeCanceled}|{l.Token == live.Token}\";")]
    // A disposed registration runs nothing, and unregisters once.
    [InlineData("var log = \"\"; var cts = new CTS(); var r = cts.Token.Register(() => log += \"x\"); var first = r.Unregister(); "
        + "var second = r.Unregister(); r.Dispose(); cts.Cancel(); return $\"[{log}]{first}{second}\";")]
    // The callbacks that throw are gathered after the others ran.
    [InlineData("var log = \"\"; var cts = new CTS(); cts.Token.Register(() => log += \"1\"); "
        + "cts.Token.Register(() => throw new InvalidOperationException(\"boom\")); cts.Token.Register(() => log += \"3\"); "
        + "try { cts.Cancel(); return \"ran\"; } catch (AggregateException e) { return $\"{log}|{e.Message}\"; }")]
    // …and the first of them is its InnerException, as a constructed aggregate's is.
    [InlineData("var cts = new CTS(); cts.Token.Register(() => throw new FormatException(\"f\")); "
        + "cts.Token.Register(() => throw new InvalidOperationException(\"g\")); "
        + "try { cts.Cancel(); return \"ran\"; } catch (AggregateException e) { return $\"{e.InnerException.Message}|{e.InnerExceptions.Count}\"; }")]
    // A method group keeps its receiver: the source it names is the one cancelled.
    [InlineData("var outer = new CTS(); var inner = new CTS(); outer.Token.Register(inner.Cancel); outer.Cancel(); "
        + "return $\"{inner.IsCancellationRequested}|{outer.IsCancellationRequested}\";")]
    // A disposed source lets go of its callbacks, and a registration still unregisters once after it.
    [InlineData("var cts = new CTS(); var r = cts.Token.Register(() => { }); cts.Dispose(); "
        + "return $\"{r.Unregister()}|{r.Unregister()}\";")]
    public void Callbacks_RunAsDotNetRunsThem(string program) => SameAsDotNet(program);

    [SkippableTheory]
    [InlineData("var cts = new CTS(); try { cts.Token.ThrowIfCancellationRequested(); return \"ran\"; } "
        + "catch (OperationCanceledException) { return \"canceled\"; }")]
    [InlineData("var cts = new CTS(); cts.Cancel(); try { cts.Token.ThrowIfCancellationRequested(); return \"ran\"; } "
        + "catch (OperationCanceledException e) { return e.Message; }")]
    // An OperationCanceledException IS a SystemException.
    [InlineData("var cts = new CTS(); cts.Cancel(); try { cts.Token.ThrowIfCancellationRequested(); return \"ran\"; } "
        + "catch (SystemException e) when (e is OperationCanceledException) { return \"system\"; }")]
    [InlineData("var cts = new CTS(); cts.Dispose(); try { cts.Cancel(); return \"ran\"; } "
        + "catch (ObjectDisposedException e) { return e.Message; }")]
    // A delay .NET refuses, past the longest it takes as a TimeSpan (4294967294 ms), by either door.
    [InlineData("try { new CTS().CancelAfter(TimeSpan.FromMilliseconds(4294967295d)); return \"accepted\"; } "
        + "catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("try { var c = new CTS(TimeSpan.FromMilliseconds(4294967295d)); return \"accepted\"; } "
        + "catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    // And the longest it takes, its fraction cut, and a month: neither cancels at once.
    [InlineData("var c = new CTS(); c.CancelAfter(TimeSpan.FromMilliseconds(4294967294.9)); var d = new CTS(); "
        + "d.CancelAfter(TimeSpan.FromDays(30)); var now = c.IsCancellationRequested || d.IsCancellationRequested; "
        + "c.Dispose(); d.Dispose(); return $\"{now}\";")]
    // An awaited method that finds its token cancelled.
    [InlineData("async Task<string> F(CT t) { await Task.Yield(); t.ThrowIfCancellationRequested(); return \"done\"; } "
        + "var cts = new CTS(); cts.Cancel(); try { return await F(cts.Token); } catch (OperationCanceledException) { return \"canceled\"; }")]
    public void ACancelledToken_ThrowsWhatDotNetThrows(string program) => SameAsDotNet(program);

    [SkippableTheory]
    // A linked source cancels with any of its tokens, the outer callbacks first.
    [InlineData("var a = new CTS(); var linked = CTS.CreateLinkedTokenSource(a.Token, CT.None); var log = \"\"; "
        + "linked.Token.Register(() => log += \"L\"); a.Token.Register(() => log += \"A\"); a.Cancel(); "
        + "return $\"{linked.IsCancellationRequested}{log}\";")]
    // The default registration, and its constructor: the registration of nothing, whose token is None.
    [InlineData("var r = default(CTR); r.Dispose(); var n = new CTR(); "
        + "return $\"{r.Unregister()}|{r.Token == CT.None}|{r.Token.CanBeCanceled}|{n.Unregister()}|{n.Token == CT.None}\";")]
    // A parameter defaulted to the token that never cancels.
    [InlineData("string F(CT t = default) => $\"{t.CanBeCanceled}{t.IsCancellationRequested}\"; return F();")]
    public void LinkingAndDefaults_AnswerAsDotNetDoes(string program) => SameAsDotNet(program);
}
