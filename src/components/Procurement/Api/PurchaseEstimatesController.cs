using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/purchasing/estimates")]
[Authorize]
public class PurchaseEstimatesController(IPurchaseEstimateService estimates) : ControllerBase
{
    [HttpGet]
    [RequirePermission("purchasing.estimates.view")]
    public async Task<ActionResult<PurchaseEstimatePagedResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? projectCode = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default) =>
        Ok(await estimates.ListAsync(page, pageSize, projectCode, search, ct));

    [HttpGet("{id:guid}")]
    [RequirePermission("purchasing.estimates.view")]
    public async Task<ActionResult<PurchaseEstimateDetailDto>> GetById(
        Guid id, [FromQuery] int? version, CancellationToken ct)
    {
        var row = await estimates.GetByIdAsync(id, version, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [RequirePermission("purchasing.estimates.create")]
    public async Task<ActionResult<PurchaseEstimateDetailDto>> Create(
        [FromBody] CreatePurchaseEstimateRequest request, CancellationToken ct)
    {
        try
        {
            var created = await estimates.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("purchasing.estimates.update")]
    public async Task<ActionResult<PurchaseEstimateDetailDto>> Update(
        Guid id, [FromBody] UpdatePurchaseEstimateRequest request, CancellationToken ct)
    {
        try
        {
            var row = await estimates.UpdateAsync(id, request, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/share-link")]
    [RequirePermission("purchasing.estimates.update")]
    public async Task<ActionResult<PurchaseEstimateShareLinkDto>> ShareLink(Guid id, CancellationToken ct)
    {
        try
        {
            var row = await estimates.ShareLinkAsync(id, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/revoke-share")]
    [RequirePermission("purchasing.estimates.update")]
    public async Task<ActionResult<PurchaseEstimateDetailDto>> RevokeShare(Guid id, CancellationToken ct)
    {
        try
        {
            var row = await estimates.RevokeShareLinkAsync(id, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/review")]
    [RequirePermission("purchasing.estimates.update")]
    public async Task<ActionResult<PurchaseEstimateDetailDto>> Review(Guid id, CancellationToken ct)
    {
        try
        {
            var row = await estimates.MarkReviewedAsync(id, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/submit-approval")]
    [RequirePermission("purchasing.estimates.update")]
    public async Task<ActionResult<PurchaseEstimateDetailDto>> SubmitApproval(Guid id, CancellationToken ct)
    {
        try
        {
            var row = await estimates.SubmitForApprovalAsync(id, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/approve-l1")]
    [RequirePermission("purchasing.estimates.approve_l1")]
    public async Task<ActionResult<PurchaseEstimateDetailDto>> ApproveL1(
        Guid id, [FromBody] RejectPurchaseEstimateRequest? request, CancellationToken ct)
    {
        try
        {
            var row = await estimates.ApproveLevelAsync(id, 1, request?.Note, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/approve-l2")]
    [RequirePermission("purchasing.estimates.approve_l2")]
    public async Task<ActionResult<PurchaseEstimateDetailDto>> ApproveL2(
        Guid id, [FromBody] RejectPurchaseEstimateRequest? request, CancellationToken ct)
    {
        try
        {
            var row = await estimates.ApproveLevelAsync(id, 2, request?.Note, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/reject")]
    [RequireAnyPermission("purchasing.estimates.approve_l1", "purchasing.estimates.approve_l2")]
    public async Task<ActionResult<PurchaseEstimateDetailDto>> Reject(
        Guid id, [FromBody] RejectPurchaseEstimateRequest? request, CancellationToken ct)
    {
        try
        {
            var row = await estimates.RejectAsync(id, request ?? new RejectPurchaseEstimateRequest(null), ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/convert-plan")]
    [RequirePermission("purchasing.plans.create")]
    public async Task<ActionResult<ConvertPurchaseEstimateResult>> ConvertPlan(Guid id, CancellationToken ct)
    {
        try
        {
            var row = await estimates.ConvertToPlanAsync(id, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
