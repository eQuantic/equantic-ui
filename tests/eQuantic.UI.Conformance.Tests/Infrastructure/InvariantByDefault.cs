using System.Globalization;
using System.Runtime.CompilerServices;

namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// The suite runs in the invariant culture unless a case names another, whatever the host's locale
/// is, and from before its first test. It was set by the static constructor of the .NET evaluator,
/// which ran on whichever thread first touched it: a test that rendered through the server before any
/// case ran formatted in the host's culture (#471). A case that names a culture runs in it
/// (<see cref="DotNetEvaluator.EvaluateToJson"/>).
/// </summary>
internal static class InvariantByDefault
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }
}
