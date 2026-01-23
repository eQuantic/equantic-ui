using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace eQuantic.UI.Server.Hubs;

public class ServerActionHub : Hub
{
    // Basic hub for broadcasting
    public async Task SendEvent(string eventName, object data)
    {
        await Clients.All.SendAsync("ReceiveEvent", eventName, data);
    }
}
