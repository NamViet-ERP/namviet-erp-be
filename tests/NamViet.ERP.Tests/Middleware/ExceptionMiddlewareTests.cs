using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Middleware;

namespace NamViet.ERP.Tests.Middleware;

public class ExceptionMiddlewareTests
{
    private static readonly IHostEnvironment TestEnv = new TestHostEnvironment();

    [Fact]
    public async Task Stocktake_freeze_conflict_exposes_safe_detail()
    {
        const string detail =
            "Kho dự án DA-TEST đang đóng băng kiểm kê (phiếu KK-001). " +
            "Hãy mở băng hoặc duyệt/hủy phiếu kiểm kê trước khi nhập, xuất, chuyển hoặc hoàn trả vật tư.";

        var context = await InvokeAsync(new StocktakeFreezeConflictException(detail));
        using var problem = await ReadProblemAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Equal("Kho đang đóng băng kiểm kê", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(detail, problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Arbitrary_invalid_operation_detail_remains_masked()
    {
        const string unsafeDetail = "Internal workflow state and sensitive identifier.";

        var context = await InvokeAsync(new InvalidOperationException(unsafeDetail));
        using var problem = await ReadProblemAsync(context);
        var detail = problem.RootElement.GetProperty("detail").GetString();

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, context.Response.StatusCode);
        Assert.Equal("Yêu cầu không thể thực hiện ở trạng thái hiện tại.", detail);
        Assert.DoesNotContain(unsafeDetail, detail);
    }

    [Fact]
    public async Task Transient_failure_returns_safe_vietnamese_503()
    {
        const string unsafeDetail =
            "An exception has been raised that is likely due to a transient failure. " +
            "Host=postgres;Username=erp_admin";

        var context = await InvokeAsync(new InvalidOperationException(unsafeDetail));
        using var problem = await ReadProblemAsync(context);
        var detail = problem.RootElement.GetProperty("detail").GetString() ?? string.Empty;
        var title = problem.RootElement.GetProperty("title").GetString() ?? string.Empty;

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("Hệ thống tạm gián đoạn", title);
        Assert.Contains("báo quản trị viên", detail);
        Assert.DoesNotContain("transient failure", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("postgres", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("erp_admin", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(unsafeDetail, detail);
    }

    private static async Task<DefaultHttpContext> InvokeAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionMiddleware(
            _ => Task.FromException(exception),
            NullLogger<ExceptionMiddleware>.Instance,
            TestEnv);

        await middleware.InvokeAsync(context);
        return context;
    }

    private static async Task<JsonDocument> ReadProblemAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
