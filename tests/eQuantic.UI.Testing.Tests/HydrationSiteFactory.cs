using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace eQuantic.UI.Testing.Tests;

/// <summary>The site the cases hydrate, served by Kestrel from its first request, as HydrationAssert needs.</summary>
public sealed class HydrationSiteFactory : WebApplicationFactory<Program>
{
    public HydrationSiteFactory() => UseKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
}
