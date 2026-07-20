using Microsoft.AspNetCore.SignalR;

namespace DMS.Hubs
{
    public class NotificationsHub : Hub
    {
        public async Task SendMessageToGroup(string groupName, string message)
        {
            await Clients.Group(groupName).SendAsync("ReceiveMessage", message);
        }

        public async Task AddToGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }
    }
}
