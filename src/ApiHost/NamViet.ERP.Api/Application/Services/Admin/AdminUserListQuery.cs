namespace NamViet.ERP.Api.Application.Services.Admin;

public sealed record AdminUserListQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null,
    bool PendingOnly = false,
    string? AccountStatus = null,
    string? Source = null,
    string? Department = null);
