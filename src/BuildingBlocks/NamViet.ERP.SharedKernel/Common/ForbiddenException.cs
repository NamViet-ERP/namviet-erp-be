namespace NamViet.ERP.SharedKernel.Common;

/// <summary>
/// Đã xác thực nhưng thiếu quyền nghiệp vụ → HTTP 403 (không logout như 401).
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}