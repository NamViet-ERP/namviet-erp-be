using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Api.Controllers.Wms;
using NamViet.ERP.Api.Infrastructure.RateLimiting;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Hr.Infrastructure;
using NamViet.ERP.Modules.Identity.Infrastructure;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using Npgsql;

namespace NamViet.ERP.Tests.Security;

public sealed class BackendHardeningPhase2Tests
{
    [Theory]
    [InlineData("disguised.jpg", "image/jpeg", "<html><script>alert(1)</script></html>")]
    [InlineData("disguised.png", "image/png", "<!doctype html><body>evil</body></html>")]
    [InlineData("disguised.gif", "image/gif", "<svg onload=alert(1)>")]
    [InlineData("plain.jpg", "image/jpeg", "plain text payload pretending to be jpeg")]
    [InlineData("empty.png", "image/png", "")]
    public void ValidateMaterialImageBytes_RejectsDisguisedOrEmptyPayloads(
        string fileName,
        string contentType,
        string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var (isValid, err) = FileUploadValidator.ValidateMaterialImageBytes(fileName, contentType, bytes);

        Assert.False(isValid);
        Assert.False(string.IsNullOrWhiteSpace(err));
    }

    [Theory]
    [InlineData("xss.svg", "image/svg+xml")]
    [InlineData("evil.html", "text/html")]
    [InlineData("payload.exe", "application/x-msdownload")]
    [InlineData("doc.pdf", "application/pdf")]
    [InlineData("noext", "image/png")]
    [InlineData("", "image/png")]
    public void ValidateMaterialImageBytes_RejectsForbiddenOrUnsupportedExtensions(
        string fileName,
        string contentType)
    {
        byte[] validPngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        var (isValid, err) = FileUploadValidator.ValidateMaterialImageBytes(fileName, contentType, validPngHeader);

        Assert.False(isValid);
        Assert.False(string.IsNullOrWhiteSpace(err));
    }

    [Fact]
    public async Task ValidateMaterialImage_AcceptsValidJpegPngGifWebpMagicBytes()
    {
        byte[] jpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        byte[] gifBytes = Encoding.ASCII.GetBytes("GIF89a\x01\x00\x01\x00");
        byte[] webpBytes =
        [
            (byte)'R', (byte)'I', (byte)'F', (byte)'F',
            0x24, 0x00, 0x00, 0x00,
            (byte)'W', (byte)'E', (byte)'B', (byte)'P',
        ];

        Assert.True(FileUploadValidator.ValidateMaterialImageBytes("pump.jpg", "image/jpeg", jpegBytes).IsValid);
        Assert.True(FileUploadValidator.ValidateMaterialImageBytes("pump.jpeg", "image/jpeg", jpegBytes).IsValid);
        Assert.True(FileUploadValidator.ValidateMaterialImageBytes("valve.png", "image/png", pngBytes).IsValid);
        Assert.True(FileUploadValidator.ValidateMaterialImageBytes("motor.gif", "image/gif", gifBytes).IsValid);
        Assert.True(FileUploadValidator.ValidateMaterialImageBytes("boiler.webp", "image/webp", webpBytes).IsValid);

        // Stream overload should validate and rewind seekable streams
        await using var stream = new MemoryStream(webpBytes);
        var streamResult = await FileUploadValidator.ValidateMaterialImageAsync("boiler.webp", "image/webp", stream);
        Assert.True(streamResult.IsValid);
        Assert.Equal(0, stream.Position);

        // Reject forbidden MIME even with valid magic bytes
        Assert.False(FileUploadValidator.ValidateMaterialImageBytes("valve.png", "text/html", pngBytes).IsValid);
        Assert.False(FileUploadValidator.ValidateMaterialImageBytes("valve.png", "image/jpeg", pngBytes).IsValid);
    }

