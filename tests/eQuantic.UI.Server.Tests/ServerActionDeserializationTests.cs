using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// Verifies the Server Action deserialization allow-list: complex types are only accepted
/// when declared in the application's scanned (or explicitly opted-in) assemblies. Framework,
/// system and third-party types are rejected by default.
/// </summary>
public class ServerActionDeserializationTests
{
    private sealed class SampleDto
    {
        public string Name { get; set; } = "";
    }

    private enum SampleEnum { A, B }

    private static readonly MethodInfo IsAllowedTypeMethod =
        typeof(ServerActionsMiddleware).GetMethod("IsAllowedType", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static ServerActionsMiddleware CreateMiddleware(bool scanTestAssembly)
    {
        var options = new UIOptions();
        if (scanTestAssembly)
        {
            options.ScanAssembly(typeof(ServerActionDeserializationTests).Assembly);
        }

        return new ServerActionsMiddleware(
            next: _ => Task.CompletedTask,
            registry: null!,
            serviceProvider: null!,
            authorizationService: null!,
            options: options,
            logger: NullLogger<ServerActionsMiddleware>.Instance);
    }

    private static bool IsAllowed(ServerActionsMiddleware middleware, Type type) =>
        (bool)IsAllowedTypeMethod.Invoke(middleware, new object[] { type })!;

    [Fact]
    public void Primitives_Collections_And_Enums_AreAlwaysAllowed()
    {
        var mw = CreateMiddleware(scanTestAssembly: false);

        IsAllowed(mw, typeof(string)).Should().BeTrue();
        IsAllowed(mw, typeof(int)).Should().BeTrue();
        IsAllowed(mw, typeof(int?)).Should().BeTrue();
        IsAllowed(mw, typeof(Guid)).Should().BeTrue();
        IsAllowed(mw, typeof(List<string>)).Should().BeTrue();
        IsAllowed(mw, typeof(Dictionary<string, int>)).Should().BeTrue();
        IsAllowed(mw, typeof(SampleEnum)).Should().BeTrue();
    }

    [Fact]
    public void ApplicationDto_IsAllowed_WhenAssemblyScanned()
    {
        var mw = CreateMiddleware(scanTestAssembly: true);

        IsAllowed(mw, typeof(SampleDto)).Should().BeTrue();
        IsAllowed(mw, typeof(List<SampleDto>)).Should().BeTrue();
    }

    [Fact]
    public void ApplicationDto_IsRejected_WhenAssemblyNotAllowed()
    {
        var mw = CreateMiddleware(scanTestAssembly: false);

        IsAllowed(mw, typeof(SampleDto)).Should().BeFalse();
    }

    [Fact]
    public void FrameworkType_IsRejected()
    {
        var mw = CreateMiddleware(scanTestAssembly: true);

        // Uri is a complex System type not in the application's assemblies.
        IsAllowed(mw, typeof(Uri)).Should().BeFalse();
    }
}
