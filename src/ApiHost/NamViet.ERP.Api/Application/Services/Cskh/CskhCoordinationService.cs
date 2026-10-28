using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Time;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NamViet.ERP.Api.Application.Services.Cskh;

public interface ICskhCoordinationService
{
    // Bottlenecks
    IReadOnlyList<ProjectBottleneckDto> GetBottlenecks(string projectCode);
    ProjectBottleneckDto CreateBottleneck(string projectCode, CreateProjectBottleneckRequest request);
    ProjectBottleneckDto? UpdateBottleneck(string projectCode, string id, UpdateProjectBottleneckRequest request);
    bool DeleteBottleneck(string projectCode, string id);

    // Documents
    IReadOnlyList<ProjectDocumentDto> GetDocuments(string projectCode);
    ProjectDocumentDto CreateDocument(string projectCode, CreateProjectDocumentRequest request);
    ProjectDocumentDto? UpdateDocumentStatus(string projectCode, string id, UpdateProjectDocumentStatusRequest request);
    ProjectDocumentDto? UpdateDocument(string projectCode, string id, CreateProjectDocumentRequest request);
    ProjectDocumentDto? UpdateDocumentFile(string projectCode, string id, string fileName, string fileSize, string downloadUrl);
    bool DeleteDocument(string projectCode, string id);

    // Requests
    IReadOnlyList<ProjectRequestCenterDto> GetRequests(string projectCode);
    ProjectRequestCenterDto CreateRequest(string projectCode, CreateProjectRequestCenterRequest request);
    ProjectRequestCenterDto? UpdateRequest(string projectCode, string id, UpdateProjectRequestCenterRequest request);
    bool DeleteRequest(string projectCode, string id);

    // Meetings
    IReadOnlyList<ProjectMeetingDto> GetMeetings(string projectCode);
    ProjectMeetingDto CreateMeeting(string projectCode, CreateProjectMeetingRequest request);
    ProjectMeetingDto? UpdateMeeting(string projectCode, string id, CreateProjectMeetingRequest request);
    ProjectMeetingDto? UpdateMeetingMinutes(string projectCode, string id, UpdateProjectMeetingMinutesRequest request);
    ProjectMeetingDto? ToggleMeetingAction(string projectCode, string id, string actionId, bool done);
    bool DeleteMeeting(string projectCode, string id);
}

public class CskhCoordinationService(WarehouseDbContext dbContext) : ICskhCoordinationService
{
    private readonly WarehouseDbContext _dbContext = dbContext;

    private static DateOnly ParseDate(string? value) => ParseNullableDate(value)
        ?? throw new DomainRuleException("Ng?y kh?ng ???c ?? tr?ng.");

