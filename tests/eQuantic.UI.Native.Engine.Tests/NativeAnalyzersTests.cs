using System.Reflection;
using eQuantic.UI.Native.Generators;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The native SDK's generator hands a Photon app the analyzers the web SDK's hands a web app, for the
/// pitfalls a Photon app meets the same way (#1776): an <c>Icon</c> refuses a size off the scale when it
/// is built (EQ3106), and a static keeps the language it was first read in (EQ3107). The files are linked
/// into both generators; this reads what the native one ships, so an unlinked file fails here.
/// </summary>
public class NativeAnalyzersTests
{
    [Fact]
    public void TheNativeGenerator_ShipsTheAnalyzersAPhotonAppNeeds()
    {
        var shipped = typeof(PhotonProgramGenerator).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<DiagnosticAnalyzerAttribute>() is not null)
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)
            .SelectMany(analyzer => analyzer.SupportedDiagnostics)
            .Select(descriptor => descriptor.Id);

        shipped.Should().Contain(["EQ3106", "EQ3107"]);
    }
}
