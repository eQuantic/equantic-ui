using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Strategies;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// The .NET APIs only a server has: file I/O, networking, OS threading and locking, processes, native
/// interop, runtime IL generation and direct database access. Client code that reaches one is refused
/// (EQ2102–EQ2107, EQ2112), and the bridge is a <c>[ServerAction]</c>.
/// <para>
/// It is a question the HOST-ONLY fence asks (<see cref="HostOnlySymbolExtensions"/>), wherever the
/// conversion is about to write a symbol, so the code checked is exactly the code converted (#816). It
/// was a validator of its own that walked every invocation of the FILE: a <c>[ServerAction]</c>'s
/// body, a <c>[ServerOnly]</c> member and another type declared beside the component were refused as
/// client code, though none of them crosses, while a stateless component was never checked at all, and
/// neither was a construction (<c>new FileInfo(p)</c>), a read (<c>Path.DirectorySeparatorChar</c>) or
/// a method group, each of which was written into the module as a name the browser does not have.
/// </para>
/// <para>
/// Matched on the declaring type's NAMESPACE, a whole one or a type of it, and never on a prefix of its
/// display string: the prefix <c>System.IO</c> was also the start of <c>System.IObservable</c>, whose
/// <c>Subscribe</c> was refused as file-system access. An EXCEPTION of these namespaces is none of
/// them: it is a value the runtime builds and tests by the types it is (#611), so a page may throw and
/// catch an <c>IOException</c> as it always could.
/// </para>
/// </summary>
internal static class ServerPlatform
{
    /// <summary>
    /// One API: a whole namespace (its sub-namespaces included) where <paramref name="TypePrefix"/> is
    /// null, or the types of exactly that namespace whose name starts with it (<c>Thread</c>,
    /// <c>ThreadPool</c>, <c>ThreadLocal</c>, never <c>System.Threading.Tasks</c>).
    /// </summary>
    private sealed record Api(string Namespace, string? TypePrefix, string Code, string Reason);

    /// <summary>The first that matches says which code and reason a member gets.</summary>
    private static readonly Api[] Apis =
    [
        new("System.IO", null, "EQ2112", "file-system access"),
        new("Microsoft.EntityFrameworkCore", null, "EQ2102", "direct database access"),
        new("System.Data", null, "EQ2102", "direct database access"),
        new("System.Net.Http", null, "EQ2103", "HTTP networking"),
        new("System.Net.Sockets", null, "EQ2103", "socket networking"),
        new("System.Threading", "Thread", "EQ2104", "OS threading"),
        new("System.Threading", "Monitor", "EQ2104", "OS-level locking"),
        new("System.Threading", "Mutex", "EQ2104", "OS-level locking"),
        new("System.Threading", "Semaphore", "EQ2104", "OS-level locking"),
        new("System.Diagnostics", "Process", "EQ2105", "spawning processes"),
        new("System.Runtime.InteropServices", null, "EQ2106", "native interop (P/Invoke)"),
        new("System.Reflection.Emit", null, "EQ2107", "runtime IL generation"),
    ];

    /// <summary>
    /// Reports and returns true when <paramref name="type"/> declares an API only a server has, as the
    /// type a member is declared on, the type a construction builds, or the home of an extension.
    /// </summary>
    internal static bool ReportIfServerPlatform(this INamedTypeSymbol? type, SyntaxNode node, ConversionContext context)
    {
        if (type is null || Of(type) is not { } api) return false;
        context.Report(node, ConversionSeverity.Error, api.Code,
            $"'{type.ToDisplayString()}' ({api.Reason}) is not available in a client component. Move this to a [ServerAction].");
        context.Refused.Add(node);
        return true;
    }

    private static Api? Of(INamedTypeSymbol type)
    {
        if (ExceptionTypes.Is(type)) return null;
        var outermost = type;
        while (outermost.ContainingType is { } owner) outermost = owner;
        var space = outermost.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        foreach (var api in Apis)
        {
            var matches = api.TypePrefix is null
                ? space == api.Namespace || space.StartsWith(api.Namespace + ".", StringComparison.Ordinal)
                : space == api.Namespace && outermost.Name.StartsWith(api.TypePrefix, StringComparison.Ordinal);
            if (matches) return api;
        }
        return null;
    }
}
