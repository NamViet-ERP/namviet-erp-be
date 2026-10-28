using System.ComponentModel.DataAnnotations;
using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class ProjectDocument : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(50)]
    public string ProjectCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public string Stage { get; set; } = "draft_review";
    public string Author { get; set; } = string.Empty;
    public bool InternalApproved { get; set; } = false;
    public bool HandoverSigned { get; set; } = false;
    public DateOnly? HandoverDate { get; set; }
    public string? HandoverRecipient { get; set; }
    public DateOnly? SlaDueDate { get; set; }
    public int? OverdueDays { get; set; }
    public string Status { get; set; } = "pending";
    public string StatusLabel { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    
    // File upload info
    public string FileSize { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;

    public Project? Project { get; set; }
}

public class ProjectBottleneck : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(50)]
    public string ProjectCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ImpactLevel { get; set; } = "high";
    public string Status { get; set; } = "investigating";
    public string Department { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public DateOnly ReportedDate { get; set; }
    public DateOnly? ExpectedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? GanttTask { get; set; }
    public string? Resolution { get; set; }
    public string Source { get; set; } = "manual";
    public string HistoryJson { get; set; } = "[]";
    public int DelayedDays { get; set; }
    
    public Project? Project { get; set; }
}

public class ProjectRequest : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(50)]
    public string ProjectCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = "material";
    public string Priority { get; set; } = "high";
    public string Status { get; set; } = "pending";
    public string Requester { get; set; } = string.Empty;
    public DateOnly RequestDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public string? ToDept { get; set; }
    public string? Body { get; set; }
    
    public Project? Project { get; set; }
}

public class ProjectMeeting : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(50)]
    public string ProjectCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Organizer { get; set; } = string.Empty;
    public DateOnly MeetingDate { get; set; }
    public string Status { get; set; } = "planned";
    public string Type { get; set; } = "weekly";
    public string MinutesHtml { get; set; } = string.Empty;
    public string Agenda { get; set; } = string.Empty;
    public string AttachmentsJson { get; set; } = "[]";

    public Project? Project { get; set; }
    public ICollection<ProjectMeetingAction> Actions { get; set; } = new List<ProjectMeetingAction>();
}

public class ProjectMeetingAction : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public bool IsDone { get; set; } = false;
    public bool GeneratedBottleneck { get; set; }

    public ProjectMeeting? Meeting { get; set; }
}
