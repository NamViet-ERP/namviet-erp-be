namespace NamViet.ERP.SharedKernel.Common;

public static class Paging
{
    public const int DefaultMaxPageSize = 200;

    public static (int Page, int PageSize) Normalize(int page, int pageSize, int maxPageSize = DefaultMaxPageSize)
        => (Math.Max(1, page), Math.Clamp(pageSize, 1, maxPageSize));

    public static int Skip(int page, int pageSize) => (page - 1) * pageSize;
}
