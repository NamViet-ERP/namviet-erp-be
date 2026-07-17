using System.IO.Compression;

namespace NamViet.ERP.Api.Application.Services.Hr;

public static class HrUploadValidator
{
    private static readonly Dictionary<string, string[]> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"],
        [".jpg"] = ["image/jpeg", "image/jpg"],
        [".jpeg"] = ["image/jpeg", "image/jpg"],
        [".png"] = ["image/png"],
        [".doc"] = ["application/msword"],
        [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
    };

    public static bool IsValid(string fileName, string contentType, ReadOnlySpan<byte> bytes, bool cvOnly)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedMimeTypes.TryGetValue(extension, out var mimeTypes)
            || !mimeTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            return false;
        if (cvOnly && extension is not (".pdf" or ".doc" or ".docx"))
            return false;

        return extension switch
        {
            ".pdf" => bytes.StartsWith("%PDF-"u8),
            ".jpg" or ".jpeg" => bytes.Length >= 3
                && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
            ".png" => bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            ".doc" => bytes.StartsWith(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }),
            ".docx" => IsDocx(bytes),
            _ => false,
        };
    }

    private static bool IsDocx(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4b) return false;
        try
        {
            using var stream = new MemoryStream(bytes.ToArray(), writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            return archive.GetEntry("[Content_Types].xml") is not null
                && archive.GetEntry("word/document.xml") is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
