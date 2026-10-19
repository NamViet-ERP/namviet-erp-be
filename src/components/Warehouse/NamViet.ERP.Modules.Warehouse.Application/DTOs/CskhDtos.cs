using System;
using System.Collections.Generic;

namespace NamViet.ERP.Modules.Warehouse.Application.DTOs;

// ── CSKH / Project Coordination DTOs ─────────────────────────────────────────

public record ProjectBottleneckDto(
    string Id,
    string Code,
    string ProjectCode,
    string Name,
    string Severity,       // urgent | medium | low
    string Status,         // open | closed
    string StartedAt,
    string ExpectedAt,
    string Reason,
    string? GanttTask,
    string? DepartmentId,
    string? Pic,
    string? Resolution,
    string Source,          // manual | gantt | meeting
    IReadOnlyList<ProjectBottleneckHistoryNoteDto> History);

public record ProjectBottleneckHistoryNoteDto(
    string Id,
    string At,
    string Text);

public record CreateProjectBottleneckRequest(
    string Name,
    string Severity,
    string StartedAt,
    string ExpectedAt,
    string Reason,
    string? GanttTask = null,
    string? DepartmentId = null,
    string? Pic = null,
    string? Resolution = null,
    string Source = "manual");

public record UpdateProjectBottleneckRequest(
    string? Name,
    string? Severity,
    string? Status,
    string? ExpectedAt,
    string? Reason,
    string? GanttTask,
    string? DepartmentId,
    string? Pic,
    string? Resolution,
    string? NoteText);

// ── Documents Pipeline (Unified 3-phase & Archive) ───────────────────────────

public record ProjectDocumentDto(
    string Id,
    string Code,
    string ProjectCode,
    string Name,
    string DocType,
    string Stage,          // draft_review | handover_signed | feedback_pending | approved | archived
    string? Author,
    bool InternalApproved,
    bool HandoverSigned,
    string? HandoverDate,
    string? HandoverRecipient,
    string? SlaDueDate,
    int OverdueDays,
    string Status,         // drafting | pending_handover | overdue | awaiting_client | approved
    string StatusLabel,
    string? Category,
    string? FileSize,
    string? FileName,
    string? DownloadUrl);

public record CreateProjectDocumentRequest(
    string Code,
    string Name,
    string DocType,
    string? Author,
    string? SlaDueDate,
    string? Category = null,
    string? HandoverDate = null,
    string? FileName = null,
    string? FileSize = null,
    string? HandoverRecipient = null,
    string? Stage = null,
    string? Status = null,
    string? StatusLabel = null);

public record UpdateProjectDocumentStatusRequest(
    string Stage,
    bool? InternalApproved,
    bool? HandoverSigned,
    string? HandoverDate,
    string? HandoverRecipient,
    string? Status,
    string? StatusLabel);

// ── Internal Requests Center ────────────────────────────────────────────────

public record ProjectRequestCenterDto(
    string Id,
    string Code,
    string ProjectCode,
    string Name,
    string RequestType,
    string FromParty,
    string? ToDept,
    string CreatedAt,
    string? DueDate,
    string Priority,       // urgent | medium | low
    string Status,         // open | processing | closed
    string? Body);

public record CreateProjectRequestCenterRequest(
    string Name,
    string RequestType,
    string FromParty,
    string? ToDept,
    string? DueDate,
    string Priority,
    string? Body);

public record UpdateProjectRequestCenterRequest(
    string Status,
    string? Notes);

// ── Project Meetings & Action Items ──────────────────────────────────────────

public record ProjectMeetingAttachmentDto(
    string Name,
    string? Size,
    string? Url,
    string? UploadedAt);

public record ProjectMeetingDto(
    string Id,
    string Code,
    string ProjectCode,
    string Name,
    string Date,
    string? Attendees,
    string? Agenda,
    string? Minutes,
    int OverdueActionCount,
    IReadOnlyList<ProjectMeetingActionDto> Actions,
    IReadOnlyList<ProjectMeetingAttachmentDto>? Attachments = null);

public record ProjectMeetingActionDto(
    string Id,
    string Name,
    string DueDate,
    bool Done,
    string? Pic,
    bool GeneratedBottleneck);

public record CreateProjectMeetingRequest(
    string Name,
    string Date,
    string? Attendees,
    string? Agenda,
    IReadOnlyList<CreateProjectMeetingActionRequest> Actions);

public record CreateProjectMeetingActionRequest(
    string Name,
    string DueDate,
    string? Pic);

public record UpdateProjectMeetingMinutesRequest(
    string Minutes);

public record UpdateProjectMeetingActionRequest(
    string ActionId,
    bool Done);
