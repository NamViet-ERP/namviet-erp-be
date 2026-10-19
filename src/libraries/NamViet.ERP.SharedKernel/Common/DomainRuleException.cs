namespace NamViet.ERP.SharedKernel.Common;

/// <summary>
/// User-facing business rule failure. Maps to HTTP 422 with <see cref="Exception.Message"/>.
/// Do not use <see cref="InvalidOperationException"/> for domain rules — that stays a 500.
/// </summary>
public sealed class DomainRuleException : Exception
{
    public DomainRuleException(string message) : base(message)
    {
    }

    public DomainRuleException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
