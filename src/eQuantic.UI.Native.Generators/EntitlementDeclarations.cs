using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI.Native.Generators;

/// <summary>
/// Reads <c>builder.Entitlements.Require…()</c> out of <c>Program.cs</c>, so the app states what the
/// system must permit in C# and the generator writes the assembly declaration the build reads.
/// <para>
/// The same shape as <see cref="CapabilityDeclarations"/> — one idiom for platform facts, whether
/// the thing being asked is a user (a capability) or the operating system (an entitlement).
/// </para>
/// </summary>
internal static class EntitlementDeclarations
{
    private const string BuilderType = "eQuantic.UI.Native.Hosting.PhotonEntitlementsBuilder";

    internal static readonly DiagnosticDescriptor KeyMustBeConstant = new(
        "EQ3004", "An entitlement's key must be a constant",
        "The entitlement passed here is built at run time, and entitlements are signed into the app "
        + "at BUILD time — so this one would never reach the signature. Use PhotonEntitlements.X, a "
        + "literal, or a const string.",
        "eQuantic.UI", DiagnosticSeverity.Error, isEnabledByDefault: true);

    internal static bool MightDeclare(SyntaxNode node) =>
        node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name },
        } && name.StartsWith("Require", System.StringComparison.Ordinal);

    /// <summary>The entitlement key, or null when this call is not one of ours. The location comes
    /// back with it so a non-constant key can be reported where it was written.</summary>
    internal static (string? Key, Location Location)? Read(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetOperation(context.Node) is not IInvocationOperation invocation)
            return null;
        if (invocation.TargetMethod.ContainingType?.ToDisplayString() != BuilderType) return null;

        var location = context.Node.GetLocation();

        // The NAMED methods carry the key in their own name (RequireJit → the JIT key), so there is
        // nothing to evaluate; the general Require(string) has to hold a constant, for the same
        // reason a capability's reason does — the value is baked into a file at build time.
        if (invocation.Arguments.Length == 0)
            return (KeyFor(invocation.TargetMethod.Name), location);

        return invocation.Arguments[0].Value.ConstantValue is { HasValue: true, Value: string key }
            ? (key, location)
            : (null, location);
    }

    /// <summary>The key each named method stands for. Spelled here rather than read off the
    /// constant it calls, because a source generator sees the CALL and not the method body.</summary>
    private static string? KeyFor(string method) => method switch
    {
        "RequireJit" => "com.apple.security.cs.allow-jit",
        "RequireUnsignedExecutableMemory" => "com.apple.security.cs.allow-unsigned-executable-memory",
        "RequireForeignLibraries" => "com.apple.security.cs.disable-library-validation",
        "RequireUserSelectedFiles" => "com.apple.security.files.user-selected.read-write",
        "RequireNetworkClient" => "com.apple.security.network.client",
        "RequireAppSandbox" => "com.apple.security.app-sandbox",
        _ => null,
    };
}