    private static DateOnly? ParseNullableDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date)) return date;
        throw new DomainRuleException("Ng?y ph?i c? ??nh d?ng yyyy-MM-dd.");
    }

    private string RequireProject(string projectCode)
    {
        var code = (projectCode ?? string.Empty).Trim();
        if (!_dbContext.Projects.Any(p => p.ProjectCode == code && p.DeletedAt == null))
            throw new KeyNotFoundException("Kh?ng t?m th?y d? ?n.");
        return code;
    }

    // Keep one canonical key: codes containing spaces and hyphens may identify different projects.
    private (string clean, string alt) NormalizeProjectCode(string projectCode)
    {
        var code = RequireProject(projectCode);
        return (code, code);
    }

    private static string NewCode(string prefix) => $"{prefix}-{VietnamClock.Today:yyyyMMdd}-{Guid.NewGuid():N}";
    private static List<T> ReadJson<T>(string json) => JsonSerializer.Deserialize<List<T>>(json) ?? [];

    private static ProjectBottleneckDto Map(ProjectBottleneck e) => new(
        e.Id.ToString(), e.Code, e.ProjectCode, e.Title, e.ImpactLevel, e.Status,
        e.ReportedDate.ToString("yyyy-MM-dd"), e.ExpectedAt?.ToString("yyyy-MM-dd") ?? "",
        e.Reason, e.GanttTask, e.Department, e.Assignee, e.Resolution, e.Source,
        ReadJson<ProjectBottleneckHistoryNoteDto>(e.HistoryJson));

    public IReadOnlyList<ProjectBottleneckDto> GetBottlenecks(string projectCode)
    {
        var code = RequireProject(projectCode);
        return _dbContext.ProjectBottlenecks.Where(x => x.ProjectCode == code).ToList().Select(Map).ToList();
    }

    public ProjectBottleneckDto CreateBottleneck(string projectCode, CreateProjectBottleneckRequest request)
    {
        var entity = new ProjectBottleneck
        {
            ProjectCode = RequireProject(projectCode), Code = NewCode("BN"), Title = request.Name,
            ImpactLevel = request.Severity, Status = "open", Department = request.DepartmentId ?? "",
            Assignee = request.Pic ?? "", ReportedDate = ParseDate(request.StartedAt),
            ExpectedAt = ParseDate(request.ExpectedAt), Reason = request.Reason,
            GanttTask = request.GanttTask, Resolution = request.Resolution, Source = request.Source
        };
        _dbContext.ProjectBottlenecks.Add(entity);
        _dbContext.SaveChanges();
        return Map(entity);
    }

    public ProjectBottleneckDto? UpdateBottleneck(string projectCode, string id, UpdateProjectBottleneckRequest request)
    {
        var code = RequireProject(projectCode);
        if (!Guid.TryParse(id, out var guid)) return null;
        var entity = _dbContext.ProjectBottlenecks.SingleOrDefault(x => x.ProjectCode == code && x.Id == guid);
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
        _dbContext.SaveChanges();
        return Map(entity);
    }

    public bool DeleteBottleneck(string projectCode, string id)
    {
        var code = RequireProject(projectCode);
        if (!Guid.TryParse(id, out var guid)) return false;
        var entity = _dbContext.ProjectBottlenecks.SingleOrDefault(x => x.ProjectCode == code && x.Id == guid);
        if (entity is null) return false;
        _dbContext.ProjectBottlenecks.Remove(entity);
        _dbContext.SaveChanges();
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

        // 1. Hồ sơ đã hoàn tất / đã chấp thuận / lưu trữ
        if (stage == "approved" || stage == "archived" || status == "approved")
        {
            return (0, status, string.IsNullOrEmpty(statusLabel) ? "Đã chấp thuận chính thức" : statusLabel);
        }

        // 2. Không có SLA due date
        if (!slaDueDate.HasValue)
        {
            return (0, status, statusLabel);
        }

        var today = VietnamClock.Today;
        if (today > slaDueDate.Value)
        {
            var overdue = today.DayNumber - slaDueDate.Value.DayNumber;
            var label = statusLabel;
            if (string.IsNullOrEmpty(label) || label.StartsWith("Quá hạn SLA") || status == "overdue")
            {
                label = $"Quá hạn SLA {overdue} ngày";
            }
            return (overdue, "overdue", label);
        }

        return (0, status == "overdue" ? "pending_handover" : status, statusLabel);
    }

    // Documents
    public IReadOnlyList<ProjectDocumentDto> GetDocuments(string projectCode)
    {
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entities = _dbContext.ProjectDocuments
            .Where(x => x.ProjectCode == clean || x.ProjectCode == alt)
            .OrderBy(x => x.Code)
            .ToList();
            
        return entities.Select(e => {
            var (overdueDays, status, statusLabel) = ComputeSlaStatus(e.SlaDueDate, e.Stage, e.Status, e.StatusLabel);
            return new ProjectDocumentDto(
                e.Id.ToString(), e.Code, e.ProjectCode, e.Name, e.DocType, e.Stage, e.Author, e.InternalApproved, e.HandoverSigned, 
                e.HandoverDate?.ToString("yyyy-MM-dd"), e.HandoverRecipient, e.SlaDueDate?.ToString("yyyy-MM-dd"), 
                overdueDays, status, statusLabel, e.Category, e.FileSize, e.FileName, e.DownloadUrl
            );
        }).ToList();
    }

    public ProjectDocumentDto CreateDocument(string projectCode, CreateProjectDocumentRequest request)
    {
        var slaDueDate = ParseNullableDate(request.SlaDueDate);
        var stage = request.Stage ?? "draft_review";
        var (overdueDays, status, statusLabel) = ComputeSlaStatus(slaDueDate, stage, request.Status ?? "drafting", request.StatusLabel ?? "Draft");

        var entity = new ProjectDocument
        {
            ProjectCode = RequireProject(projectCode),
            Code = string.IsNullOrEmpty(request.Code) ? $"DOC-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}" : request.Code,
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
        _dbContext.SaveChanges();
        return new ProjectDocumentDto(
            entity.Id.ToString(), entity.Code, entity.ProjectCode, entity.Name, entity.DocType, entity.Stage, entity.Author, entity.InternalApproved, entity.HandoverSigned, 
            entity.HandoverDate?.ToString("yyyy-MM-dd"), entity.HandoverRecipient, entity.SlaDueDate?.ToString("yyyy-MM-dd"), 
            overdueDays, status, statusLabel, entity.Category, entity.FileSize, entity.FileName, entity.DownloadUrl
        );
    }

    public ProjectDocumentDto? UpdateDocumentStatus(string projectCode, string id, UpdateProjectDocumentStatusRequest request)
    {
        if (!Guid.TryParse(id, out var guidId)) return null;
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entity = _dbContext.ProjectDocuments.FirstOrDefault(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId);
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
        
        _dbContext.SaveChanges();
        return new ProjectDocumentDto(
            entity.Id.ToString(), entity.Code, entity.ProjectCode, entity.Name, entity.DocType, entity.Stage, entity.Author, entity.InternalApproved, entity.HandoverSigned, 
            entity.HandoverDate?.ToString("yyyy-MM-dd"), entity.HandoverRecipient, entity.SlaDueDate?.ToString("yyyy-MM-dd"), 
            overdueDays, status, statusLabel, entity.Category, entity.FileSize, entity.FileName, entity.DownloadUrl
        );
    }

    public ProjectDocumentDto? UpdateDocument(string projectCode, string id, CreateProjectDocumentRequest request)
    {
        if (!Guid.TryParse(id, out var guidId)) return null;
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entity = _dbContext.ProjectDocuments.FirstOrDefault(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId);
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
        
        _dbContext.SaveChanges();
        return new ProjectDocumentDto(
            entity.Id.ToString(), entity.Code, entity.ProjectCode, entity.Name, entity.DocType, entity.Stage, entity.Author, entity.InternalApproved, entity.HandoverSigned, 
            entity.HandoverDate?.ToString("yyyy-MM-dd"), entity.HandoverRecipient, entity.SlaDueDate?.ToString("yyyy-MM-dd"), 
            overdueDays, status, statusLabel, entity.Category, entity.FileSize, entity.FileName, entity.DownloadUrl
        );
    }

    public ProjectDocumentDto? UpdateDocumentFile(string projectCode, string id, string fileName, string fileSize, string downloadUrl)
    {
        if (!Guid.TryParse(id, out var guidId)) return null;
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entity = _dbContext.ProjectDocuments.FirstOrDefault(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId);
        if (entity == null) return null;
        
        entity.FileName = fileName;
        entity.FileSize = fileSize;
        entity.DownloadUrl = downloadUrl;
        
        _dbContext.SaveChanges();
        return new ProjectDocumentDto(
            entity.Id.ToString(), entity.Code, entity.ProjectCode, entity.Name, entity.DocType, entity.Stage, entity.Author, entity.InternalApproved, entity.HandoverSigned, 
            entity.HandoverDate?.ToString("yyyy-MM-dd"), entity.HandoverRecipient, entity.SlaDueDate?.ToString("yyyy-MM-dd"), 
            entity.OverdueDays ?? 0, entity.Status, entity.StatusLabel, entity.Category, entity.FileSize, entity.FileName, entity.DownloadUrl
        );
    }


    public bool DeleteDocument(string projectCode, string id)
    {
        if (!Guid.TryParse(id, out var guidId)) return false;
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entity = _dbContext.ProjectDocuments.FirstOrDefault(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId);
        if (entity == null) return false;
        _dbContext.ProjectDocuments.Remove(entity);
        _dbContext.SaveChanges();
        return true;
    }

    private static ProjectRequestCenterDto Map(ProjectRequest e) => new(
        e.Id.ToString(), e.Code, e.ProjectCode, e.Title, e.Type, e.Requester, e.ToDept,
        e.RequestDate.ToString("yyyy-MM-dd"), e.ExpectedDate?.ToString("yyyy-MM-dd"), e.Priority, e.Status, e.Body);

    public IReadOnlyList<ProjectRequestCenterDto> GetRequests(string projectCode)
    {
        var code = RequireProject(projectCode);
        return _dbContext.ProjectRequests.Where(x => x.ProjectCode == code).ToList().Select(Map).ToList();
    }

    public ProjectRequestCenterDto CreateRequest(string projectCode, CreateProjectRequestCenterRequest request)
    {
        var entity = new ProjectRequest
        {
            ProjectCode = RequireProject(projectCode), Code = NewCode("REQ"), Title = request.Name,
            Type = request.RequestType, Priority = request.Priority, Status = "open", Requester = request.FromParty,
            RequestDate = VietnamClock.Today, ExpectedDate = ParseNullableDate(request.DueDate),
            ToDept = request.ToDept, Body = request.Body
        };
        _dbContext.ProjectRequests.Add(entity);
        _dbContext.SaveChanges();
        return Map(entity);
    }

    public ProjectRequestCenterDto? UpdateRequest(string projectCode, string id, UpdateProjectRequestCenterRequest request)
    {
        var code = RequireProject(projectCode);
        if (!Guid.TryParse(id, out var guid)) return null;
        var entity = _dbContext.ProjectRequests.SingleOrDefault(x => x.ProjectCode == code && x.Id == guid);
        if (entity is null) return null;
        if (request.Status is not null) entity.Status = request.Status;
        if (request.Name is not null) entity.Title = request.Name;
        if (request.RequestType is not null) entity.Type = request.RequestType;
        if (request.FromParty is not null) entity.Requester = request.FromParty;
        if (request.ToDept is not null) entity.ToDept = request.ToDept;
        if (request.DueDate is not null) entity.ExpectedDate = ParseNullableDate(request.DueDate);
        if (request.Priority is not null) entity.Priority = request.Priority;
        if (request.Body is not null || request.Notes is not null) entity.Body = request.Body ?? request.Notes;
        _dbContext.SaveChanges();
        return Map(entity);
    }

    public bool DeleteRequest(string projectCode, string id)
    {
        var code = RequireProject(projectCode);
        if (!Guid.TryParse(id, out var guid)) return false;
        var entity = _dbContext.ProjectRequests.SingleOrDefault(x => x.ProjectCode == code && x.Id == guid);
        if (entity is null) return false;
        _dbContext.ProjectRequests.Remove(entity);
        _dbContext.SaveChanges();
        return true;
    }

    private static ProjectMeetingDto Map(ProjectMeeting e) => new(
        e.Id.ToString(), e.Code, e.ProjectCode, e.Title, e.MeetingDate.ToString("yyyy-MM-dd"),
        e.Organizer, e.Agenda, e.MinutesHtml, e.Actions.Count(a => !a.IsDone && a.DueDate < VietnamClock.Today),
        e.Actions.Select(a => new ProjectMeetingActionDto(a.Id.ToString(), a.Name,
            a.DueDate.ToString("yyyy-MM-dd"), a.IsDone, a.Assignee, a.GeneratedBottleneck)).ToList(),
        ReadJson<ProjectMeetingAttachmentDto>(e.AttachmentsJson));

    private ProjectMeeting? FindMeeting(string projectCode, string id)
    {
        var code = RequireProject(projectCode);
        return Guid.TryParse(id, out var guid)
            ? _dbContext.ProjectMeetings.Include(x => x.Actions).SingleOrDefault(x => x.ProjectCode == code && x.Id == guid)
            : null;
    }

    public IReadOnlyList<ProjectMeetingDto> GetMeetings(string projectCode)
    {
        var code = RequireProject(projectCode);
        return _dbContext.ProjectMeetings.Include(x => x.Actions).Where(x => x.ProjectCode == code)
            .ToList().Select(Map).ToList();
    }

    private void ApplyMeeting(ProjectMeeting entity, CreateProjectMeetingRequest request)
    {
        // Validate the complete action set before changing any tracked rows.
        var actions = (request.Actions ?? []).Select(a => new
        {
            Request = a,
            Id = a.Id is null ? Guid.NewGuid() : Guid.TryParse(a.Id, out var id) && id != Guid.Empty
                ? id : throw new DomainRuleException("M? c?ng vi?c kh?ng h?p l?."),
            DueDate = ParseDate(a.DueDate)
        }).ToList();
        if (actions.Any(a => string.IsNullOrWhiteSpace(a.Request.Name)) || actions.Select(a => a.Id).Distinct().Count() != actions.Count)
            throw new DomainRuleException("C?ng vi?c ph?i c? t?n v? m? kh?ng tr?ng nhau.");
        var ids = actions.Select(a => a.Id).ToList();
        if (_dbContext.ProjectMeetingActions.Any(a => ids.Contains(a.Id) && a.MeetingId != entity.Id))
            throw new DomainRuleException("C?ng vi?c kh?ng thu?c cu?c h?p n?y.");
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

    public ProjectMeetingDto CreateMeeting(string projectCode, CreateProjectMeetingRequest request)
    {
        var entity = new ProjectMeeting { ProjectCode = RequireProject(projectCode), Code = NewCode("MTG") };
        ApplyMeeting(entity, request);
        _dbContext.ProjectMeetings.Add(entity);
        _dbContext.SaveChanges();
        return Map(entity);
    }

    public ProjectMeetingDto? UpdateMeeting(string projectCode, string id, CreateProjectMeetingRequest request)
    {
        var entity = FindMeeting(projectCode, id);
        if (entity is null) return null;
        ApplyMeeting(entity, request);
        _dbContext.SaveChanges();
        return Map(entity);
    }

    public ProjectMeetingDto? UpdateMeetingMinutes(string projectCode, string id, UpdateProjectMeetingMinutesRequest request)
    {
        var entity = FindMeeting(projectCode, id);
        if (entity is null) return null;
        entity.MinutesHtml = request.Minutes;
        _dbContext.SaveChanges();
        return Map(entity);
    }

    public ProjectMeetingDto? ToggleMeetingAction(string projectCode, string id, string actionId, bool done)
    {
        var entity = FindMeeting(projectCode, id);
        if (entity is null || !Guid.TryParse(actionId, out var guid)) return null;
        var action = entity.Actions.SingleOrDefault(a => a.Id == guid);
        if (action is null) return null;
        action.IsDone = done;
        _dbContext.SaveChanges();
        return Map(entity);
    }

    public bool DeleteMeeting(string projectCode, string id)
    {
        var entity = FindMeeting(projectCode, id);
        if (entity is null) return false;
        _dbContext.ProjectMeetings.Remove(entity);
        _dbContext.SaveChanges();
        return true;
    }
}
