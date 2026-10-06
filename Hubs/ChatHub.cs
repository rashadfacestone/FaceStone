using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FaceStone.Web.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ILogger<ChatHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = user?.FindFirst(ClaimTypes.Role)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Employee_{userId}");
        }

        if (!string.IsNullOrEmpty(role) && 
            (role.Equals("Accountant", StringComparison.OrdinalIgnoreCase) || 
             role.Equals("CEO", StringComparison.OrdinalIgnoreCase)))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Accountants");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "AllUsers");

        _logger.LogInformation("SignalR connection established for user {UserId} (Role: {Role}) [ConnId: {ConnectionId}]",
            userId ?? "anonymous", role ?? "none", Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (exception != null)
        {
            _logger.LogWarning("SignalR connection {ConnectionId} for user {UserId} disconnected with error: {ErrorMessage}",
                Context.ConnectionId, userId ?? "unknown", exception.Message);
        }
        else
        {
            _logger.LogInformation("SignalR connection {ConnectionId} for user {UserId} disconnected cleanly.",
                Context.ConnectionId, userId ?? "unknown");
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinRoom(string roomName)
    {
        if (!string.IsNullOrEmpty(roomName))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
        }
    }

    public async Task LeaveRoom(string roomName)
    {
        if (!string.IsNullOrEmpty(roomName))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
        }
    }

    public async Task NotifyDepartmentChange(string department, string action)
    {
        await Clients.All.SendAsync("OnDepartmentDataChanged", new
        {
            department,
            action,
            timestamp = DateTime.UtcNow
        });
    }
}
