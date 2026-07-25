using Microsoft.AspNetCore.SignalR;
using NamViet.ERP.Api.Application.Dtos.Shared;
using NamViet.ERP.Api.Hubs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Shared;

public record ActivityEventWriteRequest(
    string Module,
    string Action,
    string Message,
    string CategoryLabel,
    string? EntityType = null,
    string? EntityCode = null,
    Guid? ActorUserId = null,
    string? ActorName = null,
    DateTime? OccurredAt = null);

public interface IActivityEventWriter
{
    Task WriteAsync(ActivityEventWriteRequest request, CancellationToken ct = default);
}

/// <summary>
/// Shared append-only writer — persists then pushes via SignalR to the module group.
/// </summary>
public sealed class ActivityEventWriter(
    WarehouseDbContext db,
    ICurrentUser currentUser,
    IHubContext<ActivityHub> hub) : IActivityEventWriter
{
    public async Task WriteAsync(ActivityEventWriteRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Module))
            throw new ArgumentException("Module is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Action))
            throw new ArgumentException("Action is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Message is required.", nameof(request));

        var actorId = request.ActorUserId
            ?? (currentUser.IsAuthenticated ? currentUser.Id : null);
        var actorName = !string.IsNullOrWhiteSpace(request.ActorName)
            ? request.ActorName.Trim()
            : currentUser.IsAuthenticated
                ? (currentUser.Name ?? "Hệ thống")
                : "Hệ thống";

        var entity = new ActivityEvent
        {
            OccurredAt = request.OccurredAt ?? DateTime.UtcNow,
            ActorUserId = actorId,
            ActorName = actorName,
            Module = request.Module.Trim(),
            Action = request.Action.Trim(),
            EntityType = string.IsNullOrWhiteSpace(request.EntityType) ? null : request.EntityType.Trim(),
            EntityCode = string.IsNullOrWhiteSpace(request.EntityCode) ? null : request.EntityCode.Trim(),
            Message = request.Message.Trim(),
            CategoryLabel = string.IsNullOrWhiteSpace(request.CategoryLabel)
                ? request.Module.Trim()
                : request.CategoryLabel.Trim(),
        };

        db.ActivityEvents.Add(entity);
        await db.SaveChangesAsync(ct);

        var dto = new ActivityEventCreatedDto(
            entity.Id,
            entity.OccurredAt,
            entity.ActorUserId,
            entity.ActorName,
            entity.Module,
            entity.Action,
            entity.EntityType,
            entity.EntityCode,
            entity.Message,
            entity.CategoryLabel);

        await hub.Clients
            .Group(ActivityHub.GroupName(entity.Module))
            .SendAsync(ActivityHub.EventCreated, dto, ct);
    }
}
