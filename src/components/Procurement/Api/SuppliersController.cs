using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/suppliers")]
[Authorize]
public class SuppliersController(
    WarehouseDbContext db,
    ISupplierGroupService groups,
    ICurrentUser currentUser,
    IFileStorage? fileStorage = null,
    ISupplierExcelService? supplierExcel = null,
    ISupplierTierEvaluationService? tierEvaluation = null,
    ISupplierDirectoryService? directoryService = null) : ControllerBase
{
    private ISupplierExcelService ExcelService => supplierExcel ?? new SupplierExcelService(db);
    private ISupplierTierEvaluationService TierService => tierEvaluation ?? new SupplierTierEvaluationService(db, currentUser);
    private ISupplierDirectoryService Directory => directoryService ?? new SupplierDirectoryService(db, currentUser, fileStorage);

    [HttpGet("{id:int}/groups")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<IReadOnlyList<SupplierGroupRefDto>>> GetGroups(
        int id, CancellationToken ct) =>
        Ok(await groups.ListGroupsOfSupplierAsync(id, ct));

    [HttpPut("{id:int}/groups")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<IReadOnlyList<SupplierGroupRefDto>>> SetGroups(
        int id, [FromBody] SetSupplierGroupsRequest request, CancellationToken ct) =>
        Ok(await groups.SetGroupsOfSupplierAsync(id, request, ct));

    [HttpGet("sourcing-kpi")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<SourcingKpiSummaryDto>> GetSourcingKpi(CancellationToken ct) =>
        Ok(await TierService.GetSourcingKpiAsync(ct));

    [HttpPost("{id:int}/tier-evaluation")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<SupplierTierEvaluationDto>> SubmitTierEvaluation(
        int id, [FromBody] SubmitTierEvaluationRequest request, CancellationToken ct)
    {
        var outcome = await TierService.SubmitTierEvaluationAsync(id, request, ct);
        if (outcome.NotFound)
            return NotFound(new { message = outcome.ErrorMessage });
        if (outcome.ErrorMessage is not null)
            return BadRequest(new { message = outcome.ErrorMessage });
        return Ok(outcome.Evaluation);
    }

    [HttpGet("{id:int}/grade-history")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<IReadOnlyList<SupplierTierEvaluationDto>>> GetGradeHistory(
        int id, CancellationToken ct)
    {
        var items = await TierService.GetGradeHistoryAsync(id, ct);
        return items is null
            ? NotFound(new { message = $"Không tìm thấy nhà cung cấp id={id}." })
            : Ok(items);
    }

    [HttpGet]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<PagedResult<SupplierDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? rating = null,
        [FromQuery] int? groupId = null,
        [FromQuery] bool? isBlacklisted = null,
        [FromQuery] string? approvalStatus = null,
        CancellationToken ct = default) =>
        Ok(await Directory.GetAllAsync(page, pageSize, search, rating, groupId, isBlacklisted, approvalStatus, ct));

    [HttpGet("next-code")]
    [RequirePermission("wms.suppliers.create")]
    public async Task<ActionResult<SupplierNextCodeDto>> NextCode(CancellationToken ct) =>
        Ok(new SupplierNextCodeDto(await Directory.NextSupplierCodeAsync(ct)));

    [HttpGet("template-excel")]
    [RequirePermission("wms.suppliers.view:all")]
    public IActionResult DownloadExcelTemplate() =>
        File(
            ExcelService.BuildExcelTemplate(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Mau_Nhap_NhaCungCap.xlsx");

    [HttpPost("preview-excel")]
    [RequirePermission("wms.suppliers.create")]
    public async Task<ActionResult<SupplierImportPreviewResponseDto>> PreviewExcel(
        IFormFile file,
        [FromQuery] string? sheetName = null,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Vui lòng chọn file Excel danh sách NCC.");

        sheetName = ResolveSheetName(sheetName);
        using var stream = file.OpenReadStream();
        var (response, errorMessage) = await ExcelService.PreviewExcelAsync(stream, sheetName, ct);
        return errorMessage is not null ? BadRequest(errorMessage) : Ok(response);
    }

    [HttpPost("import-excel")]
    [RequirePermission("wms.suppliers.create")]
    public async Task<ActionResult<SupplierImportResultDto>> ImportExcel(
        IFormFile file,
        [FromQuery] string? sheetName = null,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Vui lòng chọn file Excel danh sách NCC.");

        sheetName = ResolveSheetName(sheetName);
        using var stream = file.OpenReadStream();
        var (result, errorMessage) = await ExcelService.ImportExcelAsync(stream, sheetName, ct);
        return errorMessage is not null ? BadRequest(errorMessage) : Ok(result);
    }

    [HttpGet("blacklist")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<IReadOnlyList<SupplierBlacklistDto>>> ListBlacklist(CancellationToken ct) =>
        Ok(await Directory.ListBlacklistAsync(ct));

    [HttpGet("{id:int}")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<SupplierDto>> GetById(int id, CancellationToken ct)
    {
        var dto = await Directory.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [RequirePermission("wms.suppliers.create")]
    public async Task<ActionResult<SupplierDto>> Create([FromBody] UpsertSupplierRequest req, CancellationToken ct) =>
        ToActionResult(await Directory.CreateAsync(req, ct));

    [HttpPut("{id:int}")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertSupplierRequest req, CancellationToken ct)
    {
        var outcome = await Directory.UpdateAsync(id, req, ct);
        return outcome.ErrorKind switch
        {
            SupplierServiceErrorKind.None => NoContent(),
            SupplierServiceErrorKind.NotFound => NotFound(),
            SupplierServiceErrorKind.ValidationFields => BadRequest(new { errors = outcome.FieldErrors }),
            _ => BadRequest(outcome.Message),
        };
    }

    [HttpPost("{id:int}/approve")]
    [RequirePermission("wms.suppliers.approve")]
    public async Task<ActionResult<SupplierDto>> Approve(int id, CancellationToken ct) =>
        ToActionResult(await Directory.ApproveAsync(id, ct));

    [HttpPost("{id:int}/reject")]
    [RequirePermission("wms.suppliers.approve")]
    public async Task<ActionResult<SupplierDto>> Reject(int id, CancellationToken ct) =>
        ToActionResult(await Directory.RejectAsync(id, ct));

    [HttpPost("{id:int}/blacklist")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<SupplierDto>> AddToBlacklist(
        int id, [FromBody] BlacklistSupplierRequest request, CancellationToken ct) =>
        ToActionResult(await Directory.AddToBlacklistAsync(id, request, ct));

    [HttpDelete("{id:int}/blacklist")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<SupplierDto>> RemoveFromBlacklist(int id, CancellationToken ct) =>
        ToActionResult(await Directory.RemoveFromBlacklistAsync(id, ct));

    [HttpDelete("{id:int}")]
    [RequirePermission("wms.suppliers.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await Directory.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("{id:int}/attachments")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<IReadOnlyList<SupplierAttachmentDto>>> ListAttachments(
        int id, CancellationToken ct) =>
        ToActionResult(await Directory.ListAttachmentsAsync(id, ct), wrapMessage: true);

    [HttpPost("{id:int}/attachments")]
    [RequirePermission("wms.suppliers.update")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<SupplierAttachmentDto>> UploadAttachment(
        int id,
        IFormFile file,
        [FromForm] string docType = "other",
        CancellationToken ct = default) =>
        ToActionResult(await Directory.UploadAttachmentAsync(id, file, docType, ct), wrapMessage: true);

    [HttpGet("{id:int}/attachments/{attachmentId:guid}/download")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<IActionResult> DownloadAttachment(int id, Guid attachmentId, CancellationToken ct)
    {
        var outcome = await Directory.DownloadAttachmentAsync(id, attachmentId, ct);
        return outcome.ErrorKind switch
        {
            SupplierServiceErrorKind.None => File(outcome.Data!.Stream, outcome.Data.ContentType, outcome.Data.FileName),
            SupplierServiceErrorKind.NotFound => NotFound(new { message = outcome.Message }),
            SupplierServiceErrorKind.StorageUnavailable => StatusCode(500, new { message = outcome.Message }),
            _ => BadRequest(new { message = outcome.Message }),
        };
    }

    [HttpDelete("{id:int}/attachments/{attachmentId:guid}")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<IActionResult> DeleteAttachment(int id, Guid attachmentId, CancellationToken ct)
    {
        var outcome = await Directory.DeleteAttachmentAsync(id, attachmentId, ct);
        return outcome.ErrorKind == SupplierServiceErrorKind.NotFound
            ? NotFound(new { message = outcome.Message })
            : NoContent();
    }

    private string? ResolveSheetName(string? sheetName)
    {
        if (string.IsNullOrWhiteSpace(sheetName)
            && Request?.HasFormContentType == true
            && Request.Form.TryGetValue("sheetName", out var formSheet))
        {
            return formSheet.ToString();
        }
        return sheetName;
    }

    private ActionResult<T> ToActionResult<T>(SupplierServiceOutcome<T> outcome, bool wrapMessage = false) =>
        outcome.ErrorKind switch
        {
            SupplierServiceErrorKind.None => Ok(outcome.Data),
            SupplierServiceErrorKind.ValidationFields => BadRequest(new { errors = outcome.FieldErrors }),
            SupplierServiceErrorKind.NotFound => outcome.Message is not null
                ? NotFound(new { message = outcome.Message })
                : NotFound(),
            SupplierServiceErrorKind.Conflict => Conflict(outcome.Message),
            SupplierServiceErrorKind.StorageUnavailable => StatusCode(500, new { message = outcome.Message }),
            _ => wrapMessage
                ? BadRequest(new { message = outcome.Message })
                : BadRequest(outcome.Message),
        };
}
