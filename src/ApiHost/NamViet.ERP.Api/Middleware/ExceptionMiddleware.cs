using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.SharedKernel.Common;
using System.Text.Json;

namespace NamViet.ERP.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            await HandleExceptionAsync(ctx, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext ctx, Exception ex)
    {
        var (statusCode, title, detail) = ex switch
        {
            ValidationException ve => (400, "Dữ liệu không hợp lệ",
                string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))),
            UnauthorizedAccessException => (401, "Không có quyền truy cập", ex.Message),
            KeyNotFoundException => (404, "Không tìm thấy dữ liệu", ex.Message),
            ConcurrencyConflictException => (409, "Xung đột cập nhật", ex.Message),
            InvalidOperationException => (422, "Thao tác không hợp lệ", ex.Message),
            _ => (500, "Lỗi hệ thống", "Đã xảy ra lỗi, vui lòng thử lại sau.")
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

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
