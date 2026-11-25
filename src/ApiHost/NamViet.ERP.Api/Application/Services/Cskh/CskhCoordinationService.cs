using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Time;

namespace NamViet.ERP.Api.Application.Services.Cskh;

public interface ICskhCoordinationService
{
    // Bottlenecks
    Task<IReadOnlyList<ProjectBottleneckDto>> GetBottlenecksAsync(string projectCode, CancellationToken ct = default);
    Task<ProjectBottleneckDto> CreateBottleneckAsync(string projectCode, CreateProjectBottleneckRequest request, CancellationToken ct = default);
    Task<ProjectBottleneckDto?> UpdateBottleneckAsync(string projectCode, string id, UpdateProjectBottleneckRequest request, CancellationToken ct = default);
    Task<bool> DeleteBottleneckAsync(string projectCode, string id, CancellationToken ct = default);

    // Documents
    Task<IReadOnlyList<ProjectDocumentDto>> GetDocumentsAsync(string projectCode, CancellationToken ct = default);
    Task<ProjectDocumentDto> CreateDocumentAsync(string projectCode, CreateProjectDocumentRequest request, CancellationToken ct = default);
    Task<ProjectDocumentDto?> UpdateDocumentStatusAsync(string projectCode, string id, UpdateProjectDocumentStatusRequest request, CancellationToken ct = default);
    Task<ProjectDocumentDto?> UpdateDocumentAsync(string projectCode, string id, CreateProjectDocumentRequest request, CancellationToken ct = default);
    Task<ProjectDocumentDto?> UpdateDocumentFileAsync(string projectCode, string id, string fileName, string fileSize, string downloadUrl, CancellationToken ct = default);
    Task<bool> DeleteDocumentAsync(string projectCode, string id, CancellationToken ct = default);

    // Requests
    Task<IReadOnlyList<ProjectRequestCenterDto>> GetRequestsAsync(string projectCode, CancellationToken ct = default);
    Task<ProjectRequestCenterDto> CreateRequestAsync(string projectCode, CreateProjectRequestCenterRequest request, CancellationToken ct = default);
    Task<ProjectRequestCenterDto?> UpdateRequestAsync(string projectCode, string id, UpdateProjectRequestCenterRequest request, CancellationToken ct = default);
    Task<bool> DeleteRequestAsync(string projectCode, string id, CancellationToken ct = default);

    // Meetings
    Task<IReadOnlyList<ProjectMeetingDto>> GetMeetingsAsync(string projectCode, CancellationToken ct = default);
    Task<ProjectMeetingDto> CreateMeetingAsync(string projectCode, CreateProjectMeetingRequest request, CancellationToken ct = default);
    Task<ProjectMeetingDto?> UpdateMeetingAsync(string projectCode, string id, CreateProjectMeetingRequest request, CancellationToken ct = default);
    Task<ProjectMeetingDto?> UpdateMeetingMinutesAsync(string projectCode, string id, UpdateProjectMeetingMinutesRequest request, CancellationToken ct = default);
    Task<ProjectMeetingDto?> ToggleMeetingActionAsync(string projectCode, string id, string actionId, bool done, CancellationToken ct = default);
    Task<bool> DeleteMeetingAsync(string projectCode, string id, CancellationToken ct = default);
}

public class CskhCoordinationService(WarehouseDbContext dbContext) : ICskhCoordinationService
{
    private readonly WarehouseDbContext _dbContext = dbContext;

    private static DateOnly ParseDate(string? value) => ParseNullableDate(value)
        ?? throw new DomainRuleException("Ngày không được để trống.");