    [Fact]
    public void DownloadImportTemplates_EnforcePublicReadRateLimiting()
    {
        var materialsMethod = typeof(MaterialsController)
            .GetMethod(nameof(MaterialsController.DownloadImportTemplate), BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(materialsMethod);
        var materialsRateLimit = materialsMethod.GetCustomAttribute<EnableRateLimitingAttribute>();
        Assert.NotNull(materialsRateLimit);
        Assert.Equal(RateLimitPolicies.PublicRead, materialsRateLimit.PolicyName);

        var unitsMethod = typeof(MaterialUnitsController)
            .GetMethod(nameof(MaterialUnitsController.DownloadImportTemplate), BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(unitsMethod);
        var unitsRateLimit = unitsMethod.GetCustomAttribute<EnableRateLimitingAttribute>();
        Assert.NotNull(unitsRateLimit);
        Assert.Equal(RateLimitPolicies.PublicRead, unitsRateLimit.PolicyName);
    }

    [Fact]
    public async Task MaterialsController_UploadImage_RejectsDisguisedHtmlScriptAndSanitizesMaterialCode()
    {
        var controller = new MaterialsController(null!, null!);
        var storage = new CapturingFileStorage();

        // Spoofed HTML disguised as .jpg
        using (var htmlMs = new MemoryStream(Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>")))
        {
            var formFile = CreateFormFile(htmlMs, "spoofed.jpg", "image/jpeg");
            var res = await controller.UploadImage(formFile, "VT-001", storage, CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(res);
            Assert.Equal(0, storage.SaveCalls);
        }

        // Valid PNG with traversal characters in materialCode
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        using (var pngMs = new MemoryStream(pngBytes))
        {
            var formFile = CreateFormFile(pngMs, "pump.png", "image/png");
            var res = await controller.UploadImage(formFile, "../etc/VT 001", storage, CancellationToken.None);
            Assert.IsType<OkObjectResult>(res);
            Assert.Equal(1, storage.SaveCalls);
            Assert.DoesNotContain("..", storage.LastRelativePath);
            Assert.StartsWith("materials/___etc_VT_001_", storage.LastRelativePath);
            Assert.Equal(pngBytes, storage.LastSavedBytes);
        }
    }

    [Fact]
    public async Task ProjectGanttSnapshotsController_TakeSnapshot_RejectsOver2000ItemsAndSupportsCancellation()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"gantt-sec-{Guid.NewGuid():N}")
            .Options;
        await using var db = new WarehouseDbContext(options);
        db.Projects.Add(new Project { ProjectCode = "PRJ-GANTT", ProjectName = "Dự án Gantt" });
        await db.SaveChangesAsync();

        var controller = new ProjectGanttSnapshotsController(db);
        var tooManyItems = Enumerable.Range(1, 2001)
            .Select(i => new ProjectGanttSnapshotItemDto(Guid.Empty, $"TASK-{i}", $"Hạng mục {i}", 50, 40))
            .ToList();

        var res = await controller.TakeSnapshot(
            "PRJ-GANTT",
            new CreateProjectGanttSnapshotRequest(50, 40, tooManyItems),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(res.Result);
        Assert.Equal(0, await db.ProjectGanttSnapshots.CountAsync());
    }

    [Fact]
    public void ModuleRegistrations_NormalizeKeepAliveAndRegisterDataSource_WithoutRetryOnFailure()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Port=5432;Database=mayzer_test;Username=postgres;Password=secret",
                ["Jwt:SigningKey"] = "unit-test-signing-key-at-least-32-chars-long!",
                ["Jwt:Issuer"] = "MayzerTest",
                ["Jwt:Audience"] = "MayzerTest",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddWarehouseModule(config);
        services.AddIdentityModule(config);
        services.AddHrModule(config);

        using var provider = services.BuildServiceProvider();
        var dataSource = provider.GetService<NpgsqlDataSource>();
        Assert.NotNull(dataSource);
        Assert.Contains("Keepalive=30", dataSource.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    private static FormFile CreateFormFile(MemoryStream ms, string fileName, string contentType)
    {
        ms.Position = 0;
        return new FormFile(ms, 0, ms.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private sealed class CapturingFileStorage : IFileStorage
    {
        public int SaveCalls { get; private set; }
        public string LastRelativePath { get; private set; } = string.Empty;
        public byte[] LastSavedBytes { get; private set; } = [];

        public async Task<StoredFile> SaveAsync(
            Stream content,
            string relativePath,
            string contentType,
            CancellationToken ct = default)
        {
            SaveCalls++;
            LastRelativePath = relativePath;
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, ct);
            LastSavedBytes = copy.ToArray();
            return new StoredFile(relativePath, Path.GetFileName(relativePath), LastSavedBytes.Length);
        }

        public StoredFileContent? Get(string storageKey) => null;
        public Task<StoredFileStream?> OpenReadAsync(string key, CancellationToken ct = default) => Task.FromResult<StoredFileStream?>(null);
        public Task<int> DeleteUnreferencedOlderThanAsync(IReadOnlySet<string> referencedKeys, DateTime olderThanUtc, CancellationToken ct = default) => Task.FromResult(0);
    }
}
