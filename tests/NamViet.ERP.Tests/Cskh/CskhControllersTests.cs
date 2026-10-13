using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Services.Cskh;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using Xunit;

namespace NamViet.ERP.Tests.Cskh;

public class CskhControllersTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly ICskhCoordinationService _cskhService;
    private readonly ProjectBottlenecksController _bottlenecksController;
    private readonly ProjectRequestsController _requestsController;
    private readonly ProjectMeetingsController _meetingsController;
    private const string TestProjectCode = "DA-2026-TEST";

    public CskhControllersTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);

        // Seed project
        _db.Projects.Add(new Project
        {
            ProjectCode = TestProjectCode,
            ProjectName = "Dự án Thử nghiệm CSKH",
            ProjectType = ProjectType.Project,
            PmName = "Nguyễn Văn Test",
            StartDate = new DateOnly(2026, 1, 1),
            Status = ProjectStatus.Active,
        });
        _db.SaveChanges();

        _cskhService = new CskhCoordinationService(_db);
        _bottlenecksController = new ProjectBottlenecksController(_cskhService);
        _requestsController = new ProjectRequestsController(_cskhService);
        _meetingsController = new ProjectMeetingsController(_cskhService);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // BOTTLENECK TESTS
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Bottlenecks_List_WhenEmpty_ReturnsOkWithEmptyList()
    {
        var result = _bottlenecksController.List(TestProjectCode);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ProjectBottleneckDto>>(okResult.Value);
        Assert.Empty(list);
    }

    [Fact]
    public void Bottlenecks_Create_WithValidData_ReturnsCreatedAndPersists()
    {
        var request = new CreateProjectBottleneckRequest(
            Name: "Chậm tiến độ giao cáp điện 3 pha",
            Severity: "urgent",
            StartedAt: "2026-03-01",
            ExpectedAt: "2026-03-10",
            Reason: "Nhà cung cấp giao hàng trễ",
            Pic: "Kỹ sư Điện",
            Source: "manual"
        );

        var result = _bottlenecksController.Create(TestProjectCode, request);

        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        var dto = Assert.IsType<ProjectBottleneckDto>(createdResult.Value);
        Assert.Equal("Chậm tiến độ giao cáp điện 3 pha", dto.Name);
        Assert.Equal("urgent", dto.Severity);
        Assert.Equal("open", dto.Status);
        Assert.NotEmpty(dto.Id);

        // Verify list now has 1
        var listResult = _bottlenecksController.List(TestProjectCode);
        var okResult = Assert.IsType<OkObjectResult>(listResult.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ProjectBottleneckDto>>(okResult.Value);
        Assert.Single(list);
    }

    [Fact]
    public void Bottlenecks_Create_WithEmptyName_ReturnsBadRequest()
    {
        var request = new CreateProjectBottleneckRequest(
            Name: "",
            Severity: "low",
            StartedAt: "2026-03-01",
            ExpectedAt: "2026-03-05",
            Reason: "Không rõ"
        );

        var result = _bottlenecksController.Create(TestProjectCode, request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Bottlenecks_Update_Existing_ReturnsOkWithUpdatedValues()
    {
        var createRequest = new CreateProjectBottleneckRequest(
            Name: "Thiếu bulong móng M24",
            Severity: "medium",
            StartedAt: "2026-03-02",
            ExpectedAt: "2026-03-08",
            Reason: "Kho chưa nhập đủ số lượng"
        );
        var created = Assert.IsType<CreatedResult>(_bottlenecksController.Create(TestProjectCode, createRequest).Result);
        var createdDto = Assert.IsType<ProjectBottleneckDto>(created.Value);

        var updateRequest = new UpdateProjectBottleneckRequest(
            Name: "Thiếu bulong móng M24 - Đã đặt bổ sung",
            Severity: "low",
            Status: "closed",
            ExpectedAt: "2026-03-06",
            Reason: "Đã có hàng thay thế",
            GanttTask: null,
            DepartmentId: null,
            Pic: "Thủ kho",
            Resolution: "Đã xuất kho đầy đủ",
            NoteText: "Cập nhật giải pháp xử lý"
        );

        var updateResult = _bottlenecksController.Update(TestProjectCode, createdDto.Id, updateRequest);

        var okResult = Assert.IsType<OkObjectResult>(updateResult.Result);
        var updatedDto = Assert.IsType<ProjectBottleneckDto>(okResult.Value);
        Assert.Equal("Thiếu bulong móng M24 - Đã đặt bổ sung", updatedDto.Name);
        Assert.Equal("closed", updatedDto.Status);
        Assert.Equal("low", updatedDto.Severity);
    }

    [Fact]
    public void Bottlenecks_Update_NonExistent_ReturnsNotFound()
    {
        var updateRequest = new UpdateProjectBottleneckRequest(
            Name: "Test",
            Severity: null,
            Status: null,
            ExpectedAt: null,
            Reason: null,
            GanttTask: null,
            DepartmentId: null,
            Pic: null,
            Resolution: null,
            NoteText: null
        );

        var result = _bottlenecksController.Update(TestProjectCode, "non-existent-id", updateRequest);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void Bottlenecks_Delete_Existing_ReturnsNoContent()
    {
        var createRequest = new CreateProjectBottleneckRequest(
            Name: "Vấn đề cần xóa",
            Severity: "low",
            StartedAt: "2026-03-01",
            ExpectedAt: "2026-03-02",
            Reason: "Nhập nhầm"
        );
        var created = Assert.IsType<CreatedResult>(_bottlenecksController.Create(TestProjectCode, createRequest).Result);
        var createdDto = Assert.IsType<ProjectBottleneckDto>(created.Value);

        var deleteResult = _bottlenecksController.Delete(TestProjectCode, createdDto.Id);
        Assert.IsType<NoContentResult>(deleteResult);

        // Verify it is gone
        var listResult = _bottlenecksController.List(TestProjectCode);
        var okResult = Assert.IsType<OkObjectResult>(listResult.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ProjectBottleneckDto>>(okResult.Value);
        Assert.Empty(list);
    }

    [Fact]
    public void Bottlenecks_Delete_NonExistent_ReturnsNotFound()
    {
        var deleteResult = _bottlenecksController.Delete(TestProjectCode, "unknown-id");
        Assert.IsType<NotFoundResult>(deleteResult);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // REQUESTS (YÊU CẦU PHÁT SINH) TESTS
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Requests_List_WhenEmpty_ReturnsOkWithEmptyList()
    {
        var result = _requestsController.List(TestProjectCode);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ProjectRequestCenterDto>>(okResult.Value);
        Assert.Empty(list);
    }

    [Fact]
    public void Requests_Create_WithValidData_ReturnsCreated()
    {
        var request = new CreateProjectRequestCenterRequest(
            Name: "Yêu cầu cấp thêm 5 cuộn cáp hàn 25mm2",
            RequestType: "material_additional",
            FromParty: "Công trường",
            ToDept: "KHO",
            DueDate: "2026-03-15",
            Priority: "urgent",
            Body: "Cần gấp để thi công khu vực xưởng 2"
        );

        var result = _requestsController.Create(TestProjectCode, request);

        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        var dto = Assert.IsType<ProjectRequestCenterDto>(createdResult.Value);
        Assert.Equal("Yêu cầu cấp thêm 5 cuộn cáp hàn 25mm2", dto.Name);
        Assert.Equal("urgent", dto.Priority);
        Assert.Equal("open", dto.Status);
        Assert.NotEmpty(dto.Id);
    }

    [Fact]
    public void Requests_Create_WithEmptyName_ReturnsBadRequest()
    {
        var request = new CreateProjectRequestCenterRequest(
            Name: "   ",
            RequestType: "general",
            FromParty: "BGD",
            ToDept: null,
            DueDate: null,
            Priority: "low",
            Body: null
        );

        var result = _requestsController.Create(TestProjectCode, request);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Requests_Update_Existing_ReturnsOk()
    {
        var createRequest = new CreateProjectRequestCenterRequest(
            Name: "Yêu cầu kiểm định an toàn thang cáp",
            RequestType: "inspection",
            FromParty: "Tư vấn giám sát",
            ToDept: "KỸ THUẬT",
            DueDate: "2026-03-20",
            Priority: "medium",
            Body: "Biên bản kiểm tra kỹ thuật"
        );
        var created = Assert.IsType<CreatedResult>(_requestsController.Create(TestProjectCode, createRequest).Result);
        var createdDto = Assert.IsType<ProjectRequestCenterDto>(created.Value);

        var updateRequest = new UpdateProjectRequestCenterRequest(
            Status: "closed",
            Notes: "Đã hoàn thành kiểm định và có chứng chỉ"
        );

        var updateResult = _requestsController.Update(TestProjectCode, createdDto.Id, updateRequest);

        var okResult = Assert.IsType<OkObjectResult>(updateResult.Result);
        var updatedDto = Assert.IsType<ProjectRequestCenterDto>(okResult.Value);
        Assert.Equal("closed", updatedDto.Status);
    }

    [Fact]
    public void Requests_Update_NonExistent_ReturnsNotFound()
    {
        var updateRequest = new UpdateProjectRequestCenterRequest(
            Status: "closed",
            Notes: null
        );

        var result = _requestsController.Update(TestProjectCode, "random-id", updateRequest);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void Requests_Delete_Existing_ReturnsNoContent()
    {
        var createRequest = new CreateProjectRequestCenterRequest(
            Name: "Yêu cầu test",
            RequestType: "general",
            FromParty: "Kho",
            ToDept: null,
            DueDate: null,
            Priority: "low",
            Body: null
        );
        var created = Assert.IsType<CreatedResult>(_requestsController.Create(TestProjectCode, createRequest).Result);
        var createdDto = Assert.IsType<ProjectRequestCenterDto>(created.Value);

        var deleteResult = _requestsController.Delete(TestProjectCode, createdDto.Id);
        Assert.IsType<NoContentResult>(deleteResult);
    }

    [Fact]
    public void Requests_Delete_NonExistent_ReturnsNotFound()
    {
        var deleteResult = _requestsController.Delete(TestProjectCode, "unknown-id");
        Assert.IsType<NotFoundResult>(deleteResult);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // MEETINGS (CUỘC HỌP & HÀNH ĐỘNG) TESTS
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Meetings_List_WhenEmpty_ReturnsOkWithEmptyList()
    {
        var result = _meetingsController.List(TestProjectCode);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ProjectMeetingDto>>(okResult.Value);
        Assert.Empty(list);
    }

    [Fact]
    public void Meetings_Create_WithValidData_ReturnsCreatedWithActions()
    {
        var request = new CreateProjectMeetingRequest(
            Name: "Họp giao ban tiến độ tuần 10",
            Date: "2026-03-09",
            Attendees: "PM, Chỉ huy trưởng, Trưởng kho",
            Agenda: "Đánh giá tiến độ lắp đặt và cấp phát vật tư",
            Actions: new List<CreateProjectMeetingActionRequest>
            {
                new("Xuất kho lô sơn epoxy chống gỉ", "2026-03-12", "Thủ kho"),
                new("Nghiệm thu tĩnh trạm biến áp", "2026-03-15", "Kỹ sư điện")
            }
        );

        var result = _meetingsController.Create(TestProjectCode, request);

        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        var dto = Assert.IsType<ProjectMeetingDto>(createdResult.Value);
        Assert.Equal("Họp giao ban tiến độ tuần 10", dto.Name);
        Assert.Equal(2, dto.Actions.Count);
        Assert.False(dto.Actions[0].Done);
        Assert.NotEmpty(dto.Id);
    }

    [Fact]
    public void Meetings_Create_WithEmptyName_ReturnsBadRequest()
    {
        var request = new CreateProjectMeetingRequest(
            Name: "",
            Date: "2026-03-09",
            Attendees: null,
            Agenda: null,
            Actions: new List<CreateProjectMeetingActionRequest>()
        );

        var result = _meetingsController.Create(TestProjectCode, request);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Meetings_UpdateMinutes_Existing_UpdatesMinutesSuccessfully()
    {
        var createRequest = new CreateProjectMeetingRequest(
            Name: "Họp kỹ thuật giải pháp cáp",
            Date: "2026-03-10",
            Attendees: "PM, KS",
            Agenda: "Thống nhất chủng loại cáp",
            Actions: new List<CreateProjectMeetingActionRequest>()
        );
        var created = Assert.IsType<CreatedResult>(_meetingsController.Create(TestProjectCode, createRequest).Result);
        var createdDto = Assert.IsType<ProjectMeetingDto>(created.Value);

        var minutesRequest = new UpdateProjectMeetingMinutesRequest("Biên bản: Thống nhất dùng cáp Cadivi 4x50mm2.");
        var updateResult = _meetingsController.UpdateMinutes(TestProjectCode, createdDto.Id, minutesRequest);

        var okResult = Assert.IsType<OkObjectResult>(updateResult.Result);
        var updatedDto = Assert.IsType<ProjectMeetingDto>(okResult.Value);
        Assert.Equal("Biên bản: Thống nhất dùng cáp Cadivi 4x50mm2.", updatedDto.Minutes);
    }

    [Fact]
    public void Meetings_UpdateMinutes_NonExistent_ReturnsNotFound()
    {
        var minutesRequest = new UpdateProjectMeetingMinutesRequest("Nội dung họp");
        var result = _meetingsController.UpdateMinutes(TestProjectCode, "invalid-meeting-id", minutesRequest);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void Meetings_ToggleAction_Existing_TogglesDoneStatus()
    {
        var createRequest = new CreateProjectMeetingRequest(
            Name: "Họp điều phối vật tư",
            Date: "2026-03-10",
            Attendees: "Kho, Cung ứng",
            Agenda: "Bàn giao vật tư",
            Actions: new List<CreateProjectMeetingActionRequest>
            {
                new("Bàn giao chứng chỉ CO CQ thép tấm", "2026-03-14", "NV Cung ứng")
            }
        );
        var created = Assert.IsType<CreatedResult>(_meetingsController.Create(TestProjectCode, createRequest).Result);
        var createdDto = Assert.IsType<ProjectMeetingDto>(created.Value);
        var actionId = createdDto.Actions[0].Id;

        // Toggle to true
        var toggleRequest = new UpdateProjectMeetingActionRequest(actionId, Done: true);
        var toggleResult = _meetingsController.ToggleAction(TestProjectCode, createdDto.Id, actionId, toggleRequest);

        var okResult = Assert.IsType<OkObjectResult>(toggleResult.Result);
        var updatedDto = Assert.IsType<ProjectMeetingDto>(okResult.Value);
        Assert.True(updatedDto.Actions[0].Done);
    }

    [Fact]
    public void Meetings_ToggleAction_NonExistentMeeting_ReturnsNotFound()
    {
        var toggleRequest = new UpdateProjectMeetingActionRequest("act-1", Done: true);
        var result = _meetingsController.ToggleAction(TestProjectCode, "non-existent-meeting", "act-1", toggleRequest);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void Meetings_Delete_Existing_ReturnsNoContent()
    {
        var createRequest = new CreateProjectMeetingRequest(
            Name: "Cuộc họp cần xóa",
            Date: "2026-03-01",
            Attendees: null,
            Agenda: null,
            Actions: new List<CreateProjectMeetingActionRequest>()
        );
        var created = Assert.IsType<CreatedResult>(_meetingsController.Create(TestProjectCode, createRequest).Result);
        var createdDto = Assert.IsType<ProjectMeetingDto>(created.Value);

        var deleteResult = _meetingsController.Delete(TestProjectCode, createdDto.Id);
        Assert.IsType<NoContentResult>(deleteResult);

        // Verify it is deleted
        var listResult = _meetingsController.List(TestProjectCode);
        var okResult = Assert.IsType<OkObjectResult>(listResult.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ProjectMeetingDto>>(okResult.Value);
        Assert.Empty(list);
    }

    [Fact]
    public void Meetings_Delete_NonExistent_ReturnsNotFound()
    {
        var deleteResult = _meetingsController.Delete(TestProjectCode, "unknown-id");
        Assert.IsType<NotFoundResult>(deleteResult);
    }
}
