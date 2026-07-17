namespace NamViet.ERP.Api.Application.Services.Wms;

public sealed class StocktakeFreezeConflictException(string message) : Exception(message);
