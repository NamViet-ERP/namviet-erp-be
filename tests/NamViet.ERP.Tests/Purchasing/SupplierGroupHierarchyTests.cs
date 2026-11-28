using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Controllers.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Tests.Wms;
using Xunit;

namespace NamViet.ERP.Tests.Purchasing;

public class SupplierGroupHierarchyTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly SupplierGroupService _groupService;
    private readonly SupplierDirectoryService _directoryService;
    private readonly SupplierExcelService _excelService;

    public SupplierGroupHierarchyTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"supplier-group-hierarchy-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new WarehouseDbContext(options);
        _groupService = new SupplierGroupService(_db);
        _directoryService = new SupplierDirectoryService(_db, new FakeCurrentUser());
        _excelService = new SupplierExcelService(_db);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CreateAndList_ReturnsTreeOrderWithParentBeforeChildren()
    {
        var elec = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư Điện", "Nhóm điện công nghiệp"));
        var steel = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Hệ ống thép", "Ống và phụ kiện"));
        var panel = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Tủ điện", null, elec.Id));
        var cable = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Dây cáp điện", null, elec.Id));

        Assert.Null(elec.ParentGroupId);
        Assert.Null(elec.ParentGroupName);
        Assert.Equal(elec.Id, cable.ParentGroupId);
        Assert.Equal("Vật tư Điện", cable.ParentGroupName);

        var page = await _groupService.ListAsync(null, 1, 500);

        Assert.Equal(500, page.PageSize);
        Assert.Equal(4, page.TotalCount);
        Assert.Collection(page.Items,
            item =>
            {
                Assert.Equal(steel.Id, item.Id);
                Assert.Null(item.ParentGroupId);
                Assert.Equal(0, item.ChildCount);
            },
            item =>
            {
                Assert.Equal(elec.Id, item.Id);
                Assert.Null(item.ParentGroupId);
                Assert.Equal(2, item.ChildCount);
            },
            item =>
            {
                Assert.Equal(cable.Id, item.Id);
                Assert.Equal(elec.Id, item.ParentGroupId);
                Assert.Equal("Vật tư Điện", item.ParentGroupName);
                Assert.Equal(0, item.ChildCount);
            },
            item =>
            {
                Assert.Equal(panel.Id, item.Id);
                Assert.Equal(elec.Id, item.ParentGroupId);
                Assert.Equal("Vật tư Điện", item.ParentGroupName);
                Assert.Equal(0, item.ChildCount);
            });
    }

    [Fact]
    public async Task ListAsync_WithOver200Groups_ReturnsUpTo500WithoutClamping()
    {
        var groups = Enumerable.Range(1, 205)
            .Select(i => new SupplierGroup { Name = $"Nhóm vật tư {i:D3}" })
            .ToList();
        _db.SupplierGroups.AddRange(groups);
        await _db.SaveChangesAsync();

        var page = await _groupService.ListAsync(null, 1, 500);

        Assert.Equal(500, page.PageSize);
        Assert.Equal(205, page.TotalCount);
        Assert.Equal(205, page.Items.Count);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public async Task Create_Level3UnderChildGroup_ThrowsConflict()
    {
        var parent = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư Điện", null));
        var child = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Dây cáp điện", null, parent.Id));

        var ex = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.CreateAsync(new UpsertSupplierGroupRequest("Cáp đồng", null, child.Id)));

        Assert.Contains("2 cấp", ex.Message);
    }

    [Fact]
    public async Task Create_WithMissingOrSoftDeletedParent_ThrowsConflict()
    {
        await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.CreateAsync(new UpsertSupplierGroupRequest("Mục mồ côi", null, 999999)));

        var deletedParent = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Nhóm tạm", null));
        await _groupService.DeleteAsync(deletedParent.Id);

        var ex = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.CreateAsync(new UpsertSupplierGroupRequest("Mục con nhóm đã xóa", null, deletedParent.Id)));

        Assert.Contains("Nhóm lớn không tồn tại", ex.Message);
    }

    [Fact]
    public async Task CreateAndUpdate_ScopedDuplicateNameRules_EnforcedPerLevelAndParent()
    {
        var rootA = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Hệ ống thép", null));
        var rootB = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Hệ van công nghiệp", null));
        var rootDupName = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Phụ kiện", null));

        // Same child name allowed under different parents and alongside root with same name.
        var childA = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Phụ kiện", null, rootA.Id));
        var childB = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Phụ kiện", null, rootB.Id));
        Assert.NotEqual(childA.Id, childB.Id);
        Assert.NotEqual(rootDupName.Id, childA.Id);

        // Duplicate root (case-insensitive) rejected.
        await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.CreateAsync(new UpsertSupplierGroupRequest("hệ ống thép", null)));

        // Duplicate child under same parent (case-insensitive) rejected.
        await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.CreateAsync(new UpsertSupplierGroupRequest("phụ kiện", null, rootA.Id)));

        // Updating another child under rootA to same name rejected.
        var childA2 = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Mặt bích", null, rootA.Id));
        await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.UpdateAsync(childA2.Id, new UpsertSupplierGroupRequest("PHỤ KIỆN", null, rootA.Id)));

        // Updating own description without renaming succeeds.
        var updated = await _groupService.UpdateAsync(
            childA2.Id,
            new UpsertSupplierGroupRequest("Mặt bích", "Mặt bích thép hàn", rootA.Id));
        Assert.Equal("Mặt bích thép hàn", updated.Description);
    }

    [Fact]
    public async Task Update_Guards_SelfParent_Level3_And_ParentWithChildren()
    {
        var parent = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư Điện", null));
        var child = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Tủ điện", null, parent.Id));
        var otherRoot = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Hệ ống thép", null));

        // Self-parenting blocked.
        await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.UpdateAsync(parent.Id, new UpsertSupplierGroupRequest(parent.Name, null, parent.Id)));

        // Moving empty root under a child (would create level 3) blocked.
        await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.UpdateAsync(otherRoot.Id, new UpsertSupplierGroupRequest(otherRoot.Name, null, child.Id)));

        // Converting a parent that has active children into a child blocked.
        await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.UpdateAsync(parent.Id, new UpsertSupplierGroupRequest(parent.Name, null, otherRoot.Id)));

        // Moving a child to root works.
        var promoted = await _groupService.UpdateAsync(child.Id, new UpsertSupplierGroupRequest("Tủ điện", null, null));
        Assert.Null(promoted.ParentGroupId);
        Assert.Null(promoted.ParentGroupName);

        // Now parent has no children and can become a child of otherRoot.
        var demoted = await _groupService.UpdateAsync(parent.Id, new UpsertSupplierGroupRequest(parent.Name, null, otherRoot.Id));
        Assert.Equal(otherRoot.Id, demoted.ParentGroupId);
        Assert.Equal("Hệ ống thép", demoted.ParentGroupName);
    }

    [Fact]
    public async Task Delete_BlocksParentWithActiveChildren_AndSucceedsAfterChildrenDeleted()
    {
        var parent = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư Điện", null));
        var child1 = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Dây cáp điện", null, parent.Id));
        var child2 = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Tủ điện", null, parent.Id));

        var ex = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.DeleteAsync(parent.Id));

        Assert.Equal(
            "Không thể xóa nhóm lớn 'Vật tư Điện' vì đang có 2 mục nhỏ. Vui lòng xóa hoặc chuyển các mục nhỏ trước.",
            ex.Message);

        await _groupService.DeleteAsync(child1.Id);
        await _groupService.DeleteAsync(child2.Id);

        await _groupService.DeleteAsync(parent.Id);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _groupService.GetAsync(parent.Id));
    }

    [Fact]
    public async Task ListAndGet_ParentSupplierCount_IsDistinctAcrossParentAndChildren_ExcludesSoftDeletedSuppliers()
    {
        var parent = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư Điện", null));
        var child1 = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Dây cáp điện", null, parent.Id));
        var child2 = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Thiết bị đóng cắt", null, parent.Id));

        var s1 = new Supplier { SupplierCode = "NCC-001", SupplierName = "Công ty Cadivi" };
        var s2 = new Supplier { SupplierCode = "NCC-002", SupplierName = "Công ty LS Vina" };
        var s3 = new Supplier { SupplierCode = "NCC-003", SupplierName = "Công ty Điện Quang" };
        var sDeleted = new Supplier
        {
            SupplierCode = "NCC-004",
            SupplierName = "Công ty Đã Xóa",
            DeletedAt = DateTime.UtcNow,
        };
        _db.Suppliers.AddRange(s1, s2, s3, sDeleted);
        await _db.SaveChangesAsync();

        _db.SupplierGroupMembers.AddRange(
            new SupplierGroupMember { SupplierGroupId = child1.Id, SupplierId = s1.Id },
            new SupplierGroupMember { SupplierGroupId = child2.Id, SupplierId = s1.Id },
            new SupplierGroupMember { SupplierGroupId = parent.Id, SupplierId = s1.Id },
            new SupplierGroupMember { SupplierGroupId = child1.Id, SupplierId = s2.Id },
            new SupplierGroupMember { SupplierGroupId = parent.Id, SupplierId = s3.Id },
            new SupplierGroupMember { SupplierGroupId = child2.Id, SupplierId = sDeleted.Id });
        await _db.SaveChangesAsync();

        var list = await _groupService.ListAsync(null, 1, 500);
        var parentDto = Assert.Single(list.Items, x => x.Id == parent.Id);
        var child1Dto = Assert.Single(list.Items, x => x.Id == child1.Id);
        var child2Dto = Assert.Single(list.Items, x => x.Id == child2.Id);

        Assert.Equal(2, parentDto.ChildCount);
        Assert.Equal(3, parentDto.SupplierCount); // s1, s2, s3 (distinct; sDeleted ignored)
        Assert.Equal(2, child1Dto.SupplierCount); // s1, s2
        Assert.Equal(1, child2Dto.SupplierCount); // s1 only

        var getParent = await _groupService.GetAsync(parent.Id);
        Assert.Equal(3, getParent.SupplierCount);
        Assert.Equal(2, getParent.ChildCount);

        var getChild1 = await _groupService.GetAsync(child1.Id);
        Assert.Equal(2, getChild1.SupplierCount);
        Assert.Equal("Vật tư Điện", getChild1.ParentGroupName);
    }

    [Fact]
    public async Task ListAsync_SearchFilter_MatchesParentAndChildCorrectly()
    {
        var p1 = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư Điện", "Thiết bị điện hạ thế"));
        var c1 = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Dây cáp điện", null, p1.Id));
        var c2 = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Ống luồn PVC", null, p1.Id));
        var p2 = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Hệ van công nghiệp", null));
        _ = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Van bướm", null, p2.Id));

        // Parent match includes parent + all its children.
        var byParent = await _groupService.ListAsync("hạ thế", 1, 500);
        Assert.Equal([p1.Id, c1.Id, c2.Id], byParent.Items.Select(x => x.Id).ToArray());

        // Child match includes its parent + only matching child.
        var byChild = await _groupService.ListAsync("Ống luồn", 1, 500);
        Assert.Equal([p1.Id, c2.Id], byChild.Items.Select(x => x.Id).ToArray());
        Assert.Equal(2, byChild.Items[0].ChildCount);
    }

    [Fact]
    public async Task SetGroupsOfSupplier_LeafOnlyNormalization_DropsBareParentWhenChildSelected()
    {
        var p1 = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư Điện", null));
        var c1 = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Dây cáp điện", null, p1.Id));
        var p2 = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Hệ ống thép", null));

        var s1 = new Supplier { SupplierCode = "NCC-101", SupplierName = "NCC Thiết Bị Điện" };
        var s2 = new Supplier { SupplierCode = "NCC-102", SupplierName = "NCC Tổng Hợp" };
        _db.Suppliers.AddRange(s1, s2);
        await _db.SaveChangesAsync();

        // Selecting both parent P1 and child C1 drops bare P1, keeps C1 and standalone P2.
        var saved1 = await _groupService.SetGroupsOfSupplierAsync(
            s1.Id,
            new SetSupplierGroupsRequest([p1.Id, c1.Id, p2.Id]));

        Assert.Equal(2, saved1.Count);
        Assert.Contains(saved1, g => g.Id == p2.Id && g.ParentGroupId == null && g.ParentGroupName == null);
        Assert.Contains(saved1, g => g.Id == c1.Id && g.ParentGroupId == p1.Id && g.ParentGroupName == "Vật tư Điện");
        Assert.DoesNotContain(saved1, g => g.Id == p1.Id);

        // Selecting bare P1 alone is allowed.
        var savedBare = await _groupService.SetGroupsOfSupplierAsync(
            s2.Id,
            new SetSupplierGroupsRequest([p1.Id]));
        var bareItem = Assert.Single(savedBare);
        Assert.Equal(p1.Id, bareItem.Id);
        Assert.Null(bareItem.ParentGroupId);

        // Updating S2 with both P1 and C1 removes bare P1 from DB and retains only C1.
        var savedNormalized = await _groupService.SetGroupsOfSupplierAsync(
            s2.Id,
            new SetSupplierGroupsRequest([p1.Id, c1.Id]));
        var leafItem = Assert.Single(savedNormalized);
        Assert.Equal(c1.Id, leafItem.Id);
    }

    [Fact]
    public async Task SupplierDirectory_ParentInclusiveFilter_And_FormatsParentChildLabels()
    {
        var elec = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư Điện", null));
        var cable = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Dây cáp điện", null, elec.Id));
        var steel = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Hệ ống thép", null));

        var supCable = new Supplier { SupplierCode = "NCC-201", SupplierName = "Công ty Cáp Thịnh Phát" };
        var supSteel = new Supplier { SupplierCode = "NCC-202", SupplierName = "Công ty Thép Hòa Phát" };
        _db.Suppliers.AddRange(supCable, supSteel);
        await _db.SaveChangesAsync();

        await _groupService.SetGroupsOfSupplierAsync(supCable.Id, new SetSupplierGroupsRequest([cable.Id]));
        await _groupService.SetGroupsOfSupplierAsync(supSteel.Id, new SetSupplierGroupsRequest([steel.Id]));

        // Filter by parent group ID must return supplier assigned to child "Dây cáp điện".
        var byParent = await _directoryService.GetAllAsync(page: 1, pageSize: 20, groupId: elec.Id);
        var cableSupDto = Assert.Single(byParent.Items);
        Assert.Equal(supCable.Id, cableSupDto.Id);
        Assert.Equal(["Vật tư Điện › Dây cáp điện"], cableSupDto.IndustryGroups);

        // Filter by child group ID returns the same supplier.
        var byChild = await _directoryService.GetAllAsync(page: 1, pageSize: 20, groupId: cable.Id);
        Assert.Single(byChild.Items);

        // Filter by standalone group returns only standalone supplier with plain group name.
        var bySteel = await _directoryService.GetAllAsync(page: 1, pageSize: 20, groupId: steel.Id);
        var steelSupDto = Assert.Single(bySteel.Items);
        Assert.Equal(["Hệ ống thép"], steelSupDto.IndustryGroups);

        // GetByIdAsync also formats "Parent › Child".
        var detail = await _directoryService.GetByIdAsync(supCable.Id);
        Assert.NotNull(detail);
        Assert.Equal(["Vật tư Điện › Dây cáp điện"], detail.IndustryGroups);
    }

    [Fact]
    public async Task SupplierExcelImport_SupportsParentChildPaths_AmbiguityGuard_And_Deduplicates()
    {
        var elec = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư Điện", null));
        var cable = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Dây cáp điện", null, elec.Id));
        var elecAcc = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Phụ kiện", null, elec.Id));

        var steel = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Hệ ống thép", null));
        var steelAcc = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Phụ kiện", null, steel.Id));

        var standalone = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư phụ", null));

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("DanhSachNCC");
        ws.Cell(1, 1).Value = "Mã NCC";
        ws.Cell(1, 2).Value = "Tên nhà cung cấp";
        ws.Cell(1, 11).Value = "Nhóm ngành hàng";

        // Row 2: same child specified twice via '>' and '›' -> must resolve & deduplicate into 1 link.
        ws.Cell(2, 1).Value = "NCC-XL-01";
        ws.Cell(2, 2).Value = "Công ty Cáp Việt";
        ws.Cell(2, 11).Value = "Vật tư Điện > Dây cáp điện; Vật tư Điện › Dây cáp điện";

        // Row 3: whitespace-free '>' path + standalone group.
        ws.Cell(3, 1).Value = "NCC-XL-02";
        ws.Cell(3, 2).Value = "Công ty Ống Phụ Kiện";
        ws.Cell(3, 11).Value = "Hệ ống thép>Phụ kiện, Vật tư phụ";

        // Row 4: ambiguous plain child name without parent prefix -> must NOT pick arbitrarily.
        ws.Cell(4, 1).Value = "NCC-XL-03";
        ws.Cell(4, 2).Value = "Công ty Phụ Kiện Chung";
        ws.Cell(4, 11).Value = "Phụ kiện";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var (result, errorMessage) = await _excelService.ImportExcelAsync(ms, null);
        Assert.Null(errorMessage);
        Assert.NotNull(result);
        Assert.Equal(3, result.ImportedCount);
        Assert.Equal(0, result.FailedCount);

        var sup1 = await _db.Suppliers.FirstAsync(s => s.SupplierCode == "NCC-XL-01");
        var sup1Groups = await _db.SupplierGroupMembers
            .Where(m => m.SupplierId == sup1.Id)
            .Select(m => m.SupplierGroupId)
            .ToListAsync();
        Assert.Equal([cable.Id], sup1Groups);

        var sup2 = await _db.Suppliers.FirstAsync(s => s.SupplierCode == "NCC-XL-02");
        var sup2Groups = await _db.SupplierGroupMembers
            .Where(m => m.SupplierId == sup2.Id)
            .Select(m => m.SupplierGroupId)
            .OrderBy(id => id)
            .ToListAsync();
        Assert.Equal(new[] { steelAcc.Id, standalone.Id }.OrderBy(x => x).ToArray(), sup2Groups);

        var sup3 = await _db.Suppliers.FirstAsync(s => s.SupplierCode == "NCC-XL-03");
        var sup3Groups = await _db.SupplierGroupMembers
            .Where(m => m.SupplierId == sup3.Id)
            .ToListAsync();
        Assert.Empty(sup3Groups);
        Assert.NotEqual(elecAcc.Id, steelAcc.Id);
    }

    [Fact]
    public async Task SupplierExcelImport_PrefersRootOverSameNamedChildForPlainToken_AndSupportsExplicitPath()
    {
        var elec = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư Điện", null));
        var childCommon = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư chung", null, elec.Id));
        var rootCommon = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư chung", null));

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("DanhSachNCC");
        ws.Cell(1, 1).Value = "Mã NCC";
        ws.Cell(1, 2).Value = "Tên nhà cung cấp";
        ws.Cell(1, 11).Value = "Nhóm ngành hàng";

        ws.Cell(2, 1).Value = "NCC-COM-01";
        ws.Cell(2, 2).Value = "Công ty Dùng Nhóm Lớn";
        ws.Cell(2, 11).Value = "Vật tư chung";

        ws.Cell(3, 1).Value = "NCC-COM-02";
        ws.Cell(3, 2).Value = "Công ty Dùng Mục Nhỏ";
        ws.Cell(3, 11).Value = "Vật tư Điện > Vật tư chung";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var (result, errorMessage) = await _excelService.ImportExcelAsync(ms, null);
        Assert.Null(errorMessage);
        Assert.NotNull(result);
        Assert.Equal(2, result.ImportedCount);

        var supRoot = await _db.Suppliers.FirstAsync(s => s.SupplierCode == "NCC-COM-01");
        var rootLinks = await _db.SupplierGroupMembers
            .Where(m => m.SupplierId == supRoot.Id)
            .Select(m => m.SupplierGroupId)
            .ToListAsync();
        Assert.Equal([rootCommon.Id], rootLinks);

        var supChild = await _db.Suppliers.FirstAsync(s => s.SupplierCode == "NCC-COM-02");
        var childLinks = await _db.SupplierGroupMembers
            .Where(m => m.SupplierId == supChild.Id)
            .Select(m => m.SupplierGroupId)
            .ToListAsync();
        Assert.Equal([childCommon.Id], childLinks);
    }

    [Fact]
    public async Task SupplierGroupsController_EndToEndHierarchyFlow_WorksCleanly()
    {
        var controller = new SupplierGroupsController(_groupService);

        var createParent = await controller.Create(
            new UpsertSupplierGroupRequest("Vật tư Nước", "Hệ cấp thoát nước"),
            CancellationToken.None);
        var createdParent = Assert.IsType<SupplierGroupDto>(
            Assert.IsType<CreatedAtActionResult>(createParent.Result).Value);

        var createChild = await controller.Create(
            new UpsertSupplierGroupRequest("Bơm công nghiệp", null, createdParent.Id),
            CancellationToken.None);
        var createdChild = Assert.IsType<SupplierGroupDto>(
            Assert.IsType<CreatedAtActionResult>(createChild.Result).Value);
        Assert.Equal(createdParent.Id, createdChild.ParentGroupId);
        Assert.Equal("Vật tư Nước", createdChild.ParentGroupName);

        var listRes = await controller.List(null, 1, 500, CancellationToken.None);
        var list = Assert.IsType<SupplierGroupPagedResponse>(
            Assert.IsType<OkObjectResult>(listRes.Result).Value);
        Assert.Equal(2, list.TotalCount);
        Assert.Equal(1, list.Items[0].ChildCount);

        await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            controller.Delete(createdParent.Id, CancellationToken.None));

        Assert.IsType<NoContentResult>(await controller.Delete(createdChild.Id, CancellationToken.None));
        Assert.IsType<NoContentResult>(await controller.Delete(createdParent.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WithSubGroups_CreatesParentAndChildrenAtomically()
    {
        var created = await _groupService.CreateAsync(new UpsertSupplierGroupRequest(
            "Vật tư Điện",
            "Nhóm thiết bị điện",
            ParentGroupId: null,
            SubGroups:
            [
                new UpsertSupplierSubGroupItem(null, "  Dây cáp điện  ", "  Cáp đồng  "),
                new UpsertSupplierSubGroupItem(null, "Tủ điện", null),
            ]));

        Assert.Equal("Vật tư Điện", created.Name);
        Assert.Equal("Nhóm thiết bị điện", created.Description);
        Assert.Null(created.ParentGroupId);
        Assert.Null(created.ParentGroupName);
        Assert.Equal(2, created.ChildCount);

        var page = await _groupService.ListAsync(null, 1, 500);
        Assert.Equal(3, page.TotalCount);
        Assert.Collection(page.Items,
            parent =>
            {
                Assert.Equal(created.Id, parent.Id);
                Assert.Null(parent.ParentGroupId);
                Assert.Equal(2, parent.ChildCount);
            },
            child1 =>
            {
                Assert.Equal("Dây cáp điện", child1.Name);
                Assert.Equal("Cáp đồng", child1.Description);
                Assert.Equal(created.Id, child1.ParentGroupId);
                Assert.Equal("Vật tư Điện", child1.ParentGroupName);
                Assert.Equal(0, child1.ChildCount);
            },
            child2 =>
            {
                Assert.Equal("Tủ điện", child2.Name);
                Assert.Null(child2.Description);
                Assert.Equal(created.Id, child2.ParentGroupId);
                Assert.Equal("Vật tư Điện", child2.ParentGroupName);
                Assert.Equal(0, child2.ChildCount);
            });
    }

    [Fact]
    public async Task CreateAsync_WithSubGroupsAndNonNullParentGroupId_ThrowsConflictAndLeavesZeroRows()
    {
        var parent = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư Điện", null));

        var ex = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.CreateAsync(new UpsertSupplierGroupRequest(
                "Nhóm con",
                null,
                parent.Id,
                [new UpsertSupplierSubGroupItem(null, "Cáp đồng", null)])));

        Assert.Equal("Chỉ Nhóm lớn mới có thể chứa mục nhỏ.", ex.Message);
        Assert.Equal(1, await _db.SupplierGroups.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateOrInvalidSubGroupNames_ThrowsConflictAndLeavesZeroPartialParent()
    {
        var dupEx = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.CreateAsync(new UpsertSupplierGroupRequest(
                "Hệ van công nghiệp",
                null,
                null,
                [
                    new UpsertSupplierSubGroupItem(null, "Van bướm", null),
                    new UpsertSupplierSubGroupItem(null, "  van bướm ", "Trùng tên"),
                ])));

        Assert.Contains("bị trùng trong danh sách", dupEx.Message);
        Assert.Equal(0, await _db.SupplierGroups.CountAsync());

        var blankEx = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.CreateAsync(new UpsertSupplierGroupRequest(
                "Hệ van công nghiệp",
                null,
                null,
                [
                    new UpsertSupplierSubGroupItem(null, "Van bướm", null),
                    new UpsertSupplierSubGroupItem(null, "   ", null),
                ])));

        Assert.Equal("Tên nhóm NCC là bắt buộc.", blankEx.Message);
        Assert.Equal(0, await _db.SupplierGroups.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_WithSubGroups_UpdatesExistingAndAddsNewInSingleCall()
    {
        var created = await _groupService.CreateAsync(new UpsertSupplierGroupRequest(
            "Vật tư Điện",
            "Mô tả cũ",
            null,
            [
                new UpsertSupplierSubGroupItem(null, "Dây cáp", "Cáp hạ thế"),
                new UpsertSupplierSubGroupItem(null, "Tủ điện", "Tủ MSB"),
            ]));

        var children = await _db.SupplierGroups
            .Where(g => g.ParentGroupId == created.Id)
            .OrderBy(g => g.Name)
            .ToListAsync();
        var cable = Assert.Single(children, c => c.Name == "Dây cáp");
        var panel = Assert.Single(children, c => c.Name == "Tủ điện");

        var updated = await _groupService.UpdateAsync(
            created.Id,
            new UpsertSupplierGroupRequest(
                "Vật tư Điện CN",
                "Mô tả mới",
                null,
                [
                    new UpsertSupplierSubGroupItem(cable.Id, "Cáp động lực", "Cáp XLPE"),
                    new UpsertSupplierSubGroupItem(null, "Thiết bị đóng cắt", "MCB & MCCB"),
                ]));

        Assert.Equal("Vật tư Điện CN", updated.Name);
        Assert.Equal("Mô tả mới", updated.Description);
        Assert.Equal(3, updated.ChildCount);

        var page = await _groupService.ListAsync(null, 1, 500);
        var updatedCable = Assert.Single(page.Items, i => i.Id == cable.Id);
        Assert.Equal("Cáp động lực", updatedCable.Name);
        Assert.Equal("Cáp XLPE", updatedCable.Description);
        Assert.Equal("Vật tư Điện CN", updatedCable.ParentGroupName);

        var untouchedPanel = Assert.Single(page.Items, i => i.Id == panel.Id);
        Assert.Equal("Tủ điện", untouchedPanel.Name);
        Assert.Equal("Tủ MSB", untouchedPanel.Description);

        var addedSwitchgear = Assert.Single(page.Items, i => i.Name == "Thiết bị đóng cắt");
        Assert.Equal("MCB & MCCB", addedSwitchgear.Description);
        Assert.Equal(created.Id, addedSwitchgear.ParentGroupId);
        Assert.Equal("Vật tư Điện CN", addedSwitchgear.ParentGroupName);
    }

    [Fact]
    public async Task UpdateAsync_WithNullSubGroups_LeavesExistingChildrenUntouched()
    {
        var created = await _groupService.CreateAsync(new UpsertSupplierGroupRequest(
            "Vật tư Điện",
            null,
            null,
            [
                new UpsertSupplierSubGroupItem(null, "Dây cáp", null),
                new UpsertSupplierSubGroupItem(null, "Tủ điện", null),
            ]));

        var updated = await _groupService.UpdateAsync(
            created.Id,
            new UpsertSupplierGroupRequest("Vật tư Điện Cao Thế", "Đã đổi tên nhóm lớn", null, null));

        Assert.Equal("Vật tư Điện Cao Thế", updated.Name);
        Assert.Equal("Đã đổi tên nhóm lớn", updated.Description);
        Assert.Equal(2, updated.ChildCount);

        var children = await _db.SupplierGroups.Where(g => g.ParentGroupId == created.Id).ToListAsync();
        Assert.Equal(2, children.Count);
        Assert.Contains(children, c => c.Name == "Dây cáp");
        Assert.Contains(children, c => c.Name == "Tủ điện");
    }

    [Fact]
    public async Task UpdateAsync_WithSubGroups_SwapRenameAndNewClaimingOldName_SucceedsUnderUniqueIndexConstraint()
    {
        var interceptor = new UniqueSupplierGroupNameInterceptor();
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"supplier-group-swap-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(interceptor)
            .Options;

        await using var guardedDb = new WarehouseDbContext(options);
        var service = new SupplierGroupService(guardedDb);

        var parent = await service.CreateAsync(new UpsertSupplierGroupRequest(
            "Vật tư Điện",
            null,
            null,
            [
                new UpsertSupplierSubGroupItem(null, "Dây cáp", "Mô tả 1"),
                new UpsertSupplierSubGroupItem(null, "Tủ điện", "Mô tả 2"),
            ]));

        var children = await guardedDb.SupplierGroups
            .Where(g => g.ParentGroupId == parent.Id)
            .ToListAsync();
        var child1 = Assert.Single(children, c => c.Name == "Dây cáp");
        var child2 = Assert.Single(children, c => c.Name == "Tủ điện");

        interceptor.ResetObservedNames();

        var swapped = await service.UpdateAsync(
            parent.Id,
            new UpsertSupplierGroupRequest(
                "Vật tư Điện",
                null,
                null,
                [
                    new UpsertSupplierSubGroupItem(child1.Id, "Tủ điện", "Mô tả mới cho 1"),
                    new UpsertSupplierSubGroupItem(child2.Id, "Dây cáp", "Mô tả mới cho 2"),
                ]));

        Assert.Equal(2, swapped.ChildCount);
        Assert.Contains(interceptor.ObservedSavedNames, n => n.StartsWith("__tmp_swap_", StringComparison.Ordinal) && n.Length <= 255);

        var reloaded1 = await guardedDb.SupplierGroups.FirstAsync(g => g.Id == child1.Id);
        var reloaded2 = await guardedDb.SupplierGroups.FirstAsync(g => g.Id == child2.Id);
        Assert.Equal("Tủ điện", reloaded1.Name);
        Assert.Equal("Mô tả mới cho 1", reloaded1.Description);
        Assert.Equal("Dây cáp", reloaded2.Name);
        Assert.Equal("Mô tả mới cho 2", reloaded2.Description);

        // Also verify renaming an existing child while a new child claims the old name succeeds without unique collision.
        var afterReclaim = await service.UpdateAsync(
            parent.Id,
            new UpsertSupplierGroupRequest(
                "Vật tư Điện",
                null,
                null,
                [
                    new UpsertSupplierSubGroupItem(child1.Id, "Tủ điện trung thế", "Đổi tên"),
                    new UpsertSupplierSubGroupItem(null, "Tủ điện", "Mục mới lấy lại tên cũ"),
                ]));

        Assert.Equal(3, afterReclaim.ChildCount);
        var allChildren = await guardedDb.SupplierGroups.Where(g => g.ParentGroupId == parent.Id).ToListAsync();
        Assert.Contains(allChildren, c => c.Id == child1.Id && c.Name == "Tủ điện trung thế");
        Assert.Contains(allChildren, c => c.Id != child1.Id && c.Name == "Tủ điện" && c.Description == "Mục mới lấy lại tên cũ");
    }

    [Fact]
    public async Task UpdateAsync_WithSubGroups_RejectsSiblingCollision_DuplicateIds_ForeignId_AndNonNullParent()
    {
        var parentA = await _groupService.CreateAsync(new UpsertSupplierGroupRequest(
            "Vật tư Điện",
            null,
            null,
            [
                new UpsertSupplierSubGroupItem(null, "Dây cáp", "Ban đầu"),
                new UpsertSupplierSubGroupItem(null, "Tủ điện", null),
            ]));
        var parentB = await _groupService.CreateAsync(new UpsertSupplierGroupRequest(
            "Hệ ống thép",
            null,
            null,
            [new UpsertSupplierSubGroupItem(null, "Ống mạ kẽm", null)]));

        var childrenA = await _db.SupplierGroups.Where(g => g.ParentGroupId == parentA.Id).ToListAsync();
        var cable = Assert.Single(childrenA, c => c.Name == "Dây cáp");
        var foreignChild = await _db.SupplierGroups.FirstAsync(g => g.ParentGroupId == parentB.Id);

        // 1. New child colliding with untouched sibling ("Tủ điện").
        var siblingEx = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.UpdateAsync(
                parentA.Id,
                new UpsertSupplierGroupRequest(
                    "Vật tư Điện (Đổi tên)",
                    null,
                    null,
                    [
                        new UpsertSupplierSubGroupItem(cable.Id, "Cáp đồng", null),
                        new UpsertSupplierSubGroupItem(null, "tủ điện", null),
                    ])));
        Assert.Equal("Mục nhỏ 'tủ điện' đã tồn tại trong nhóm 'Vật tư Điện'.", siblingEx.Message);

        // Verify zero partial mutation occurred on parentA or cable.
        var unchangedParent = await _groupService.GetAsync(parentA.Id);
        Assert.Equal("Vật tư Điện", unchangedParent.Name);
        var unchangedCable = await _groupService.GetAsync(cable.Id);
        Assert.Equal("Dây cáp", unchangedCable.Name);

        // 2. Duplicate IDs in SubGroups.
        var dupIdEx = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.UpdateAsync(
                parentA.Id,
                new UpsertSupplierGroupRequest(
                    "Vật tư Điện",
                    null,
                    null,
                    [
                        new UpsertSupplierSubGroupItem(cable.Id, "Tên 1", null),
                        new UpsertSupplierSubGroupItem(cable.Id, "Tên 2", null),
                    ])));
        Assert.Equal("Danh sách mục nhỏ có ID bị trùng.", dupIdEx.Message);

        // 3. Foreign child ID (not belonging to parentA).
        var foreignEx = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.UpdateAsync(
                parentA.Id,
                new UpsertSupplierGroupRequest(
                    "Vật tư Điện",
                    null,
                    null,
                    [new UpsertSupplierSubGroupItem(foreignChild.Id, "Ống thép", null)])));
        Assert.Equal("Mục nhỏ không thuộc nhóm lớn này hoặc đã bị xóa.", foreignEx.Message);

        // 4. Non-null ParentGroupId when SubGroups is non-null.
        var standalone = await _groupService.CreateAsync(new UpsertSupplierGroupRequest("Vật tư phụ", null));
        var parentGuardEx = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _groupService.UpdateAsync(
                standalone.Id,
                new UpsertSupplierGroupRequest(
                    "Vật tư phụ",
                    null,
                    parentA.Id,
                    [])));
        Assert.Equal("Chỉ Nhóm lớn mới có thể chứa mục nhỏ.", parentGuardEx.Message);
    }

    private sealed class UniqueSupplierGroupNameInterceptor : SaveChangesInterceptor
    {
        public List<string> ObservedSavedNames { get; } = [];

        public void ResetObservedNames() => ObservedSavedNames.Clear();

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is not null)
            {
                var activeGroups = eventData.Context.ChangeTracker
                    .Entries<SupplierGroup>()
                    .Where(e => e.State != EntityState.Deleted && e.Entity.DeletedAt == null)
                    .Select(e => e.Entity)
                    .ToList();

                foreach (var entry in eventData.Context.ChangeTracker.Entries<SupplierGroup>()
                             .Where(e => e.State is EntityState.Added or EntityState.Modified))
                {
                    ObservedSavedNames.Add(entry.Entity.Name);
                }

                var duplicateChild = activeGroups
                    .Where(g => g.ParentGroupId != null)
                    .GroupBy(g => (ParentId: g.ParentGroupId!.Value, g.Name), KeyComparer.Instance)
                    .FirstOrDefault(g => g.Count() > 1);

                if (duplicateChild is not null)
                {
                    throw new InvalidOperationException(
                        $"Simulated IX_supplier_groups_ParentGroupId_Name violation: ({duplicateChild.Key.ParentId}, {duplicateChild.Key.Name})");
                }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private sealed class KeyComparer : IEqualityComparer<(int ParentId, string Name)>
        {
            public static readonly KeyComparer Instance = new();
            public bool Equals((int ParentId, string Name) x, (int ParentId, string Name) y) =>
                x.ParentId == y.ParentId && string.Equals(x.Name, y.Name, StringComparison.Ordinal);
            public int GetHashCode((int ParentId, string Name) obj) =>
                HashCode.Combine(obj.ParentId, StringComparer.Ordinal.GetHashCode(obj.Name));
        }
    }
}
