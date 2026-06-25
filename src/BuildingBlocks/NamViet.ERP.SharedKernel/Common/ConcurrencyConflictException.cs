namespace NamViet.ERP.SharedKernel.Common;

/// <summary>
/// Raised when EF detects an optimistic concurrency conflict (stale row version).
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("Dữ liệu đã được người khác cập nhật. Vui lòng tải lại và thử lại.")
    {
    }

    public ConcurrencyConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
