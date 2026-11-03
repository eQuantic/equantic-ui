using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// A Server Action runs in the culture of the page that called it (#580). The endpoint has no culture
/// segment, so the route that rendered <c>/pt-BR/playground</c> answered nothing there, and the cookie
/// or <c>Accept-Language</c> chose the language of the handler's answer. The runtime names the page's
/// culture in the query keys ASP.NET's <c>QueryStringRequestCultureProvider</c> reads, ahead of the
/// cookie, so the app's own localization middleware applies it, whichever overload wired it: the
/// lambda the template writes sees nothing registered through DI, which is why no provider of the SDK's
/// could carry this.
/// </summary>
public class ServerActionCultureTests
{
    private const string Body = """{"actionId":"CultureActions/Read","arguments":[]}""";

    public sealed class CultureActions : StatelessComponent
    {
        [ServerAction]
        public Task<string> Read() => Task.FromResult(
            $"{CultureInfo.CurrentCulture.Name}|{CultureInfo.CurrentUICulture.Name}|{1234.5.ToString("N1")}");

        public override VisualNode Build(ComponentContext context) => new Column(gap: 0);
    }

    private static async Task<string> Invoke(bool cultureRoutes, string query, params (string Name, string Value)[] headers)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            options.ScanAssembly(typeof(ServerActionCultureTests).Assembly);
            if (cultureRoutes) options.UseCultureRoutes("en", "pt-BR", "es");
        });
        builder.Services.AddSingleton<IServerActionAuthorizationService, AlwaysAllowed>();

        await using var app = builder.Build();
        if (cultureRoutes) app.UseRequestLocalization();
        else
            app.UseRequestLocalization(options =>
            {
                options.AddSupportedUICultures("en", "pt-BR", "es");
                options.AddSupportedCultures("en", "pt-BR", "es");
            });
        app.UseServerActions();
        await app.StartAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/_equantic/actions" + query)
        {
            Content = new StringContent(Body, Encoding.UTF8, "application/json"),
        };
        foreach (var (name, value) in headers) request.Headers.TryAddWithoutValidation(name, value);

        var response = await app.GetTestClient().SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement.GetProperty("result").GetString()!;
    }

    private sealed class AlwaysAllowed : IServerActionAuthorizationService
    {
        public Task<ServerActionAuthorizationResult> AuthorizeAsync(HttpContext context, ServerActionDescriptor descriptor) =>
            Task.FromResult(ServerActionAuthorizationResult.Success());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheCultureThePageNames_BeatsTheBrowser(bool cultureRoutes)
    {
        var answer = await Invoke(cultureRoutes, "?culture=pt-BR&ui-culture=pt-BR", ("Accept-Language", "en"));

        answer.Should().Be("pt-BR|pt-BR|1.234,5", "a Portuguese page is answered in Portuguese, its numbers included");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheCultureThePageNames_BeatsTheCookie(bool cultureRoutes)
    {
        var cookie = CookieRequestCultureProvider.DefaultCookieName + "=" +
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture("es")).Replace("|", "%7C");

        var answer = await Invoke(cultureRoutes, "?culture=pt-BR&ui-culture=pt-BR", ("Cookie", cookie));

        answer.Should().StartWith("pt-BR|pt-BR|");
    }

    [Fact]
    public async Task TheFormatAndTheLanguage_AreNamedApart()
    {
        var answer = await Invoke(cultureRoutes: false, "?culture=en&ui-culture=pt-BR", ("Accept-Language", "es"));

        answer.Should().StartWith("en|pt-BR|");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ACultureTheAppDoesNotSupport_IsRefused_AndTheNextProviderDecides(bool cultureRoutes)
    {
        var answer = await Invoke(cultureRoutes, "?culture=de&ui-culture=de", ("Accept-Language", "pt-BR"));

        answer.Should().StartWith("pt-BR|pt-BR|", "the name comes from a URL anyone can write");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ACallThatNamesNoCulture_KeepsTheCookieAndTheBrowser(bool cultureRoutes)
    {
        var answer = await Invoke(cultureRoutes, "", ("Accept-Language", "es"));

        answer.Should().StartWith("es|es|");
    }
}
