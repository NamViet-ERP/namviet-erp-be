using System.Text;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Controllers.Wms;
using NamViet.ERP.Api.Services.Storage;
using Xunit;

namespace NamViet.ERP.Tests.Wms;

public class FileStorageStreamTests
{
    [Theory]
    [InlineData("may-bom-cnf80.png", null, "image/png")]
    [InlineData("may-bom-cnf80.jpg", null, "image/jpeg")]
    [InlineData("may-bom-cnf80.jpeg", null, "image/jpeg")]
    [InlineData("hop-dong.pdf", null, "application/pdf")]
    [InlineData("bien-ban.docx", null, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("file-la.xyz", null, "application/octet-stream")]
    [InlineData("may-bom-cnf80.png", "image/png", "image/png")]
    [InlineData("may-bom-cnf80.png", "application/octet-stream", "image/png")]
    public void StoredFileStream_ResolveContentType_resolves_accurately(
        string fileName,
        string? explicitContentType,
        string expectedContentType)
    {
        using var ms = new MemoryStream();
        var streamResult = new StoredFileStream(ms, explicitContentType, ms.Length);

        var actual = streamResult.ResolveContentType(fileName);

        Assert.Equal(expectedContentType, actual);
    }

    [Fact]
    public async Task ExportReturnsController_DownloadAttachment_returns_FileStreamResult_via_OpenReadAsync()
    {
        var returnId = "HT-0001";
        var attachmentId = Guid.NewGuid();
        var storageKey = "wms/export-returns/HT-0001/photo/may-bom.png";
        var fileName = "may-bom-cnf80.png";

        var attachmentDto = new ImportAttachmentDto(
            attachmentId,
            "photo",
            fileName,
            storageKey,
            DateTime.UtcNow.ToString("O"));

        var bytes = Encoding.UTF8.GetBytes("fake-image-bytes");
        using var stream = new MemoryStream(bytes);
        var storedStream = new StoredFileStream(stream, null, bytes.Length);

        var fakeService = new StubExportReturnService
        {
            AttachmentToReturn = attachmentDto,
        };
        var fakeStorage = new StubFileStorage
        {
            StreamToReturn = storedStream,
        };

        var controller = new ExportReturnsController(
            fakeService,
            null!,
            fakeStorage,
            null!);

        var result = await controller.DownloadAttachment(returnId, attachmentId, CancellationToken.None);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/png", fileResult.ContentType);
        Assert.Equal(fileName, fileResult.FileDownloadName);
        Assert.Same(stream, fileResult.FileStream);
    }

    [Fact]
    public async Task ExportReturnsController_DownloadAttachment_returns_NotFound_when_file_not_in_storage()
    {
        var returnId = "HT-0001";
        var attachmentId = Guid.NewGuid();
        var storageKey = "wms/export-returns/HT-0001/photo/missing.png";

        var attachmentDto = new ImportAttachmentDto(
            attachmentId,
            "photo",
            "missing.png",
            storageKey,
            DateTime.UtcNow.ToString("O"));

        var fakeService = new StubExportReturnService
        {
            AttachmentToReturn = attachmentDto,
        };
        var fakeStorage = new StubFileStorage
        {
            StreamToReturn = null,
        };

        var controller = new ExportReturnsController(
            fakeService,
            null!,
            fakeStorage,
            null!);

        var result = await controller.DownloadAttachment(returnId, attachmentId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private sealed class StubFileStorage : IFileStorage
    {
        public StoredFileStream? StreamToReturn { get; set; }

        public Task<StoredFile> SaveAsync(Stream content, string relativePath, string contentType, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public StoredFileContent? Get(string storageKey) => null;

        public Task<StoredFileStream?> OpenReadAsync(string storageKey, CancellationToken ct = default) =>
            Task.FromResult(StreamToReturn);

        public Task<int> DeleteUnreferencedOlderThanAsync(IReadOnlySet<string> referencedKeys, DateTime olderThanUtc, CancellationToken ct = default) =>
            Task.FromResult(0);
    }

    private sealed class StubExportReturnService : IExportReturnApplicationService
    {
        public ImportAttachmentDto? AttachmentToReturn { get; set; }

        public Task<ImportAttachmentDto?> GetAttachmentAsync(string id, Guid attachmentId, CancellationToken ct = default) =>
            Task.FromResult(AttachmentToReturn);

        public Task<List<ExportReturnDto>> ListAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ExportReturnDto?> GetByIdAsync(string id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ExportReturnableExportDto>> ListReturnableExportsAsync(string projectCode, string? excludeReturnId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ExportReturnableLineDto>> GetReturnableLinesAsync(string sourceExportId, string? excludeReturnId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ExportReturnDto> CreateAsync(CreateExportReturnDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ExportReturnDto?> UpdateAsync(string id, CreateExportReturnDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ExportReturnDto?> ApproveAsync(string id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ExportReturnDto?> SubmitAsync(string id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(string id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ImportAttachmentDto?> UploadAttachmentAsync(string id, string kind, string fileName, string contentType, Stream content, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> DeleteAttachmentAsync(string id, Guid attachmentId, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