    private static DateOnly? ParseNullableDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date)) return date;
        throw new DomainRuleException("Ngày phải có định dạng yyyy-MM-dd.");
    }

    private async Task<string> RequireProjectAsync(string projectCode, CancellationToken ct)
    {
        var code = (projectCode ?? string.Empty).Trim();
        if (!await _dbContext.Projects.AnyAsync(p => p.ProjectCode == code && p.DeletedAt == null, ct))
            throw new KeyNotFoundException("Không tìm thấy dự án.");
        return code;
    }

    // Keep one canonical key: codes containing spaces and hyphens may identify different projects.
    private async Task<(string clean, string alt)> NormalizeProjectCodeAsync(string projectCode, CancellationToken ct)
    {
        var code = await RequireProjectAsync(projectCode, ct);
        return (code, code);
    }

    private static string NewCode(string prefix) => $"{prefix}-{VietnamClock.Today:yyyyMMdd}-{Guid.NewGuid():N}";
    private static List<T> ReadJson<T>(string json) => JsonSerializer.Deserialize<List<T>>(json) ?? [];

    private static ProjectBottleneckDto Map(ProjectBottleneck e) => new(
        e.Id.ToString(), e.Code, e.ProjectCode, e.Title, e.ImpactLevel, e.Status,
        e.ReportedDate.ToString("yyyy-MM-dd"), e.ExpectedAt?.ToString("yyyy-MM-dd") ?? "",
        e.Reason, e.GanttTask, e.Department, e.Assignee, e.Resolution, e.Source,
        ReadJson<ProjectBottleneckHistoryNoteDto>(e.HistoryJson));

    public async Task<IReadOnlyList<ProjectBottleneckDto>> GetBottlenecksAsync(string projectCode, CancellationToken ct = default)
    {
        var code = await RequireProjectAsync(projectCode, ct);
        var items = await _dbContext.ProjectBottlenecks
            .AsNoTracking()
            .Where(x => x.ProjectCode == code)
            .ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<ProjectBottleneckDto> CreateBottleneckAsync(
        string projectCode, CreateProjectBottleneckRequest request, CancellationToken ct = default)
    {
        var code = await RequireProjectAsync(projectCode, ct);
        var entity = new ProjectBottleneck
        {
            ProjectCode = code, Code = NewCode("BN"), Title = request.Name,
            ImpactLevel = request.Severity, Status = "open", Department = request.DepartmentId ?? "",
            Assignee = request.Pic ?? "", ReportedDate = ParseDate(request.StartedAt),
            ExpectedAt = ParseDate(request.ExpectedAt), Reason = request.Reason,
            GanttTask = request.GanttTask, Resolution = request.Resolution, Source = request.Source
        };
        _dbContext.ProjectBottlenecks.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<ProjectBottleneckDto?> UpdateBottleneckAsync(
        string projectCode, string id, UpdateProjectBottleneckRequest request, CancellationToken ct = default)
    {
        var code = await RequireProjectAsync(projectCode, ct);
        if (!Guid.TryParse(id, out var guid)) return null;
        var entity = await _dbContext.ProjectBottlenecks
            .SingleOrDefaultAsync(x => x.ProjectCode == code && x.Id == guid, ct);
        if (entity is null) return null;
        if (request.Name is not null) entity.Title = request.Name;
        if (request.Severity is not null) entity.ImpactLevel = request.Severity;
        if (request.Status is not null) entity.Status = request.Status;
        if (request.StartedAt is not null) entity.ReportedDate = ParseDate(request.StartedAt);
        if (request.ExpectedAt is not null) entity.ExpectedAt = ParseDate(request.ExpectedAt);
        if (request.Reason is not null) entity.Reason = request.Reason;
        if (request.GanttTask is not null) entity.GanttTask = request.GanttTask;
        if (request.DepartmentId is not null) entity.Department = request.DepartmentId;
        if (request.Pic is not null) entity.Assignee = request.Pic;
        if (request.Resolution is not null) entity.Resolution = request.Resolution;
        if (!string.IsNullOrWhiteSpace(request.NoteText))
        {
            var history = ReadJson<ProjectBottleneckHistoryNoteDto>(entity.HistoryJson);
            history.Add(new(Guid.NewGuid().ToString(), DateTimeOffset.UtcNow.ToString("O"), request.NoteText));
            entity.HistoryJson = JsonSerializer.Serialize(history);
        }
        await _dbContext.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<bool> DeleteBottleneckAsync(string projectCode, string id, CancellationToken ct = default)
    {
        var code = await RequireProjectAsync(projectCode, ct);
        if (!Guid.TryParse(id, out var guid)) return false;
        var entity = await _dbContext.ProjectBottlenecks
            .SingleOrDefaultAsync(x => x.ProjectCode == code && x.Id == guid, ct);
        if (entity is null) return false;
        _dbContext.ProjectBottlenecks.Remove(entity);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    private static (int overdueDays, string status, string statusLabel) ComputeSlaStatus(
        DateOnly? slaDueDate,
        string stage,
        string? currentStatus,
        string? currentStatusLabel)
    {
        var status = currentStatus ?? "drafting";
        var statusLabel = currentStatusLabel ?? "";

        if (stage == "approved" || stage == "archived" || status == "approved")
        {
            return (0, status, string.IsNullOrEmpty(statusLabel) ? "Đã chấp thuận chính thức" : statusLabel);
        }

        if (!slaDueDate.HasValue)
        {
            return (0, status, statusLabel);
        }

        var today = VietnamClock.Today;
        if (today > slaDueDate.Value)
        {
            var overdue = today.DayNumber - slaDueDate.Value.DayNumber;
            var label = statusLabel;
            if (string.IsNullOrEmpty(label) || label.StartsWith("Quá hạn SLA", StringComparison.Ordinal) || status == "overdue")
            {
                label = $"Quá hạn SLA {overdue} ngày";
            }
            return (overdue, "overdue", label);
        }

        return (0, status == "overdue" ? "pending_handover" : status, statusLabel);
    }

    private static ProjectDocumentDto MapDocument(ProjectDocument e, int overdueDays, string status, string statusLabel) =>
        new(
            e.Id.ToString(), e.Code, e.ProjectCode, e.Name, e.DocType, e.Stage, e.Author, e.InternalApproved, e.HandoverSigned,
            e.HandoverDate?.ToString("yyyy-MM-dd"), e.HandoverRecipient, e.SlaDueDate?.ToString("yyyy-MM-dd"),
            overdueDays, status, statusLabel, e.Category, e.FileSize, e.FileName, e.DownloadUrl);

    // Documents
    public async Task<IReadOnlyList<ProjectDocumentDto>> GetDocumentsAsync(string projectCode, CancellationToken ct = default)
    {
        var (clean, alt) = await NormalizeProjectCodeAsync(projectCode, ct);
        var entities = await _dbContext.ProjectDocuments
            .AsNoTracking()
            .Where(x => x.ProjectCode == clean || x.ProjectCode == alt)
            .OrderBy(x => x.Code)
            .ToListAsync(ct);

        return entities.Select(e =>
        {
            var (overdueDays, status, statusLabel) = ComputeSlaStatus(e.SlaDueDate, e.Stage, e.Status, e.StatusLabel);
            return MapDocument(e, overdueDays, status, statusLabel);
        }).ToList();
    }

    public async Task<ProjectDocumentDto> CreateDocumentAsync(
        string projectCode, CreateProjectDocumentRequest request, CancellationToken ct = default)
    {
        var code = await RequireProjectAsync(projectCode, ct);
        var slaDueDate = ParseNullableDate(request.SlaDueDate);
        var stage = request.Stage ?? "draft_review";
        var (overdueDays, status, statusLabel) = ComputeSlaStatus(slaDueDate, stage, request.Status ?? "drafting", request.StatusLabel ?? "Draft");

        var entity = new ProjectDocument
        {
            ProjectCode = code,
            Code = string.IsNullOrEmpty(request.Code) ? $"DOC-{VietnamClock.Today:yyyyMMdd}-{RandomNumberGenerator.GetInt32(100, 1000)}" : request.Code,
            Name = request.Name,
            DocType = request.DocType,
            Stage = stage,
            Author = request.Author ?? "",
            InternalApproved = request.InternalApproved ?? false,
            HandoverSigned = request.HandoverSigned ?? false,
            HandoverDate = ParseNullableDate(request.HandoverDate),
            HandoverRecipient = request.HandoverRecipient,
            SlaDueDate = slaDueDate,
            OverdueDays = overdueDays,
            Status = status,
            StatusLabel = statusLabel,
            Category = request.Category ?? "",
            FileSize = request.FileSize ?? "",
            FileName = request.FileName ?? "",
            DownloadUrl = ""
        };
        _dbContext.ProjectDocuments.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
        return MapDocument(entity, overdueDays, status, statusLabel);
    }

    public async Task<ProjectDocumentDto?> UpdateDocumentStatusAsync(
        string projectCode, string id, UpdateProjectDocumentStatusRequest request, CancellationToken ct = default)
    {
        if (!Guid.TryParse(id, out var guidId)) return null;
        var (clean, alt) = await NormalizeProjectCodeAsync(projectCode, ct);
        var entity = await _dbContext.ProjectDocuments
            .FirstOrDefaultAsync(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId, ct);
        if (entity == null) return null;

        entity.Stage = request.Stage;
        if (request.InternalApproved.HasValue) entity.InternalApproved = request.InternalApproved.Value;
        if (request.HandoverSigned.HasValue) entity.HandoverSigned = request.HandoverSigned.Value;
        if (request.HandoverDate != null) entity.HandoverDate = ParseNullableDate(request.HandoverDate);
        if (request.HandoverRecipient != null) entity.HandoverRecipient = request.HandoverRecipient;
        if (request.Status != null) entity.Status = request.Status;
        if (request.StatusLabel != null) entity.StatusLabel = request.StatusLabel;

        var (overdueDays, status, statusLabel) = ComputeSlaStatus(entity.SlaDueDate, entity.Stage, entity.Status, entity.StatusLabel);
        entity.OverdueDays = overdueDays;
        entity.Status = status;
        entity.StatusLabel = statusLabel;

        await _dbContext.SaveChangesAsync(ct);
        return MapDocument(entity, overdueDays, status, statusLabel);
    }

    public async Task<ProjectDocumentDto?> UpdateDocumentAsync(
        string projectCode, string id, CreateProjectDocumentRequest request, CancellationToken ct = default)
    {
        if (!Guid.TryParse(id, out var guidId)) return null;
        var (clean, alt) = await NormalizeProjectCodeAsync(projectCode, ct);
        var entity = await _dbContext.ProjectDocuments
            .FirstOrDefaultAsync(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId, ct);
        if (entity == null) return null;

        entity.Name = request.Name;
        entity.DocType = request.DocType;
        entity.Author = request.Author ?? "";
        entity.HandoverDate = ParseNullableDate(request.HandoverDate);
        entity.HandoverRecipient = request.HandoverRecipient;
        entity.SlaDueDate = ParseNullableDate(request.SlaDueDate);
        if (request.InternalApproved.HasValue) entity.InternalApproved = request.InternalApproved.Value;
        if (request.HandoverSigned.HasValue) entity.HandoverSigned = request.HandoverSigned.Value;
        entity.Category = request.Category ?? "";
        entity.FileSize = request.FileSize ?? "";
        entity.FileName = request.FileName ?? "";
        if (!string.IsNullOrEmpty(request.Stage)) entity.Stage = request.Stage;
        if (!string.IsNullOrEmpty(request.Status)) entity.Status = request.Status;
        if (!string.IsNullOrEmpty(request.StatusLabel)) entity.StatusLabel = request.StatusLabel;

        var (overdueDays, status, statusLabel) = ComputeSlaStatus(entity.SlaDueDate, entity.Stage, entity.Status, entity.StatusLabel);
        entity.OverdueDays = overdueDays;
        entity.Status = status;
        entity.StatusLabel = statusLabel;

        await _dbContext.SaveChangesAsync(ct);
        return MapDocument(entity, overdueDays, status, statusLabel);
    }

    public async Task<ProjectDocumentDto?> UpdateDocumentFileAsync(
        string projectCode, string id, string fileName, string fileSize, string downloadUrl, CancellationToken ct = default)
    {
        if (!Guid.TryParse(id, out var guidId)) return null;
        var (clean, alt) = await NormalizeProjectCodeAsync(projectCode, ct);
        var entity = await _dbContext.ProjectDocuments
            .FirstOrDefaultAsync(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId, ct);
        if (entity == null) return null;

        entity.FileName = fileName;
        entity.FileSize = fileSize;
        entity.DownloadUrl = downloadUrl;

        await _dbContext.SaveChangesAsync(ct);
        return MapDocument(entity, entity.OverdueDays ?? 0, entity.Status, entity.StatusLabel);
    }

    public async Task<bool> DeleteDocumentAsync(string projectCode, string id, CancellationToken ct = default)
    {
        if (!Guid.TryParse(id, out var guidId)) return false;
        var (clean, alt) = await NormalizeProjectCodeAsync(projectCode, ct);
        var entity = await _dbContext.ProjectDocuments
            .FirstOrDefaultAsync(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId, ct);
        if (entity == null) return false;
        _dbContext.ProjectDocuments.Remove(entity);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    private static ProjectRequestCenterDto Map(ProjectRequest e) => new(
        e.Id.ToString(), e.Code, e.ProjectCode, e.Title, e.Type, e.Requester, e.ToDept,
        e.RequestDate.ToString("yyyy-MM-dd"), e.ExpectedDate?.ToString("yyyy-MM-dd"), e.Priority, e.Status, e.Body);

    public async Task<IReadOnlyList<ProjectRequestCenterDto>> GetRequestsAsync(string projectCode, CancellationToken ct = default)
    {
        var code = await RequireProjectAsync(projectCode, ct);
        var items = await _dbContext.ProjectRequests
            .AsNoTracking()
            .Where(x => x.ProjectCode == code)
            .ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<ProjectRequestCenterDto> CreateRequestAsync(
        string projectCode, CreateProjectRequestCenterRequest request, CancellationToken ct = default)
    {
        var code = await RequireProjectAsync(projectCode, ct);
        var entity = new ProjectRequest
        {
            ProjectCode = code, Code = NewCode("REQ"), Title = request.Name,
            Type = request.RequestType, Priority = request.Priority, Status = "open", Requester = request.FromParty,
            RequestDate = VietnamClock.Today, ExpectedDate = ParseNullableDate(request.DueDate),
            ToDept = request.ToDept, Body = request.Body
        };
        _dbContext.ProjectRequests.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<ProjectRequestCenterDto?> UpdateRequestAsync(
        string projectCode, string id, UpdateProjectRequestCenterRequest request, CancellationToken ct = default)
    {
        var code = await RequireProjectAsync(projectCode, ct);
        if (!Guid.TryParse(id, out var guid)) return null;
        var entity = await _dbContext.ProjectRequests
            .SingleOrDefaultAsync(x => x.ProjectCode == code && x.Id == guid, ct);
        if (entity is null) return null;
        if (request.Status is not null) entity.Status = request.Status;
        if (request.Name is not null) entity.Title = request.Name;
        if (request.RequestType is not null) entity.Type = request.RequestType;
        if (request.FromParty is not null) entity.Requester = request.FromParty;
        if (request.ToDept is not null) entity.ToDept = request.ToDept;
        if (request.DueDate is not null) entity.ExpectedDate = ParseNullableDate(request.DueDate);
        if (request.Priority is not null) entity.Priority = request.Priority;
        if (request.Body is not null || request.Notes is not null) entity.Body = request.Body ?? request.Notes;
        await _dbContext.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<bool> DeleteRequestAsync(string projectCode, string id, CancellationToken ct = default)
    {
        var code = await RequireProjectAsync(projectCode, ct);
        if (!Guid.TryParse(id, out var guid)) return false;
        var entity = await _dbContext.ProjectRequests
            .SingleOrDefaultAsync(x => x.ProjectCode == code && x.Id == guid, ct);
        if (entity is null) return false;
        _dbContext.ProjectRequests.Remove(entity);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    private static ProjectMeetingDto Map(ProjectMeeting e) => new(
        e.Id.ToString(), e.Code, e.ProjectCode, e.Title, e.MeetingDate.ToString("yyyy-MM-dd"),
        e.Organizer, e.Agenda, e.MinutesHtml, e.Actions.Count(a => !a.IsDone && a.DueDate < VietnamClock.Today),
        e.Actions.Select(a => new ProjectMeetingActionDto(a.Id.ToString(), a.Name,
            a.DueDate.ToString("yyyy-MM-dd"), a.IsDone, a.Assignee, a.GeneratedBottleneck)).ToList(),
        ReadJson<ProjectMeetingAttachmentDto>(e.AttachmentsJson));

    private async Task<ProjectMeeting?> FindMeetingAsync(string projectCode, string id, CancellationToken ct)
    {
        var code = await RequireProjectAsync(projectCode, ct);
        return Guid.TryParse(id, out var guid)
            ? await _dbContext.ProjectMeetings.Include(x => x.Actions)
                .SingleOrDefaultAsync(x => x.ProjectCode == code && x.Id == guid, ct)
            : null;
    }

    public async Task<IReadOnlyList<ProjectMeetingDto>> GetMeetingsAsync(string projectCode, CancellationToken ct = default)
    {
        var code = await RequireProjectAsync(projectCode, ct);
        var items = await _dbContext.ProjectMeetings
            .AsNoTracking()
            .Include(x => x.Actions)
            .Where(x => x.ProjectCode == code)
            .ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    private async Task ApplyMeetingAsync(ProjectMeeting entity, CreateProjectMeetingRequest request, CancellationToken ct)
    {
        // Validate the complete action set before changing any tracked rows.
        var actions = (request.Actions ?? []).Select(a => new
        {
            Request = a,
            Id = a.Id is null ? Guid.NewGuid() : Guid.TryParse(a.Id, out var id) && id != Guid.Empty
                ? id : throw new DomainRuleException("Mã công việc không hợp lệ."),
            DueDate = ParseDate(a.DueDate)
        }).ToList();
        if (actions.Any(a => string.IsNullOrWhiteSpace(a.Request.Name)) || actions.Select(a => a.Id).Distinct().Count() != actions.Count)
            throw new DomainRuleException("Công việc phải có tên và mã không trùng nhau.");
        var ids = actions.Select(a => a.Id).ToList();
        if (await _dbContext.ProjectMeetingActions.AnyAsync(a => ids.Contains(a.Id) && a.MeetingId != entity.Id, ct))
            throw new DomainRuleException("Công việc không thuộc cuộc họp này.");
        var meetingDate = ParseDate(request.Date);

        entity.Title = request.Name;
        entity.MeetingDate = meetingDate;
        entity.Organizer = request.Attendees ?? "";
        entity.Agenda = request.Agenda ?? "";
        if (request.Minutes is not null) entity.MinutesHtml = request.Minutes;
        if (request.Attachments is not null) entity.AttachmentsJson = JsonSerializer.Serialize(request.Attachments);
        foreach (var removed in entity.Actions.Where(a => !ids.Contains(a.Id)).ToList())
        {
            entity.Actions.Remove(removed);
            _dbContext.ProjectMeetingActions.Remove(removed);
        }
        foreach (var input in actions)
        {
            var action = entity.Actions.SingleOrDefault(a => a.Id == input.Id);
            if (action is null)
            {
                action = new ProjectMeetingAction { Id = input.Id, MeetingId = entity.Id };
                entity.Actions.Add(action);
                _dbContext.ProjectMeetingActions.Add(action);
            }
            action.Name = input.Request.Name;
            action.Assignee = input.Request.Pic ?? "";
            action.DueDate = input.DueDate;
            if (input.Request.Done.HasValue) action.IsDone = input.Request.Done.Value;
            if (input.Request.GeneratedBottleneck.HasValue) action.GeneratedBottleneck = input.Request.GeneratedBottleneck.Value;
        }
    }

    public async Task<ProjectMeetingDto> CreateMeetingAsync(
        string projectCode, CreateProjectMeetingRequest request, CancellationToken ct = default)
    {
        var code = await RequireProjectAsync(projectCode, ct);
        var entity = new ProjectMeeting { ProjectCode = code, Code = NewCode("MTG") };
        await ApplyMeetingAsync(entity, request, ct);
        _dbContext.ProjectMeetings.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<ProjectMeetingDto?> UpdateMeetingAsync(
        string projectCode, string id, CreateProjectMeetingRequest request, CancellationToken ct = default)
    {
        var entity = await FindMeetingAsync(projectCode, id, ct);
        if (entity is null) return null;
        await ApplyMeetingAsync(entity, request, ct);
        await _dbContext.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<ProjectMeetingDto?> UpdateMeetingMinutesAsync(
        string projectCode, string id, UpdateProjectMeetingMinutesRequest request, CancellationToken ct = default)
    {
        var entity = await FindMeetingAsync(projectCode, id, ct);
        if (entity is null) return null;
        entity.MinutesHtml = request.Minutes;
        await _dbContext.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<ProjectMeetingDto?> ToggleMeetingActionAsync(
        string projectCode, string id, string actionId, bool done, CancellationToken ct = default)
    {
        var entity = await FindMeetingAsync(projectCode, id, ct);
        if (entity is null || !Guid.TryParse(actionId, out var guid)) return null;
        var action = entity.Actions.SingleOrDefault(a => a.Id == guid);
        if (action is null) return null;
        action.IsDone = done;
        await _dbContext.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<bool> DeleteMeetingAsync(string projectCode, string id, CancellationToken ct = default)
    {
        var entity = await FindMeetingAsync(projectCode, id, ct);
        if (entity is null) return false;
        _dbContext.ProjectMeetings.Remove(entity);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }
}
