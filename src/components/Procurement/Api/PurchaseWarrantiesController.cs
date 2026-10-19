using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/purchasing/warranties")]
[Authorize]
public class PurchaseWarrantiesController(IPurchaseWarrantyService warranties) : ControllerBase
{
    [HttpGet]
    [RequirePermission("purchasing.warranties.view")]
    public async Task<ActionResult<IReadOnlyList<PurchaseWarrantyDto>>> List(
        [FromQuery] string? projectCode,
        [FromQuery] string? materialCode,
        [FromQuery] int? supplierId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken ct)
        => Ok(await warranties.ListAsync(projectCode, materialCode, supplierId, status, search, ct));

    [HttpGet("claims")]
    [RequirePermission("purchasing.warranties.view")]
    public async Task<ActionResult<IReadOnlyList<PurchaseWarrantyClaimListDto>>> ListClaims(
        [FromQuery] string? kind,
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken ct)
        => Ok(await warranties.ListClaimsAsync(kind, status, search, ct));

    [HttpGet("{id:guid}")]
    [RequirePermission("purchasing.warranties.view")]
    public async Task<ActionResult<PurchaseWarrantyDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var row = await warranties.GetByIdAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("purchasing.warranties.update")]
    public async Task<ActionResult<PurchaseWarrantyDetailDto>> Update(
        Guid id,
        [FromBody] UpdatePurchaseWarrantyRequest request,
        CancellationToken ct)
    {
        try
        {
            var row = await warranties.UpdateAsync(id, request, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/claims")]
    [RequirePermission("purchasing.warranties.create")]
    public async Task<ActionResult<PurchaseWarrantyClaimDto>> CreateClaim(
        Guid id,
        [FromBody] CreatePurchaseWarrantyClaimRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await warranties.CreateClaimAsync(id, request, ct));
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("claims/{claimId:guid}/submit")]
    [RequirePermission("purchasing.warranties.create")]
    public async Task<ActionResult<PurchaseWarrantyClaimDto>> SubmitClaim(Guid claimId, CancellationToken ct)
    {
        try
        {
            var row = await warranties.SubmitClaimAsync(claimId, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("claims/{claimId:guid}/close")]
    [RequirePermission("purchasing.warranties.update")]
    public async Task<ActionResult<PurchaseWarrantyClaimDto>> CloseClaim(
        Guid claimId,
        [FromBody] ClosePurchaseWarrantyClaimRequest request,
        CancellationToken ct)
    {
        try
        {
            var row = await warranties.CloseClaimAsync(claimId, request, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("sync-from-receipts")]
    [RequirePermission("purchasing.warranties.update")]
    public async Task<ActionResult<SyncPurchaseWarrantiesResult>> SyncFromReceipts(CancellationToken ct)
        => Ok(await warranties.SyncFromReceivedPoLinesAsync(ct));
}
