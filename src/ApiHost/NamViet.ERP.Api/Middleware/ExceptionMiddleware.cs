using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.SharedKernel.Common;
using System.Text.Json;

namespace NamViet.ERP.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
{
    private static readonly JsonSerializerOptions ProblemJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(
                "Request cancelled by client for {Method} {Path}",
                ctx.Request.Method,
                ctx.Request.Path);
        }
        catch (Exception ex)
        {
            if (ctx.Response.HasStarted)
            {
                logger.LogWarning(
                    ex,
                    "Exception after response started for {Method} {Path}",
                    ctx.Request.Method,
                    ctx.Request.Path);
                throw;
            }

            if (IsExpectedClientError(ex))
                logger.LogWarning(ex, "Request failed for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            else
                logger.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);

            await HandleExceptionAsync(ctx, ex, env.IsDevelopment());
        }
    }

    private static bool IsExpectedClientError(Exception ex) =>
        ex is ValidationException
            or UnauthorizedAccessException
            or KeyNotFoundException
            or ConcurrencyConflictException
            or StocktakeFreezeConflictException
            or PurchasingConflictException
            or InvalidOperationException;

    private static async Task HandleExceptionAsync(HttpContext ctx, Exception ex, bool isDevelopment)
    {
        var (statusCode, title, detail) = ex switch
        {
            ValidationException ve => (400, "Dữ liệu không hợp lệ",
                string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))),
            UnauthorizedAccessException => (401, "Không có quyền truy cập", "Yêu cầu xác thực không hợp lệ."),
            KeyNotFoundException => (404, "Không tìm thấy dữ liệu", "Không tìm thấy tài nguyên được yêu cầu."),
            ConcurrencyConflictException => (409, "Xung đột cập nhật", "Dữ liệu đã thay đổi. Vui lòng tải lại và thử lại."),
            StocktakeFreezeConflictException => (409, "Kho đang đóng băng kiểm kê", ex.Message),
            PurchasingConflictException => (409, "Xung đột mua hàng", ex.Message),
            InvalidOperationException => (422, "Thao tác không hợp lệ",
                isDevelopment ? ex.Message : "Yêu cầu không thể thực hiện ở trạng thái hiện tại."),
            OperationCanceledException => (503, "Yêu cầu bị gián đoạn", "Yêu cầu chưa thể hoàn tất."),
            _ => (500, "Lỗi hệ thống",
                isDevelopment
                    ? $"{ex.GetType().Name}: {ex.Message}" +
                      (ex.InnerException is null ? "" : $" | Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}")
                    : "Đã xảy ra lỗi, vui lòng thử lại sau.")
        };

        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = ctx.Request.Path
        };

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem, ProblemJsonOptions));
    }
}
