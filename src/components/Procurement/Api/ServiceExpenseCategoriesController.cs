using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Purchasing;

public record ServiceExpenseCategoryDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive);

[ApiController]
[Route("api/purchasing/service-categories")]
[Authorize]
public class ServiceExpenseCategoriesController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ServiceExpenseCategoryDto>>> GetAll(CancellationToken ct = default)
    {
        var items = await db.ServiceExpenseCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new ServiceExpenseCategoryDto(
                c.Id,
                c.Code,
                c.Name,
                c.Description,
                c.SortOrder,
                c.IsActive))
            .ToListAsync(ct);

        return Ok(items);
    }
}
