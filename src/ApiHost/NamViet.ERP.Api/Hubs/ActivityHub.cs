using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NamViet.ERP.Api.Hubs;

/// <summary>
/// Real-time activity feed. Clients call JoinModule("purchasing"|"wms"|…) then receive
/// <see cref="EventCreated"/> payloads for that module group.
/// </summary>
[Authorize]
public class ActivityHub : Hub
{
    public const string EventCreated = "activity.created";

    public static string GroupName(string module) => $"activity:{module.Trim().ToLowerInvariant()}";

    public Task JoinModule(string module)
    {
        if (string.IsNullOrWhiteSpace(module))
            return Task.CompletedTask;
        return Groups.AddToGroupAsync(Context.ConnectionId, GroupName(module));
    }

    public Task LeaveModule(string module)
    {
        if (string.IsNullOrWhiteSpace(module))
            return Task.CompletedTask;
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(module));
    }
}
