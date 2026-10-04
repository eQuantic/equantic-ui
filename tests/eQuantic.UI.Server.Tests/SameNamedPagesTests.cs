using System.Text.Json;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Metadata;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Xunit;
using StatelessComponent = eQuantic.UI.Primitives.StatelessComponent;

namespace eQuantic.UI.Server.Tests.SameNamedPages.Admin
{
    /// <summary>One of two pages called <c>Dashboard</c>, in its own namespace.</summary>
    [Page("/same-named/admin")]
    public sealed class Dashboard : StatelessComponent, IHandleMetadata
    {
        public void ConfigureMetadata(SeoBuilder seo) => seo.Title("Admin Dashboard");

        public override VisualNode Build(ComponentContext context) => new Text("the admin dashboard", TypeRole.Heading);
    }
}

namespace eQuantic.UI.Server.Tests.SameNamedPages.Shop
{
    /// <summary>The other <c>Dashboard</c>, in a namespace of its own, as an app with two areas has it.</summary>
    [Page("/same-named/shop")]
    public sealed class Dashboard : StatelessComponent, IHandleMetadata
    {
        public void ConfigureMetadata(SeoBuilder seo) => seo.Title("Shop Dashboard");

        public override VisualNode Build(ComponentContext context) => new Text("the shop dashboard", TypeRole.Heading);
    }
}

namespace eQuantic.UI.Server.Tests
{
    /// <summary>
    /// Two pages with one name in two namespaces are two pages (#514). The server keyed its pages
    /// by the simple name, so the one registered last took it and the other's route rendered it,
    /// with no error and no log line. The endpoint carries the page's TYPE now, to the document and
    /// to the state a client navigation asks for, and the rendering service holds its pages by type.
    /// </summary>
    public class SameNamedPagesTests
    {
        [Theory]
        [InlineData("/same-named/admin", "the admin dashboard", "the shop dashboard")]
        [InlineData("/same-named/shop", "the shop dashboard", "the admin dashboard")]
        public async Task EachRoute_RendersItsOwnPage(string route, string own, string other)
        {
            await using var app = await StartAsync();

            var html = await app.GetTestClient().GetStringAsync(route);

            html.Should().Contain(own).And.NotContain(other);
        }

        [Theory]
        [InlineData("/same-named/admin", "Admin Dashboard")]
        [InlineData("/same-named/shop", "Shop Dashboard")]
        public async Task ANavigationToEachRoute_CarriesItsOwnPagesState(string route, string title)
        {
            await using var app = await StartAsync();

            using var request = new HttpRequestMessage(HttpMethod.Get, route);
            request.Headers.Add("X-EQ-Navigate", "1");
            var response = await app.GetTestClient().SendAsync(request);

            var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            payload.GetProperty("title").GetString().Should().Be(title);
        }

        private static async Task<WebApplication> StartAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                WebRootPath = Directory.CreateTempSubdirectory("eq-same-named-").FullName,
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddUI(options =>
            {
                options.EnableSsr = true;
                options.ScanAssembly(typeof(SameNamedPagesTests).Assembly);
            });
            var app = builder.Build();
            app.MapUI();
            await app.StartAsync();
            return app;
        }
    }
}
