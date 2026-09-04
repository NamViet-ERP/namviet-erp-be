using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers.Wms;

internal static class WmsVoucherControllerResponses
{
    public static async Task<ActionResult<TDto>> ExecuteRequiredAsync<TDto>(
        Func<Task<TDto>> action,
        CancellationToken ct = default)
    {
        try
        {
            var item = await action();
            return new OkObjectResult(item);
        }
        catch (DomainRuleException ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }
        catch (InvalidOperationException)
        {
            return new ObjectResult("Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.")
            {
                StatusCode = StatusCodes.Status500InternalServerError,
            };
        }
    }

    public static async Task<ActionResult<TDto>> ExecuteAsync<TDto>(
        Func<Task<TDto?>> action,
        CancellationToken ct = default)
    {
        try
        {
            var item = await action();
            return item is null ? new NotFoundResult() : new OkObjectResult(item);
        }
        catch (DomainRuleException ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }
        catch (InvalidOperationException)
        {
            return new ObjectResult("Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.")
            {
                StatusCode = StatusCodes.Status500InternalServerError,
            };
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
        catch (DomainRuleException ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }
        catch (InvalidOperationException)
        {
            return new ObjectResult("Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.")
            {
                StatusCode = StatusCodes.Status500InternalServerError,
            };
        }
    }
}
