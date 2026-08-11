using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/purchasing/documents")]
[Authorize]
public class PurchaseDocumentsController(IPurchaseDocumentService documents) : ControllerBase
{
    [HttpGet]
    [RequirePermission("purchasing.documents.view")]
    public async Task<ActionResult<PurchaseDocumentPagedResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? poCode = null,
        [FromQuery] string? projectCode = null,
        [FromQuery] int? supplierId = null,
        [FromQuery] string? docType = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default) =>
        Ok(await documents.ListAsync(page, pageSize, poCode, projectCode, supplierId, docType, search, ct));
}
