using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

            if (IsExpectedClientError(ex) && !IsTransientFailure(ex))
                logger.LogWarning(ex, "Request failed for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            else
                logger.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);

            await HandleExceptionAsync(ctx, ex, env.IsDevelopment());
        }
    }

    private static bool IsExpectedClientError(Exception ex) =>
        ex is ValidationException
            or UnauthorizedAccessException
            or ForbiddenException
            or KeyNotFoundException
            or ConcurrencyConflictException
            or StocktakeFreezeConflictException
            or PurchasingConflictException
            or DomainRuleException;

    private static bool IsTransientFailure(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current) as string;
            if (!string.IsNullOrEmpty(sqlState) && (sqlState.StartsWith("23") || sqlState.StartsWith("42")))
            {
                return false;
            }

            var message = current.Message ?? string.Empty;
            if (message.Contains("23505", StringComparison.Ordinal)
                || message.Contains("23503", StringComparison.Ordinal)
                || message.Contains("23502", StringComparison.Ordinal)
                || message.Contains("42703", StringComparison.Ordinal))
            {
                return false;
            }

            var typeName = current.GetType().FullName ?? current.GetType().Name;
            if (typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("Postgres", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("SqlException", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("DbException", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("TimeoutException", StringComparison.OrdinalIgnoreCase)
                || message.Contains("transient failure", StringComparison.OrdinalIgnoreCase)
                || message.Contains("the database is not currently available", StringComparison.OrdinalIgnoreCase)
                || message.Contains("timeout expired", StringComparison.OrdinalIgnoreCase)
                || message.Contains("could not connect", StringComparison.OrdinalIgnoreCase)
                || message.Contains("connection refused", StringComparison.OrdinalIgnoreCase)
                || message.Contains("server closed the connection", StringComparison.OrdinalIgnoreCase)
                || message.Contains("the connection pool has been exhausted", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static (int StatusCode, string Title, string Detail) MapException(Exception ex, bool isDevelopment)
    {
        if (IsTransientFailure(ex))
        {
            return (
                StatusCodes.Status503ServiceUnavailable,
                "Hệ thống tạm gián đoạn",
                "Hệ thống đang tạm gián đoạn. Thử lại sau 1-2 phút. Nếu vẫn lỗi, báo quản trị viên.");
        }

        return ex switch
        {
            ValidationException ve => (400, "Dữ liệu không hợp lệ",
                string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))),
            DbUpdateException dbEx when SequentialCodeHelper.IsUniqueViolation(dbEx) =>
                (StatusCodes.Status409Conflict, "Dữ liệu trùng lặp", "Mã chứng từ hoặc dữ liệu đã tồn tại trong hệ thống."),
            DbUpdateException dbEx when dbEx.InnerException?.Message.Contains("23503") == true =>
                (StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ", "Dữ liệu tham chiếu không tồn tại trong hệ thống."),
            ForbiddenException => (403, "Không đủ quyền",
                string.IsNullOrWhiteSpace(ex.Message)
                    ? "Bạn không có quyền thực hiện thao tác này."
                    : ex.Message),
            UnauthorizedAccessException => (401, "Không có quyền truy cập", "Yêu cầu xác thực không hợp lệ."),
            KeyNotFoundException => (404, "Không tìm thấy dữ liệu", "Không tìm thấy tài nguyên được yêu cầu."),
            ConcurrencyConflictException => (409, "Xung đột cập nhật", "Dữ liệu đã thay đổi. Vui lòng tải lại và thử lại."),
            StocktakeFreezeConflictException => (409, "Kho đang đóng băng kiểm kê", ex.Message),
            PurchasingConflictException => (409, "Xung đột mua hàng", ex.Message),
            DomainRuleException => (422, "Thao tác không hợp lệ", ex.Message),
            OperationCanceledException => (503, "Yêu cầu bị gián đoạn", "Yêu cầu chưa thể hoàn tất."),
            _ => (500, "Lỗi hệ thống",
                isDevelopment
                    ? $"{ex.GetType().Name}: {ex.Message}" +
                      (ex.InnerException is null ? "" : $" | Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}")
                    : "Đã xảy ra lỗi, vui lòng thử lại sau.")
        };
    }

    private static async Task HandleExceptionAsync(HttpContext ctx, Exception ex, bool isDevelopment)
    {
        var (statusCode, title, detail) = MapException(ex, isDevelopment);

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
