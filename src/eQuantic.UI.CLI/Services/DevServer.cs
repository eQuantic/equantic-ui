using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace eQuantic.UI.CLI.Services;

public class DevServer
{
    private static readonly List<WebSocket> _clients = new();
    private WebApplication? _app;

    public async Task StartAsync(string webRoot, int port)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders(); // Keep CLI clean
        
        builder.WebHost.ConfigureKestrel(options => 
        {
            options.ListenLocalhost(port);
        });

        var app = builder.Build();
        _app = app;

        app.UseWebSockets();

        // HMR Endpoint
        app.Map("/hmr", async (HttpContext context) =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                using var ws = await context.WebSockets.AcceptWebSocketAsync();
                lock (_clients) _clients.Add(ws);
                Console.WriteLine("🔌 HMR Client Connected");

                try
                {
                    var buffer = new byte[1024];
                    while (ws.State == WebSocketState.Open)
                    {
                        // Keep connection alive, ignore incoming messages for now
                        await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    }
                }
                catch 
                { 
                    // Client disconnected 
                }
                finally
                {
                    lock (_clients) _clients.Remove(ws);
                }
            }
            else
            {
                context.Response.StatusCode = 400;
            }
        });

        // Serve Static Files
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(webRoot),
            RequestPath = ""
        });
        
        // Serve index.html for root
        app.MapGet("/", async context => 
        {
            var indexPath = Path.Combine(webRoot, "index.html");
            if (File.Exists(indexPath))
            {
                context.Response.ContentType = "text/html";
                await context.Response.SendFileAsync(indexPath);
            }
            else
            {
                await context.Response.WriteAsync("Index.html not found (Build pending...)");
            }
        });

        await app.StartAsync();
        Console.WriteLine($"✅ Dev Server running at http://localhost:{port}");
    }

    public async Task BroadcastUpdateAsync(string type, string payload = "")
    {
        var message = JsonSerializer.Serialize(new { type, payload });
        var bytes = Encoding.UTF8.GetBytes(message);
        var arraySegment = new ArraySegment<byte>(bytes);

        List<WebSocket> activeClients;
        lock (_clients) 
        {
            activeClients = _clients.Where(c => c.State == WebSocketState.Open).ToList();
        }

        if (activeClients.Count > 0)
        {
             // Console.WriteLine($"Broadcast: {type}");
             foreach (var client in activeClients)
             {
                 try 
                 {
                     await client.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                 }
                 catch { /* Ignore send errors */ }
             }
        }
    }

    public async Task StopAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
