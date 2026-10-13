using System;
using System.Collections.Generic;
using System.Linq;
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

public class CskhCoordinationService : ICskhCoordinationService
{
    private readonly WarehouseDbContext _dbContext;

    public CskhCoordinationService(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DateOnly ParseDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return DateOnly.FromDateTime(DateTime.Today);
        if (DateOnly.TryParse(dateStr, out var d)) return d;
        if (DateTime.TryParse(dateStr, out var dt)) return DateOnly.FromDateTime(dt);
        return DateOnly.FromDateTime(DateTime.Today);
    }

    private static DateOnly? ParseNullableDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        if (DateOnly.TryParse(dateStr, out var d)) return d;
        if (DateTime.TryParse(dateStr, out var dt)) return DateOnly.FromDateTime(dt);
        return null;
    }

    private static (string clean, string alt) NormalizeProjectCode(string projectCode)
    {
        var clean = (projectCode ?? string.Empty).Trim();
        var alt = clean.Contains(' ') ? clean.Replace(' ', '-') : clean.Replace('-', ' ');
        return (clean, alt);
    }

    // Bottlenecks
    public IReadOnlyList<ProjectBottleneckDto> GetBottlenecks(string projectCode)
    {
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entities = _dbContext.ProjectBottlenecks
            .Where(x => x.ProjectCode == clean || x.ProjectCode == alt)
            .ToList();
        return entities.Select(e => new ProjectBottleneckDto(
            e.Id.ToString(), e.Code, e.ProjectCode, e.Title, e.ImpactLevel, e.Status, 
            e.ReportedDate.ToString("yyyy-MM-dd"), 
            e.ReportedDate.AddDays(10).ToString("yyyy-MM-dd"), // mock ExpectedAt
            "N/A", null, e.Department, e.Assignee, null, "manual", 
            new List<ProjectBottleneckHistoryNoteDto>()
        )).ToList();
    }

    public ProjectBottleneckDto CreateBottleneck(string projectCode, CreateProjectBottleneckRequest request)
    {
        var entity = new ProjectBottleneck
        {
            ProjectCode = projectCode,
            Code = $"BN-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}",
            Title = request.Name,
            ImpactLevel = request.Severity,
            Status = "open",
            Department = request.DepartmentId ?? "",
            Assignee = request.Pic ?? "",
            ReportedDate = ParseDate(request.StartedAt),
            DelayedDays = 0
        };
        _dbContext.ProjectBottlenecks.Add(entity);
        _dbContext.SaveChanges();
        return new ProjectBottleneckDto(
            entity.Id.ToString(), entity.Code, entity.ProjectCode, entity.Title, entity.ImpactLevel, entity.Status, 
            entity.ReportedDate.ToString("yyyy-MM-dd"), 
            entity.ReportedDate.AddDays(10).ToString("yyyy-MM-dd"), // mock ExpectedAt
            request.Reason, request.GanttTask, entity.Department, entity.Assignee, request.Resolution, "manual", 
            new List<ProjectBottleneckHistoryNoteDto>()
        );
    }

    public ProjectBottleneckDto? UpdateBottleneck(string projectCode, string id, UpdateProjectBottleneckRequest request)
    {
        if (!Guid.TryParse(id, out var guidId)) return null;
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entity = _dbContext.ProjectBottlenecks.FirstOrDefault(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId);
        if (entity == null) return null;
        
        if (request.Name != null) entity.Title = request.Name;
        if (request.Severity != null) entity.ImpactLevel = request.Severity;
        if (request.Status != null) entity.Status = request.Status;
        if (request.DepartmentId != null) entity.Department = request.DepartmentId;
        if (request.Pic != null) entity.Assignee = request.Pic;
        
        _dbContext.SaveChanges();
        return new ProjectBottleneckDto(
            entity.Id.ToString(), entity.Code, entity.ProjectCode, entity.Title, entity.ImpactLevel, entity.Status, 
            entity.ReportedDate.ToString("yyyy-MM-dd"), 
            entity.ReportedDate.AddDays(10).ToString("yyyy-MM-dd"), // mock ExpectedAt
            request.Reason ?? "", request.GanttTask, entity.Department, entity.Assignee, request.Resolution, "manual", 
            new List<ProjectBottleneckHistoryNoteDto>()
        );
    }

    public bool DeleteBottleneck(string projectCode, string id)
    {
        if (!Guid.TryParse(id, out var guidId)) return false;
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entity = _dbContext.ProjectBottlenecks.FirstOrDefault(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId);
        if (entity == null) return false;
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

        var today = DateOnly.FromDateTime(DateTime.Today);
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
            ProjectCode = projectCode,
            Code = string.IsNullOrEmpty(request.Code) ? $"DOC-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}" : request.Code,
            Name = request.Name,
            DocType = request.DocType,
            Stage = stage,
            Author = request.Author ?? "",
            InternalApproved = false,
            HandoverSigned = false,
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

    // Requests
    public IReadOnlyList<ProjectRequestCenterDto> GetRequests(string projectCode)
    {
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entities = _dbContext.ProjectRequests
            .Where(x => x.ProjectCode == clean || x.ProjectCode == alt)
            .ToList();
        return entities.Select(e => new ProjectRequestCenterDto(
            e.Id.ToString(), e.Code, e.ProjectCode, e.Title, e.Type, e.Requester, null, 
            e.RequestDate.ToString("yyyy-MM-dd"), e.ExpectedDate.ToString("yyyy-MM-dd"), 
            e.Priority, e.Status, null
        )).ToList();
    }

    public ProjectRequestCenterDto CreateRequest(string projectCode, CreateProjectRequestCenterRequest request)
    {
        var entity = new ProjectRequest
        {
            ProjectCode = projectCode,
            Code = $"REQ-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}",
            Title = request.Name,
            Type = request.RequestType,
            Priority = request.Priority,
            Status = "open",
            Requester = request.FromParty,
            RequestDate = DateOnly.FromDateTime(DateTime.Today),
            ExpectedDate = ParseDate(request.DueDate)
        };
        _dbContext.ProjectRequests.Add(entity);
        _dbContext.SaveChanges();
        return new ProjectRequestCenterDto(
            entity.Id.ToString(), entity.Code, entity.ProjectCode, entity.Title, entity.Type, entity.Requester, null, 
            entity.RequestDate.ToString("yyyy-MM-dd"), entity.ExpectedDate.ToString("yyyy-MM-dd"), 
            entity.Priority, entity.Status, request.Body
        );
    }

    public ProjectRequestCenterDto? UpdateRequest(string projectCode, string id, UpdateProjectRequestCenterRequest request)
    {
        if (!Guid.TryParse(id, out var guidId)) return null;
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entity = _dbContext.ProjectRequests.FirstOrDefault(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId);
        if (entity == null) return null;
        
        entity.Status = request.Status;
        
        _dbContext.SaveChanges();
        return new ProjectRequestCenterDto(
            entity.Id.ToString(), entity.Code, entity.ProjectCode, entity.Title, entity.Type, entity.Requester, null, 
            entity.RequestDate.ToString("yyyy-MM-dd"), entity.ExpectedDate.ToString("yyyy-MM-dd"), 
            entity.Priority, entity.Status, request.Notes
        );
    }

    public bool DeleteRequest(string projectCode, string id)
    {
        if (!Guid.TryParse(id, out var guidId)) return false;
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entity = _dbContext.ProjectRequests.FirstOrDefault(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId);
        if (entity == null) return false;
        _dbContext.ProjectRequests.Remove(entity);
        _dbContext.SaveChanges();
        return true;
    }

    // Meetings
    public IReadOnlyList<ProjectMeetingDto> GetMeetings(string projectCode)
    {
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entities = _dbContext.ProjectMeetings
            .Include(x => x.Actions)
            .Where(x => x.ProjectCode == clean || x.ProjectCode == alt)
            .ToList();
        return entities.Select(e => new ProjectMeetingDto(
            e.Id.ToString(), e.Code, e.ProjectCode, e.Title, e.MeetingDate.ToString("yyyy-MM-dd"), 
            e.Organizer, null, e.MinutesHtml, 0,
            e.Actions.Select(a => new ProjectMeetingActionDto(
                a.Id.ToString(), a.Name, a.DueDate.ToString("yyyy-MM-dd"), a.IsDone, a.Assignee, false
            )).ToList(),
            new List<ProjectMeetingAttachmentDto>()
        )).ToList();
    }

    public ProjectMeetingDto CreateMeeting(string projectCode, CreateProjectMeetingRequest request)
    {
        var entity = new ProjectMeeting
        {
            ProjectCode = projectCode,
            Code = $"MTG-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}",
            Title = request.Name,
            Location = "",
            Organizer = request.Attendees ?? "",
            MeetingDate = ParseDate(request.Date),
            Status = "planned",
            Type = "weekly",
            MinutesHtml = request.Agenda ?? ""
        };
        
        foreach (var action in request.Actions)
        {
            entity.Actions.Add(new ProjectMeetingAction
            {
                Name = action.Name,
                Assignee = action.Pic ?? "",
                DueDate = ParseDate(action.DueDate),
                IsDone = false
            });
        }
        
        _dbContext.ProjectMeetings.Add(entity);
        _dbContext.SaveChanges();
        
        return new ProjectMeetingDto(
            entity.Id.ToString(), entity.Code, entity.ProjectCode, entity.Title, entity.MeetingDate.ToString("yyyy-MM-dd"), 
            entity.Organizer, null, entity.MinutesHtml, 0,
            entity.Actions.Select(a => new ProjectMeetingActionDto(
                a.Id.ToString(), a.Name, a.DueDate.ToString("yyyy-MM-dd"), a.IsDone, a.Assignee, false
            )).ToList(),
            new List<ProjectMeetingAttachmentDto>()
        );
    }

    public ProjectMeetingDto? UpdateMeeting(string projectCode, string id, CreateProjectMeetingRequest request)
    {
        if (!Guid.TryParse(id, out var guidId)) return null;
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entity = _dbContext.ProjectMeetings.Include(x => x.Actions).FirstOrDefault(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId);
        if (entity == null) return null;

        entity.Title = request.Name;
        entity.MeetingDate = ParseDate(request.Date);
        entity.Organizer = request.Attendees ?? "";
        entity.MinutesHtml = request.Agenda ?? "";

        _dbContext.SaveChanges();

        return new ProjectMeetingDto(
            entity.Id.ToString(), entity.Code, entity.ProjectCode, entity.Title, entity.MeetingDate.ToString("yyyy-MM-dd"), 
            entity.Organizer, null, entity.MinutesHtml, 0,
            entity.Actions.Select(a => new ProjectMeetingActionDto(
                a.Id.ToString(), a.Name, a.DueDate.ToString("yyyy-MM-dd"), a.IsDone, a.Assignee, false
            )).ToList(),
            new List<ProjectMeetingAttachmentDto>()
        );
    }

    public ProjectMeetingDto? UpdateMeetingMinutes(string projectCode, string id, UpdateProjectMeetingMinutesRequest request)
    {
        if (!Guid.TryParse(id, out var guidId)) return null;
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entity = _dbContext.ProjectMeetings.Include(x => x.Actions).FirstOrDefault(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId);
        if (entity == null) return null;
        
        entity.MinutesHtml = request.Minutes;
        
        _dbContext.SaveChanges();
        
        return new ProjectMeetingDto(
            entity.Id.ToString(), entity.Code, entity.ProjectCode, entity.Title, entity.MeetingDate.ToString("yyyy-MM-dd"), 
            entity.Organizer, null, entity.MinutesHtml, 0,
            entity.Actions.Select(a => new ProjectMeetingActionDto(
                a.Id.ToString(), a.Name, a.DueDate.ToString("yyyy-MM-dd"), a.IsDone, a.Assignee, false
            )).ToList(),
            new List<ProjectMeetingAttachmentDto>()
        );
    }

    public ProjectMeetingDto? ToggleMeetingAction(string projectCode, string id, string actionId, bool done)
    {
        if (!Guid.TryParse(id, out var guidId)) return null;
        if (!Guid.TryParse(actionId, out var actionGuid)) return null;
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entity = _dbContext.ProjectMeetings.Include(x => x.Actions).FirstOrDefault(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId);
        if (entity == null) return null;
        
        var action = entity.Actions.FirstOrDefault(a => a.Id == actionGuid);
        if (action == null) return null;

        action.IsDone = done;
        _dbContext.SaveChanges();
        
        return new ProjectMeetingDto(
            entity.Id.ToString(), entity.Code, entity.ProjectCode, entity.Title, entity.MeetingDate.ToString("yyyy-MM-dd"), 
            entity.Organizer, null, entity.MinutesHtml, 0,
            entity.Actions.Select(a => new ProjectMeetingActionDto(
                a.Id.ToString(), a.Name, a.DueDate.ToString("yyyy-MM-dd"), a.IsDone, a.Assignee, false
            )).ToList(),
            new List<ProjectMeetingAttachmentDto>()
        );
    }

    public bool DeleteMeeting(string projectCode, string id)
    {
        if (!Guid.TryParse(id, out var guidId)) return false;
        var (clean, alt) = NormalizeProjectCode(projectCode);
        var entity = _dbContext.ProjectMeetings.FirstOrDefault(x => (x.ProjectCode == clean || x.ProjectCode == alt) && x.Id == guidId);
        if (entity == null) return false;
        _dbContext.ProjectMeetings.Remove(entity);
        _dbContext.SaveChanges();
        return true;
    }
}
