using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.SupplierEvaluation;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace NamViet.ERP.Tests.Purchasing;

public class SupplierAttachmentsAndContactsTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly FakeSupplierGroupService _groupsService;
    private readonly TestCurrentUser _currentUser;
    private readonly FakeFileStorage _fileStorage;
    private readonly SuppliersController _controller;
    private readonly SupplierContactService _contactService;

    public SupplierAttachmentsAndContactsTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);

        _groupsService = new FakeSupplierGroupService();
        _currentUser = new TestCurrentUser
        {
            Id = Guid.NewGuid(),
            Name = "Chuyên viên Mua hàng",
            Email = "buyer@mayzer.com"
        };
        _fileStorage = new FakeFileStorage();

        _controller = new SuppliersController(_db, _groupsService, _currentUser, _fileStorage);
        _contactService = new SupplierContactService(_db);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task CreateSupplier_WithContactInfo_AutoCreatesPrimaryContactAndSetsPhone()
    {
        var req = new UpsertSupplierRequest(
            SupplierCode: null,
            SupplierName: "Công ty TNHH Nhôm Kính Nam Việt",
            ContactPhone: "0912345678",
            MaxDebtDays: 30,
            PaymentTerms: "Net 30",
            CreditLimit: 100_000_000m,
            TaxCode: "0312345678",
            ContactName: "Trần Văn An",
            ContactEmail: "an.tran@namvietalu.com",
            ContactPosition: "Giám đốc kinh doanh");

        var result = await _controller.Create(req, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SupplierDto>(okResult.Value);

        Assert.Equal("0912345678", dto.ContactPhone);
        Assert.NotNull(dto.PrimaryContact);
        Assert.Equal("Trần Văn An", dto.PrimaryContact.FullName);
        Assert.Equal("0912345678", dto.PrimaryContact.Phone);
        Assert.Equal("an.tran@namvietalu.com", dto.PrimaryContact.Email);
        Assert.Equal("Giám đốc kinh doanh", dto.PrimaryContact.Position);
        Assert.Equal("primary", dto.PrimaryContact.Role);
        Assert.Equal(1, dto.ContactCount);

        var contactInDb = await _db.SupplierContacts
            .FirstOrDefaultAsync(c => c.SupplierId == dto.Id);
        Assert.NotNull(contactInDb);
        Assert.Equal("Trần Văn An", contactInDb.FullName);
        Assert.True(contactInDb.IsPrimary);
    }

    [Fact]
    public async Task UpdateSupplier_WithContactInfo_UpdatesExistingPrimaryContact()
    {
        var createReq = new UpsertSupplierRequest(
            SupplierCode: null,
            SupplierName: "Công ty Cổ phần Xi Măng Hà Tiên",
            ContactPhone: "0987654321",
            ContactName: "Lê Hoàng",
            ContactPosition: "Trưởng phòng Bán hàng");

        var createResult = await _controller.Create(createReq, CancellationToken.None);
        var created = Assert.IsType<SupplierDto>(Assert.IsType<OkObjectResult>(createResult.Result).Value);

        var updateReq = new UpsertSupplierRequest(
            SupplierCode: created.SupplierCode,
            SupplierName: "Công ty Cổ phần Xi Măng Hà Tiên (Đã đổi tên)",
            ContactPhone: "0909111222",
            ContactName: "Lê Hoàng (Thăng chức)",
            ContactPosition: "Phó Tổng Giám Đốc",
            ContactEmail: "hoang.le@hatien.vn");

        var updateResult = await _controller.Update(created.Id, updateReq, CancellationToken.None);
        Assert.IsType<NoContentResult>(updateResult);

        var getResult = await _controller.GetById(created.Id, CancellationToken.None);
        var updatedDto = Assert.IsType<SupplierDto>(Assert.IsType<OkObjectResult>(getResult.Result).Value);

        Assert.Equal("0909111222", updatedDto.ContactPhone);
        Assert.NotNull(updatedDto.PrimaryContact);
        Assert.Equal("Lê Hoàng (Thăng chức)", updatedDto.PrimaryContact.FullName);
        Assert.Equal("Phó Tổng Giám Đốc", updatedDto.PrimaryContact.Position);
        Assert.Equal("hoang.le@hatien.vn", updatedDto.PrimaryContact.Email);
        Assert.Equal("0909111222", updatedDto.PrimaryContact.Phone);
    }

    [Fact]
    public async Task SupplierContactService_CreateOrUpdatePrimary_UpdatesSupplierContactPhone()
    {
        var supplier = new Supplier
        {
            SupplierCode = "NCC-0001",
            SupplierName = "Công ty TNHH Nhựa Tiền Phong",
            ContactPhone = "0911222333"
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var createContactReq = new UpsertSupplierContactRequest(
            SupplierId: supplier.Id,
            FullName: "Nguyễn Thị Mai",
            Position: "Kế toán trưởng",
            Phone: "0977888999",
            Email: "mai.nguyen@tienphong.vn",
            Note: "Đầu mối công nợ",
            Role: "primary");

        var createdContact = await _contactService.CreateAsync(createContactReq);
        Assert.Equal("primary", createdContact.Role);

        var reloadedSupplier = await _db.Suppliers.FindAsync(supplier.Id);
        Assert.NotNull(reloadedSupplier);
        Assert.Equal("0977888999", reloadedSupplier.ContactPhone);

        var updateContactReq = new UpsertSupplierContactRequest(
            SupplierId: supplier.Id,
            FullName: "Nguyễn Thị Mai",
            Position: "Kế toán trưởng",
            Phone: "0966555444",
            Email: "mai.nguyen@tienphong.vn",
            Note: null,
            Role: "primary");

        await _contactService.UpdateAsync(createdContact.Id, updateContactReq);
        reloadedSupplier = await _db.Suppliers.FindAsync(supplier.Id);
        Assert.Equal("0966555444", reloadedSupplier!.ContactPhone);
    }

    [Fact]
    public async Task UploadAttachment_ValidFile_SavesAttachmentAndUpdatesSupplier()
    {
        var supplier = new Supplier
        {
            SupplierCode = "NCC-0002",
            SupplierName = "Công ty Cáp điện Cadivi",
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var content = Encoding.UTF8.GetBytes("Fake PDF Content for GPKD");
        using var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, content.Length, "file", "GPKD_Cadivi_2026.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var uploadResult = await _controller.UploadAttachment(
            supplier.Id, formFile, docType: "business_license", CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(uploadResult.Result);
        var attachmentDto = Assert.IsType<SupplierAttachmentDto>(okResult.Value);

        Assert.Equal("business_license", attachmentDto.DocType);
        Assert.Equal("GPKD / ĐKKD doanh nghiệp", attachmentDto.DocTypeLabel);
        Assert.Equal("GPKD_Cadivi_2026.pdf", attachmentDto.FileName);
        Assert.Equal(content.Length, attachmentDto.FileSizeBytes);
        Assert.Contains(attachmentDto.Id.ToString(), attachmentDto.DownloadUrl);

        var listResult = await _controller.ListAttachments(supplier.Id, CancellationToken.None);
        var listOk = Assert.IsType<OkObjectResult>(listResult.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<SupplierAttachmentDto>>(listOk.Value);
        Assert.Single(list);

        var getResult = await _controller.GetById(supplier.Id, CancellationToken.None);
        var supplierDto = Assert.IsType<SupplierDto>(Assert.IsType<OkObjectResult>(getResult.Result).Value);
        Assert.NotNull(supplierDto.Attachments);
        Assert.Single(supplierDto.Attachments);
    }

    [Fact]
    public async Task DownloadAttachment_ExistingAttachment_ReturnsFileStreamResult()
    {
        var supplier = new Supplier
        {
            SupplierCode = "NCC-0003",
            SupplierName = "Công ty TNHH Sơn Dulux",
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var content = Encoding.UTF8.GetBytes("Company Profile Dulux 2026");
        using var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, content.Length, "file", "Dulux_Profile.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var uploadResult = await _controller.UploadAttachment(
            supplier.Id, formFile, docType: "company_profile", CancellationToken.None);
        var attachmentDto = Assert.IsType<SupplierAttachmentDto>(Assert.IsType<OkObjectResult>(uploadResult.Result).Value);

        var downloadResult = await _controller.DownloadAttachment(supplier.Id, attachmentDto.Id, CancellationToken.None);
        var fileResult = Assert.IsType<FileStreamResult>(downloadResult);
        Assert.Equal("Dulux_Profile.pdf", fileResult.FileDownloadName);
        Assert.Equal("application/pdf", fileResult.ContentType);
    }

    [Fact]
    public async Task DeleteAttachment_ExistingAttachment_SoftDeletes()
    {
        var supplier = new Supplier
        {
            SupplierCode = "NCC-0004",
            SupplierName = "Công ty Thiết bị Điện Schneider",
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var content = Encoding.UTF8.GetBytes("Schneider Price List 2026");
        using var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, content.Length, "file", "Schneider_Catalog.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        var uploadResult = await _controller.UploadAttachment(
            supplier.Id, formFile, docType: "catalog_price_list", CancellationToken.None);
        var attachmentDto = Assert.IsType<SupplierAttachmentDto>(Assert.IsType<OkObjectResult>(uploadResult.Result).Value);

        var deleteResult = await _controller.DeleteAttachment(supplier.Id, attachmentDto.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(deleteResult);

        var listResult = await _controller.ListAttachments(supplier.Id, CancellationToken.None);
        var list = Assert.IsAssignableFrom<IReadOnlyList<SupplierAttachmentDto>>(Assert.IsType<OkObjectResult>(listResult.Result).Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task UploadAttachment_DisallowedExtension_ReturnsBadRequest()
    {
        var supplier = new Supplier
        {
            SupplierCode = "NCC-0005",
            SupplierName = "Test Supplier",
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var content = Encoding.UTF8.GetBytes("malicious script");
        using var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, content.Length, "file", "bad.exe")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };

        var uploadResult = await _controller.UploadAttachment(
            supplier.Id, formFile, docType: "other", CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(uploadResult.Result);
    }

    private class FakeFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = new();

        public async Task<StoredFile> SaveAsync(Stream content, string relativePath, string contentType, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();
            var key = "fake://" + relativePath;
            _files[key] = bytes;
            return new StoredFile(key, Path.GetFileName(relativePath), bytes.Length);
        }

        public StoredFileContent? Get(string storageKey)
        {
            if (_files.TryGetValue(storageKey, out var bytes))
                return new StoredFileContent(storageKey, bytes.Length);
            return null;
        }

        public Task<StoredFileStream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
        {
            if (_files.TryGetValue(storageKey, out var bytes))
            {
                var stream = new MemoryStream(bytes);
                return Task.FromResult<StoredFileStream?>(new StoredFileStream(stream, "application/pdf", bytes.Length));
            }
            return Task.FromResult<StoredFileStream?>(null);
        }

        public Task<int> DeleteUnreferencedOlderThanAsync(IReadOnlySet<string> referencedKeys, DateTime olderThanUtc, CancellationToken ct = default)
            => Task.FromResult(0);
    }

    private class FakeSupplierGroupService : ISupplierGroupService
    {
        public Task<SupplierGroupPagedResponse> ListAsync(string? search, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(new SupplierGroupPagedResponse([], 0, page, pageSize, 0));

        public Task<SupplierGroupDto> GetAsync(int id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<SupplierGroupDto> CreateAsync(UpsertSupplierGroupRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<SupplierGroupDto> UpdateAsync(int id, UpsertSupplierGroupRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task DeleteAsync(int id, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<SupplierGroupMemberDto>> ListMembersAsync(int groupId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SupplierGroupMemberDto>>([]);

        public Task<IReadOnlyList<SupplierGroupMemberDto>> SetMembersAsync(int groupId, SetSupplierGroupMembersRequest request, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SupplierGroupMemberDto>>([]);

        public Task<IReadOnlyList<SupplierGroupRefDto>> ListGroupsOfSupplierAsync(int supplierId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SupplierGroupRefDto>>([]);

        public Task<IReadOnlyList<SupplierGroupRefDto>> SetGroupsOfSupplierAsync(int supplierId, SetSupplierGroupsRequest request, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SupplierGroupRefDto>>([]);
    }

    private class TestCurrentUser : ICurrentUser
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Test User";
        public string Email { get; set; } = "test@namviet.vn";
        public string EmployeeCode => "EMP-001";
        public int? EmployeeId => null;
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles { get; set; } = [];
        public IReadOnlyList<string> Permissions { get; set; } = [];
        public ClaimsPrincipal Principal => new();

        public bool IsInRole(string role) => Roles.Contains(role);
        public bool HasPermission(string permission) => true;
        public bool HasAllPermissions(params string[] permissions) => true;
        public bool HasAnyPermission(params string[] permissions) => true;
    }
}
