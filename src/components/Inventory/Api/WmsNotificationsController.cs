using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/warehouse/notifications")]
[Authorize]
public class WmsNotificationsController(
    IWmsNotificationService notifications,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<ActionResult<WmsPendingNotificationsDto>> Pending(CancellationToken ct)
    {
        if (!HasAnyNotificationPermission(currentUser.Permissions))
            return Ok(new WmsPendingNotificationsDto(0, 0, 0, 0, 0, []));

        return Ok(await notifications.GetPendingAsync(currentUser.Permissions, ct));
    }

    private static bool HasAnyNotificationPermission(IReadOnlyList<string> permissions) =>
        permissions.Contains("wms.imports.view:all")
        || permissions.Contains("wms.exports.view:all")
        || permissions.Contains("wms.transfers.view:all")
        || permissions.Contains("wms.stocktakes.view:all")
        || permissions.Contains("purchasing.estimates.approve_l1");
}
