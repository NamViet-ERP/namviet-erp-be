using MediatR;
using Microsoft.Extensions.Logging;

namespace NamViet.ERP.Application.Abstractions.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {Request}", requestName);
        var response = await next(ct);
        logger.LogInformation("Handled {Request}", requestName);
        return response;
    }
}
