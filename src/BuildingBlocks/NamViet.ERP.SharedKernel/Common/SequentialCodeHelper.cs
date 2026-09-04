using System.Reflection;

namespace NamViet.ERP.SharedKernel.Common;

/// <summary>
/// Sinh mã tuần tự an toàn hơn Count+1: lấy Max (kể cả soft-delete) và nhận diện unique violation để retry.
/// </summary>
public static class SequentialCodeHelper
{
    public const int DefaultMaxAttempts = 3;

    public static int NextSequence(IEnumerable<string> codes, string prefix)
    {
        var max = 0;
        var prefixLen = prefix.Length;
        foreach (var code in codes)
        {
            if (code.Length > prefixLen
                && code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(code.AsSpan(prefixLen), out var n)
                && n > max)
            {
                max = n;
            }
        }

        return max + 1;
    }

    public static string Format(string prefix, int sequence, int padWidth = 4) =>
        $"{prefix}{sequence.ToString($"D{padWidth}")}";

    public static bool IsUniqueViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var sqlState = current.GetType().GetProperty("SqlState", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(current) as string;
            if (string.Equals(sqlState, "23505", StringComparison.Ordinal))
                return true;

            var message = current.Message ?? string.Empty;
            if (message.Contains("23505", StringComparison.Ordinal)
                || message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static async Task<T> ExecuteWithUniqueRetryAsync<T>(
        Func<Task<T>> action,
        int maxAttempts = DefaultMaxAttempts,
        CancellationToken ct = default)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await action();
            }
            catch (Exception ex) when (IsUniqueViolation(ex) && attempt < maxAttempts)
            {
                last = ex;
            }
        }

        throw last ?? new InvalidOperationException("Không thể sinh mã chứng từ duy nhất.");
    }

    public static async Task ExecuteWithUniqueRetryAsync(
        Func<Task> action,
        int maxAttempts = DefaultMaxAttempts,
        CancellationToken ct = default)
    {
        await ExecuteWithUniqueRetryAsync(async () =>
        {
            await action();
            return true;
        }, maxAttempts, ct);
    }
}
