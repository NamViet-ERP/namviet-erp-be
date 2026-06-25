using Microsoft.AspNetCore.Mvc;

namespace NamViet.ERP.Api.Controllers.Wms;

internal static class WmsVoucherControllerResponses
{
    public static async Task<ActionResult<TDto>> ExecuteAsync<TDto>(
        Func<Task<TDto?>> action,
        CancellationToken ct = default)
    {
        try
        {
            var item = await action();
            return item is null ? new NotFoundResult() : new OkObjectResult(item);
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }
    }

    public static async Task<IActionResult> ExecuteDeleteAsync(
        Func<Task<bool>> action,
        CancellationToken ct = default)
    {
        try
        {
            var deleted = await action();
            return deleted ? new NoContentResult() : new NotFoundResult();
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }
    }
}
