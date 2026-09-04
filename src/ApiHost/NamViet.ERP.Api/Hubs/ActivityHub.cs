using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.SharedKernel.Authorization;

namespace NamViet.ERP.Api.Hubs;

/// <summary>
/// Real-time activity feed. Clients call JoinModule("purchasing"|"wms"|…) then receive
/// <see cref="EventCreated"/> payloads for that module group.
/// </summary>
[Authorize]
public class ActivityHub : Hub
{
    public const string EventCreated = "activity.created";

    /// <summary>
    /// Chỉ user có permission xem dữ liệu của phân hệ mới được join group live activity.
    /// "wms" mirror đúng bộ quyền của WmsNotificationsController (chuông thông báo kho);
    /// "purchasing" mirror PurchasingDashboardController.
    /// </summary>
    private static readonly Dictionary<string, string[]> ModulePermissions = new(StringComparer.Ordinal)
    {
        ["purchasing"] = ["purchasing.dashboard.view"],
        ["wms"] =
        [
            "wms.imports.view:all",
            "wms.exports.view:all",
            "wms.transfers.view:all",
            "wms.stocktakes.view:all",
            "purchasing.estimates.approve_l1",
        ],
    };

    public static string GroupName(string module) => $"activity:{module.Trim().ToLowerInvariant()}";

    public Task JoinModule(string module)
    {
        var normalized = module?.Trim().ToLowerInvariant() ?? "";
        if (!ModulePermissions.TryGetValue(normalized, out var requiredAnyOf))
            throw new HubException("Phân hệ không hợp lệ.");

        var user = Context.User;
        var allowed = user is not null
            && (AuthorizationRoleBypass.IsAdmin(user)
                || requiredAnyOf.Any(p => user.HasClaim(PermissionCatalog.ClaimType, p)));
        if (!allowed)
            throw new HubException("Bạn không có quyền theo dõi hoạt động của phân hệ này.");

        return Groups.AddToGroupAsync(Context.ConnectionId, GroupName(normalized));
    }
}
