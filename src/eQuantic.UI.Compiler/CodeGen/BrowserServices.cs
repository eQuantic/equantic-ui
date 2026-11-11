using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The key a resolution in the browser names, and the one writer of it (#1335). Every place the
/// browser resolves a service writes its key here: a component's constructor dependency, a
/// <c>GetService&lt;T&gt;()</c> on any receiver, <c>CapabilityScope.Resolve&lt;T&gt;()</c>.
/// <para>
/// A key whose service the app registered (<see cref="ServiceRegistrations"/>) is written as
/// <c>$eq.services.provide('IWalletLedger', () =&gt; new WalletLedger(), 'singleton')</c>, which
/// registers the implementation when nothing has yet and answers the key, so the resolution around
/// it reads <c>$eq.services.resolve($eq.services.provide(…))</c>. EVERY site carries it, and that is
/// what makes the order of resolution irrelevant: whichever module resolves the service first
/// registers it, and every later one finds the same singleton. A registration written in one module
/// that boots first would have left the others depending on which page the browser opened.
/// </para>
/// <para>
/// The implementation is built as the container builds it: its widest public constructor, with each
/// dependency resolved the same way. Naming it in the output imports it, so it crosses as any type a
/// page reaches does, and the client boundary judges it where it is compiled: a class that reads a
/// file fails the build there, never at hydration.
/// </para>
/// </summary>
internal static class BrowserServices
{
    /// <summary>
    /// The key of <paramref name="service"/>, written for a resolution at <paramref name="at"/>.
    /// </summary>
    /// <param name="key">The interface's simple name, which the registry is keyed by.</param>
    /// <param name="service">The interface, when the site's model knows it.</param>
    /// <param name="requiredBy">The type that cannot work without the service (a constructor
    /// parameter that is not nullable), or null where its absence is an answer.</param>
    /// <param name="at">The resolution, where a diagnostic about it points.</param>
    /// <param name="context">The conversion, which carries the registrations and takes the
    /// diagnostics and the names the output introduces.</param>
    public static string Key(string key, ITypeSymbol? service, string? requiredBy, SyntaxNode at, ConversionContext context) =>
        KeyOf(key, service, requiredBy, at, context, []);

    private static string KeyOf(string key, ITypeSymbol? service, string? requiredBy, SyntaxNode at,
        ConversionContext context, HashSet<string> building)
    {
        var quoted = $"'{key}'";
        var registrations = context.AppServices;
        if (!registrations.WasRead) return quoted;

        var registration = registrations.Find(key);
        if (registration is { Implementation: { } implementation })
        {
            // A registration that needs itself, through any number of others, has no first instance.
            if (!building.Add(key))
            {
                context.Report(at, ConversionSeverity.Error, "EQ2116",
                    $"{key} cannot be built in the browser: its registration depends on itself through "
                    + $"{string.Join(" → ", building.Append(key))}.");
                return quoted;
            }
            var construction = Construction(implementation, key, at, context, building);
            building.Remove(key);
            if (construction is null) return quoted;
            context.UsedHelpers.Add(Eq.Import);
            return $"{Eq.ProvideService}({quoted}, () => {construction}, '{registration.Lifetime}')";
        }

        // Nothing the browser can build. Where the component declared it cannot work without one,
        // that is a page that fails at hydration, and the build says so now. A service of the SDK is
        // the runtime's to provide, and one from a package is out of the build's sight.
        if (requiredBy is not null && service is not null && registrations.IsAppsOwn(service))
            context.Report(at, ConversionSeverity.Error, "EQ2116", Missing(key, requiredBy, registrations.All(key)));
        return quoted;
    }

    /// <summary>
    /// <c>new Implementation(…)</c> as the container builds it, or null when the browser cannot,
    /// which it has reported. The widest public constructor is the one the container takes, and each
    /// of its dependencies is resolved here as any other.
    /// </summary>
    private static string? Construction(INamedTypeSymbol implementation, string key, SyntaxNode at,
        ConversionContext context, HashSet<string> building)
    {
        var constructor = ServiceRegistrations.ConstructorOf(implementation);
        if (constructor is null)
        {
            context.Report(at, ConversionSeverity.Error, "EQ2116",
                $"{key} cannot be built in the browser: {implementation.Name} has no public constructor.");
            return null;
        }

        var arguments = new List<string>();
        foreach (var parameter in constructor.Parameters)
        {
            if (CapabilityRule.IsDependency(parameter.Type))
            {
                var dependency = KeyOf(parameter.Type.Name, parameter.Type,
                    CapabilityRule.IsRequired(parameter) ? implementation.Name : null, at, context, building);
                arguments.Add($"{Eq.ResolveService}({dependency})");
            }
            else if (parameter.HasExplicitDefaultValue || parameter.IsParams)
            {
                // Its own default, which the twin's parameter carries.
                arguments.Add("undefined");
            }
            else
            {
                context.Report(at, ConversionSeverity.Error, "EQ2116",
                    $"{key} cannot be built in the browser: {implementation.Name}'s constructor takes "
                    + $"{parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {parameter.Name}, "
                    + "which no container provides. Give it a default, or take it as a service of the app's.");
                return null;
            }
        }
        while (arguments.Count > 0 && arguments[^1] == "undefined") arguments.RemoveAt(arguments.Count - 1);

        var twin = implementation.TwinReference();
        context.UsedAppTypes.Add(twin);
        return $"new {twin}({string.Join(", ", arguments)})";
    }

    /// <summary>The message for a service a component cannot work without and the browser cannot
    /// build: what was found, if anything, and the line to write.</summary>
    private static string Missing(string key, string requiredBy, IReadOnlyList<ServiceRegistration> found)
    {
        var write = $"builder.Services.AddSingleton<{key}, TImplementation>() (or AddScoped, AddTransient)";
        if (found.Count == 0)
            return $"{requiredBy} needs {key} in the browser, and the app registers none. Register it in Program.cs with "
                + $"{write}, and the browser builds the same class; or declare the parameter as {key}? if {requiredBy} "
                + "can work without it.";
        if (found.All(registration => !registration.Crosses))
            return $"{requiredBy} needs {key} in the browser, and its registration hands the container something only the "
                + $"server can run: {string.Join(", ", found.Select(registration => registration.Describe()))}. Register the "
                + $"class itself, {write}, for the browser to build it.";
        return $"{requiredBy} needs {key} in the browser, and the app registers it more than once: "
            + $"{string.Join(", ", found.Select(registration => registration.Describe()))}. Which one the container keeps "
            + "depends on the order the code runs in, which the build cannot tell. Keep one registration.";
    }
}
