namespace NamViet.ERP.Api.Application.Dtos.Shared;

/// <summary>Pushed to clients when an activity_events row is appended.</summary>
public record ActivityEventCreatedDto(
    long Id,
    DateTime OccurredAt,
    Guid? ActorUserId,
    string ActorName,
    string Module,
    string Action,
    string? EntityType,
    string? EntityCode,
    string Message,
    string CategoryLabel);
