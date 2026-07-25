namespace NamViet.ERP.Api.Application.Services.Purchasing;

/// <summary>
/// Business conflict for purchasing flows. Message is safe to expose to clients.
/// </summary>
public sealed class PurchasingConflictException(string message) : Exception(message);
