using System.Collections;
using System.Reflection;
using eQuantic.UI.Server;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The shell template cache is STATIC and every request that serves the app shell reaches it, so it
/// is reached concurrently by construction — a server's whole job is answering more than one person
/// at a time.
/// <para>
/// It was a plain <c>Dictionary</c>, read and then written as two steps. Two hosts starting together
/// in CI took the process down with the exception that names the mistake exactly:
/// </para>
/// <code>
/// System.InvalidOperationException : Operations that change non-concurrent collections must have
/// exclusive access. A concurrent update was performed on this collection …
///    at System.Collections.Generic.Dictionary`2.TryInsert
///    at eQuantic.UI.Server.HtmlTemplateEngine.FromResource(String resourceName, …)
/// </code>
/// <para>
/// Two test hosts starting together is the same shape as two first requests, so this is a
/// production race that a test found rather than a test that is flaky. The pin is the field's TYPE:
/// a race cannot be asserted reliably, and the thing that made it possible can.
/// </para>
/// </summary>
public class TemplateCacheTests
{
    [Fact]
    public void TheShellTemplateCacheIsConcurrent()
    {
        var cache = typeof(HtmlTemplateEngine)
            .GetField("_templateCache", BindingFlags.NonPublic | BindingFlags.Static);

        cache.Should().NotBeNull("the cache is what this is about — rename it and say so here too");
        cache!.FieldType.Should().BeAssignableTo<IDictionary>(
            "it is still a dictionary of templates by key");
        cache.FieldType.Name.Should().StartWith("ConcurrentDictionary",
            "static, written on a cache miss, and reached by every request at once");
    }

    /// <summary>
    /// And the behaviour the type is there for: many callers on one key, all answered, none throwing.
    /// It passes on the old code too when the cache is already warm — which is why the type is the
    /// real pin and this is the conduct of the path.
    /// </summary>
    [Fact]
    public async Task ManyCallersAtOnceAllGetTheShell()
    {
        const string resource = "eQuantic.UI.Server.Templates.app-shell.html";
        var assembly = typeof(HtmlTemplateEngine).Assembly;

        var engines = await Task.WhenAll(Enumerable.Range(0, 64)
            .Select(_ => Task.Run(() => HtmlTemplateEngine.FromResource(resource, assembly))));

        engines.Should().OnlyContain(engine => engine != null).And.HaveCount(64);
    }
}
