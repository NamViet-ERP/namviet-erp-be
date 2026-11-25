using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Controllers.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Authorization;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Tests.Purchasing;

public sealed class PriceBookMarkupSettingsTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly FakeCurrentUser _currentUser;
    private readonly PriceBookMarkupSettingsService _service;

    public PriceBookMarkupSettingsTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);
        _currentUser = new FakeCurrentUser();
        _service = new PriceBookMarkupSettingsService(_db, _currentUser);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task GetAsync_WhenSettingAbsent_ReturnsZeroDefaultsAndDoesNotSeedDb()
    {
        var result = await _service.GetAsync();

        Assert.Equal(0m, result.GlobalMarkupPercent);
        Assert.Empty(result.Categories);
        Assert.Empty(result.ItemOverrides);
        Assert.Null(result.UpdatedAt);
        Assert.Null(result.UpdatedBy);
        Assert.Equal(0, await _db.WmsSettings.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_WithValidPayload_PersistsAndSetsAuditFields()
    {
        var request = new UpdatePriceBookMarkupSettingsRequest(
            GlobalMarkupPercent: 12.5m,
            Categories:
            [
                new PriceBookMarkupCategoryDto(" cat-valves ", " Van công nghiệp ", 15m, " Hệ van hơi ")
            ],
            ItemOverrides: new Dictionary<string, decimal>
            {
                [" VT-001 "] = 18.25m,
            });

        var updated = await _service.UpdateAsync(request);

        Assert.Equal(12.5m, updated.GlobalMarkupPercent);
        Assert.NotNull(updated.UpdatedAt);
        Assert.Equal("Giám đốc Điều hành", updated.UpdatedBy);

        var singleCat = Assert.Single(updated.Categories);
        Assert.Equal("cat-valves", singleCat.Id);
        Assert.Equal("Van công nghiệp", singleCat.Name);
        Assert.Equal(15m, singleCat.Percent);
        Assert.Equal("Hệ van hơi", singleCat.Note);
        Assert.Equal(18.25m, updated.ItemOverrides["VT-001"]);

        var settingRow = await _db.WmsSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == WmsSettingsKeys.PriceBookMarkup);
        Assert.NotNull(settingRow);

        var reloaded = await _service.GetAsync();
        Assert.Equal(12.5m, reloaded.GlobalMarkupPercent);
        Assert.Equal("Giám đốc Điều hành", reloaded.UpdatedBy);
        Assert.Single(reloaded.Categories);
        Assert.Equal(18.25m, reloaded.ItemOverrides["VT-001"]);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public async Task UpdateAsync_OutOfRangeGlobalMarkup_ThrowsDomainRuleException(double percent)
    {
        var request = new UpdatePriceBookMarkupSettingsRequest((decimal)percent);

        await Assert.ThrowsAsync<DomainRuleException>(() => _service.UpdateAsync(request));
        Assert.Equal(0, await _db.WmsSettings.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_InvalidCategoriesOrOverrides_ThrowsDomainRuleException()
    {
        await Assert.ThrowsAsync<DomainRuleException>(() => _service.UpdateAsync(
            new UpdatePriceBookMarkupSettingsRequest(10m, [new PriceBookMarkupCategoryDto(" ", "Van", 10m)])));

        await Assert.ThrowsAsync<DomainRuleException>(() => _service.UpdateAsync(
            new UpdatePriceBookMarkupSettingsRequest(10m, [new PriceBookMarkupCategoryDto("c1", " ", 10m)])));

        await Assert.ThrowsAsync<DomainRuleException>(() => _service.UpdateAsync(
            new UpdatePriceBookMarkupSettingsRequest(10m, [new PriceBookMarkupCategoryDto("c1", "Van", 150m)])));

        await Assert.ThrowsAsync<DomainRuleException>(() => _service.UpdateAsync(
            new UpdatePriceBookMarkupSettingsRequest(10m,
            [
                new PriceBookMarkupCategoryDto("CAT-1", "Van 1", 10m),
                new PriceBookMarkupCategoryDto("cat-1", "Van 2", 20m),
            ])));

        await Assert.ThrowsAsync<DomainRuleException>(() => _service.UpdateAsync(
            new UpdatePriceBookMarkupSettingsRequest(10m, [], new Dictionary<string, decimal> { [" "] = 5m })));

        await Assert.ThrowsAsync<DomainRuleException>(() => _service.UpdateAsync(
            new UpdatePriceBookMarkupSettingsRequest(10m, [], new Dictionary<string, decimal> { ["VT-001"] = -5m })));
    }

    [Fact]
    public async Task PurchasePriceBookController_MarkupEndpoints_EnforcePermissionsRoutesAndValidation()
    {
        var getMethod = typeof(PurchasePriceBookController).GetMethod(nameof(PurchasePriceBookController.GetMarkupSettings));
        var putMethod = typeof(PurchasePriceBookController).GetMethod(nameof(PurchasePriceBookController.UpdateMarkupSettings));

        var getPerm = getMethod?.GetCustomAttribute<RequirePermissionAttribute>();
        var putPerm = putMethod?.GetCustomAttribute<RequirePermissionAttribute>();
        var getRoute = getMethod?.GetCustomAttribute<HttpGetAttribute>();
        var putRoute = putMethod?.GetCustomAttribute<HttpPutAttribute>();

        Assert.NotNull(getPerm);
        Assert.Equal("purchasing.pricebook.view", getPerm.Permission);
        Assert.True(PermissionCatalog.IsValid(getPerm.Permission));
        Assert.Equal("/api/purchasing/markup-settings", getRoute?.Template);

        Assert.NotNull(putPerm);
        Assert.Equal("purchasing.pricebook.update", putPerm.Permission);
        Assert.True(PermissionCatalog.IsValid(putPerm.Permission));
        Assert.Equal("/api/purchasing/markup-settings", putRoute?.Template);

        var controller = new PurchasePriceBookController(null!, _service);
        var bad = await controller.UpdateMarkupSettings(new UpdatePriceBookMarkupSettingsRequest(105m), default);
        Assert.IsType<BadRequestObjectResult>(bad.Result);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        private readonly ClaimsPrincipal _principal = new();

        public Guid Id => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string Name => "Giám đốc Điều hành";
        public string Email => "boss@mayzer.com";
        public string EmployeeCode => "EMP-BOD";
        public int? EmployeeId => 1;
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles { get; } = [RolePermissionTemplates.AdminRole];
        public IReadOnlyList<string> Permissions { get; } = ["purchasing.pricebook.view", "purchasing.pricebook.update"];
        public ClaimsPrincipal Principal => _principal;

        public bool IsInRole(string role) => Roles.Contains(role);
        public bool HasPermission(string permission) => Permissions.Contains(permission);
        public bool HasAllPermissions(params string[] permissions) => permissions.All(HasPermission);
        public bool HasAnyPermission(params string[] permissions) => permissions.Any(HasPermission);
    }
}
