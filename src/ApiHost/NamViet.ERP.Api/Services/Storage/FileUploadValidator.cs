using System.Text;

namespace NamViet.ERP.Api.Services.Storage;

/// <summary>
/// Centralized file upload validation: extension whitelist, dangerous extension/MIME blocking,
/// and magic-byte verification for binary formats.
/// </summary>
public static class FileUploadValidator
{
    public static readonly IReadOnlySet<string> StandardOfficeAndImageExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif",
            ".pdf",
            ".doc", ".docx", ".rtf",
            ".xls", ".xlsx", ".xlsm", ".xlsb", ".csv",
            ".ppt", ".pptx",
            ".txt",
        };

    public static readonly IReadOnlySet<string> DefectAttachmentExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif",
            ".pdf",
            ".doc", ".docx",
            ".xls", ".xlsx",
        };

    public static readonly IReadOnlySet<string> WmsImageExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp",
        };

    public static readonly IReadOnlySet<string> WmsDocumentExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx",
            ".jpg", ".jpeg", ".png", ".webp",
        };

    public static readonly IReadOnlySet<string> MaterialImageExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif",
        };

    public static readonly IReadOnlySet<string> ProjectDocumentExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".rtf",
            ".xls", ".xlsx", ".xlsm", ".xlsb", ".csv",
            ".ppt", ".pptx",
            ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp",
            ".txt", ".dwg", ".zip", ".rar",
        };

    private static readonly HashSet<string> ForbiddenExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".html", ".htm", ".xhtml", ".svg", ".svgz",
            ".js", ".mjs", ".cjs", ".ts",
            ".exe", ".dll", ".msi", ".com", ".scr",
            ".bat", ".cmd", ".sh", ".bash", ".zsh", ".ps1", ".psm1", ".vbs", ".wsf",
            ".php", ".jsp", ".asp", ".aspx", ".py", ".rb", ".pl",
        };

    private static readonly HashSet<string> ForbiddenMimeTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "text/html",
            "application/xhtml+xml",
            "image/svg+xml",
            "application/javascript",
            "text/javascript",
            "application/x-javascript",
            "application/ecmascript",
            "text/ecmascript",
            "application/x-msdownload",
            "application/x-dosexec",
            "application/x-executable",
            "application/x-sh",
            "application/x-shellscript",
            "application/x-bat",
            "text/x-script",
        };

    private static readonly Dictionary<string, string[]> AllowedMimeTypesByExtension =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ["application/pdf", "application/x-pdf", "application/octet-stream"],
            [".png"] = ["image/png", "application/octet-stream"],
            [".jpg"] = ["image/jpeg", "image/jpg", "image/pjpeg", "application/octet-stream"],
            [".jpeg"] = ["image/jpeg", "image/jpg", "image/pjpeg", "application/octet-stream"],
            [".gif"] = ["image/gif", "application/octet-stream"],
            [".webp"] = ["image/webp", "application/octet-stream"],
            [".bmp"] = ["image/bmp", "image/x-ms-bmp", "application/octet-stream"],
            [".doc"] = ["application/msword", "application/octet-stream"],
            [".xls"] = ["application/vnd.ms-excel", "application/octet-stream"],
            [".ppt"] = ["application/vnd.ms-powerpoint", "application/octet-stream"],
            [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/octet-stream"],
            [".xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/octet-stream"],
            [".xlsm"] = ["application/vnd.ms-excel.sheet.macroenabled.12", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/octet-stream"],
            [".xlsb"] = ["application/vnd.ms-excel.sheet.binary.macroenabled.12", "application/octet-stream"],
            [".pptx"] = ["application/vnd.openxmlformats-officedocument.presentationml.presentation", "application/octet-stream"],
            [".csv"] = ["text/csv", "application/csv", "application/vnd.ms-excel", "text/plain", "application/octet-stream"],
            [".txt"] = ["text/plain", "application/octet-stream"],
            [".rtf"] = ["application/rtf", "text/rtf", "application/octet-stream"],
            [".dwg"] = ["application/acad", "application/x-acad", "application/autocad_dwg", "image/vnd.dwg", "image/x-dwg", "application/dwg", "application/x-dwg", "application/octet-stream"],
            [".zip"] = ["application/zip", "application/x-zip-compressed", "multipart/x-zip", "application/octet-stream"],
            [".rar"] = ["application/vnd.rar", "application/x-rar-compressed", "application/x-rar", "application/octet-stream"],
        };

    public static bool IsAllowedExtension(string? fileName, IReadOnlySet<string> allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return !string.IsNullOrEmpty(ext)
               && !ForbiddenExtensions.Contains(ext)
               && allowedExtensions.Contains(ext);
    }

    public static void EnsureAllowedExtension(
        string fileName,
        IReadOnlySet<string> allowedExtensions,
        Func<string, Exception> exceptionFactory,
        string? customMessage = null)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (!IsAllowedExtension(fileName, allowedExtensions))
        {
            throw exceptionFactory(customMessage ?? $"Định dạng file không hỗ trợ: {ext}.");
        }
    }

    public static Task<(bool IsValid, string? ErrorMessage)> ValidateProjectDocumentAsync(
        string? fileName,
        string? contentType,
        Stream stream,
        CancellationToken ct = default) =>
        ValidateStreamAsync(fileName, contentType, stream, ProjectDocumentExtensions, ct);

    public static (bool IsValid, string? ErrorMessage) ValidateProjectDocumentBytes(
        string? fileName,
        string? contentType,
        ReadOnlySpan<byte> bytes) =>
        ValidateBytes(fileName, contentType, bytes, ProjectDocumentExtensions);

    public static Task<(bool IsValid, string? ErrorMessage)> ValidateMaterialImageAsync(
        string? fileName,
        string? contentType,
        Stream stream,
        CancellationToken ct = default) =>
        ValidateStreamAsync(fileName, contentType, stream, MaterialImageExtensions, ct);

    public static (bool IsValid, string? ErrorMessage) ValidateMaterialImageBytes(
        string? fileName,
        string? contentType,
        ReadOnlySpan<byte> bytes) =>
        ValidateBytes(fileName, contentType, bytes, MaterialImageExtensions);

    private static (bool IsValid, string? ErrorMessage, string Ext) ValidateMetadata(
        string? fileName,
        string? contentType,
        IReadOnlySet<string> allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return (false, "Tên file không hợp lệ.", string.Empty);

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || ForbiddenExtensions.Contains(ext) || !allowedExtensions.Contains(ext))
            return (false, $"Định dạng file không được phép tải lên: '{ext}'.", ext);

        var normalizedMime = NormalizeContentType(contentType);
        if (!string.IsNullOrEmpty(normalizedMime))
        {
            if (ForbiddenMimeTypes.Contains(normalizedMime))
                return (false, $"Loại nội dung '{normalizedMime}' không được phép tải lên.", ext);

            if (AllowedMimeTypesByExtension.TryGetValue(ext, out var allowedMimes)
                && !allowedMimes.Contains(normalizedMime, StringComparer.OrdinalIgnoreCase))
            {
                return (false, $"Content-Type '{normalizedMime}' không khớp với định dạng '{ext}'.", ext);
            }
        }

        return (true, null, ext);
    }

    private static async Task<(bool IsValid, string? ErrorMessage)> ValidateStreamAsync(
        string? fileName,
        string? contentType,
        Stream stream,
        IReadOnlySet<string> allowedExtensions,
        CancellationToken ct)
    {
        var (metaValid, metaError, ext) = ValidateMetadata(fileName, contentType, allowedExtensions);
        if (!metaValid)
            return (false, metaError);

        var buffer = new byte[Math.Max(64, ext is ".txt" or ".csv" ? 512 : 64)];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            if (read == 0) break;
            totalRead += read;
        }

        if (stream.CanSeek)
            stream.Position = 0;

        if (totalRead == 0)
            return (false, "File rỗng.");

        if (!MatchesMagicBytes(ext, buffer.AsSpan(0, totalRead)))
            return (false, $"Nội dung file không khớp với định dạng '{ext}' (sai magic bytes).");

        return (true, null);
    }

    private static (bool IsValid, string? ErrorMessage) ValidateBytes(
        string? fileName,
        string? contentType,
        ReadOnlySpan<byte> bytes,
        IReadOnlySet<string> allowedExtensions)
    {
        var (metaValid, metaError, ext) = ValidateMetadata(fileName, contentType, allowedExtensions);
        if (!metaValid)
            return (false, metaError);

        if (bytes.IsEmpty)
            return (false, "File rỗng.");

        if (!MatchesMagicBytes(ext, bytes))
            return (false, $"Nội dung file không khớp với định dạng '{ext}' (sai magic bytes).");

        return (true, null);
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return string.Empty;

        var semi = contentType.IndexOf(';');
        return (semi >= 0 ? contentType[..semi] : contentType).Trim().ToLowerInvariant();
    }

    private static bool MatchesMagicBytes(string ext, ReadOnlySpan<byte> bytes) => ext switch
    {
        ".pdf" => bytes.StartsWith("%PDF-"u8),
        ".png" => bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        ".gif" => bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8),
        ".webp" => bytes.Length >= 12
                   && bytes[..4].SequenceEqual("RIFF"u8)
                   && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
        ".bmp" => bytes.StartsWith("BM"u8),
        ".doc" or ".xls" or ".ppt" =>
            bytes.StartsWith(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
        ".docx" or ".xlsx" or ".xlsm" or ".xlsb" or ".pptx" or ".zip" =>
            bytes.StartsWith(new byte[] { 0x50, 0x4B, 0x03, 0x04 })
            || bytes.StartsWith(new byte[] { 0x50, 0x4B, 0x05, 0x06 })
            || bytes.StartsWith(new byte[] { 0x50, 0x4B, 0x07, 0x08 }),
        ".rar" => bytes.StartsWith(new byte[] { 0x52, 0x61, 0x72, 0x21 }),
        ".dwg" => bytes.StartsWith("AC10"u8),
        ".rtf" => bytes.StartsWith("{\\rtf"u8),
        ".txt" or ".csv" => IsSafeTextHeader(bytes),
        _ => false,
    };

    private static bool IsSafeTextHeader(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IndexOf((byte)0) >= 0)
            return false;

        if (bytes.StartsWith("MZ"u8) || bytes.StartsWith(new byte[] { 0x7F, 0x45, 0x4C, 0x46 }))
            return false;

        // Strip UTF-8 BOM if present, then inspect leading text for disguised HTML/SVG/script payloads
        var slice = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
            ? bytes[3..]
            : bytes;
        var head = Encoding.UTF8.GetString(slice[..Math.Min(slice.Length, 128)]).TrimStart();
        if (head.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase)
            || head.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
            || head.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)
            || head.StartsWith("<script", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
