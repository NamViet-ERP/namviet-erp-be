using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Services.Cskh;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Tests.Wms;

public class FileAccessBoundaryTests
{
    [Theory]
    [InlineData("hr/employees/private.png")]
    [InlineData("materials/../hr/employees/private.png")]
    [InlineData("materials/..\\private.png")]
    [InlineData("materials/private.txt")]
    [InlineData("materials/%2e%2e.png")]
    public async Task Public_image_endpoint_rejects_non_material_keys_before_opening_storage(string key)
    {
        var storage = new TrackingStorage();
        var controller = new MaterialsController(null!, null!);
        Assert.IsType<NotFoundResult>(await controller.GetImage(key, storage, default));
        Assert.Null(storage.OpenedKey);
    }

    [Fact]
    public async Task Public_material_image_is_still_readable()
    {
        var storage = new TrackingStorage();
        var result = await new MaterialsController(null!, null!).GetImage("materials/pump.png", storage, default);
        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/png", file.ContentType);
        file.FileStream.Dispose();
    }

    [Fact]
    public async Task Document_download_binds_key_to_existing_project_and_document()
    {
        await using var db = new WarehouseDbContext(new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Projects.Add(new Project { ProjectCode = "TEST", ProjectName = "Test" });
        await db.SaveChangesAsync();
        var service = new CskhCoordinationService(db);
        var document = service.CreateDocument("TEST", new CreateProjectDocumentRequest("DOC", "Document", "other", null, null));
        var key = $"cskh/projects/TEST/docs/{document.Id}/file.pdf";
        service.UpdateDocumentFile("TEST", document.Id, "file.pdf", "1 KB", $"/download?key={Uri.EscapeDataString(key)}");
        var storage = new TrackingStorage();
        var controller = new ProjectDocumentsController(service);
        Assert.IsType<NotFoundResult>(await controller.DownloadFile("TEST", document.Id, "hr/employees/private.pdf", storage, default));
        Assert.IsType<NotFoundResult>(await controller.DownloadFile("TEST", "missing", key, storage, default));
        Assert.Null(storage.OpenedKey);
        var file = Assert.IsType<FileStreamResult>(await controller.DownloadFile("TEST", document.Id, null, storage, default));
        Assert.Equal(key, storage.OpenedKey);
        Assert.Equal("file.pdf", file.FileDownloadName);
        file.FileStream.Dispose();
        Assert.Empty(typeof(ProjectDocumentsController).GetMethod(nameof(ProjectDocumentsController.DownloadFile))!
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
    }

    private sealed class TrackingStorage : IFileStorage
    {
        public string? OpenedKey { get; private set; }
        public Task<StoredFileStream?> OpenReadAsync(string key, CancellationToken ct = default)
        {
            OpenedKey = key;
            return Task.FromResult<StoredFileStream?>(new StoredFileStream(new MemoryStream([1, 2, 3]), null, 3));
        }
        public Task<StoredFile> SaveAsync(Stream content, string relativePath, string contentType, CancellationToken ct = default) => throw new NotImplementedException();
        public StoredFileContent? Get(string storageKey) => throw new NotImplementedException();
        public Task<int> DeleteUnreferencedOlderThanAsync(IReadOnlySet<string> referencedKeys, DateTime olderThanUtc, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
