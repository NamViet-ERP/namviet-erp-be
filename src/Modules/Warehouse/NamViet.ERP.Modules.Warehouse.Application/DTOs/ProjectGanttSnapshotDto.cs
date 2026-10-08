using System;
using System.Collections.Generic;

namespace NamViet.ERP.Modules.Warehouse.Application.DTOs;

public record ProjectGanttSnapshotDto(
    Guid Id,
    string ProjectCode,
    DateTimeOffset CreatedAt,
    double PlanPct,
    double ActualPct,
    int ItemCount,
    List<ProjectGanttSnapshotItemDto> Items
);

public record ProjectGanttSnapshotItemDto(
    Guid Id,
    string TaskId,
    string TaskName,
    double PlanPct,
    double ActualPct
);

public record CreateProjectGanttSnapshotRequest(
    double PlanPct,
    double ActualPct,
    List<ProjectGanttSnapshotItemDto> Items
);
