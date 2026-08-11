using System.IO.Compression;

namespace IK.Web.Services;

public static class EmployeeFileContentPolicy
{
    public const long MaxProfilePhotoBytes = 5 * 1024 * 1024;
    public const long MaxDocumentBytes = 20 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> ImageContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

    private static readonly IReadOnlyDictionary<string, string> DocumentContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png"
        };

    public static bool CanPreviewDocument(string contentType) =>
        string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
        || string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
        || string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase);

    public static async Task<ValidatedEmployeeFile> ValidateProfilePhotoAsync(
        EmployeeFileUpload upload,
        CancellationToken cancellationToken = default)
    {
        var fileName = NormalizeFileName(upload.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!ImageContentTypes.TryGetValue(extension, out var contentType))
        {
            throw new EmployeeFileValidationException(
                "Profil fotoğrafı JPG, PNG veya WEBP biçiminde olmalıdır.");
        }

        var content = await ReadBoundedContentAsync(
            upload,
            MaxProfilePhotoBytes,
            "Profil fotoğrafı en fazla 5 MB olabilir.",
            cancellationToken);

        if (!HasExpectedSignature(extension, content))
        {
            throw new EmployeeFileValidationException(
                "Profil fotoğrafının içeriği dosya uzantısıyla eşleşmiyor.");
        }

        return new ValidatedEmployeeFile(fileName, extension, contentType, content);
    }

    public static async Task<ValidatedEmployeeFile> ValidateDocumentAsync(
        EmployeeFileUpload upload,
        CancellationToken cancellationToken = default)
    {
        var fileName = NormalizeFileName(upload.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!DocumentContentTypes.TryGetValue(extension, out var contentType))
        {
            throw new EmployeeFileValidationException(
                "Belge PDF, DOCX, JPG veya PNG biçiminde olmalıdır.");
        }

        var content = await ReadBoundedContentAsync(
            upload,
            MaxDocumentBytes,
            "Belge en fazla 20 MB olabilir.",
            cancellationToken);

        if (!HasExpectedSignature(extension, content))
        {
            throw new EmployeeFileValidationException(
                "Belgenin içeriği dosya uzantısıyla eşleşmiyor.");
        }

        return new ValidatedEmployeeFile(fileName, extension, contentType, content);
    }

    private static async Task<byte[]> ReadBoundedContentAsync(
        EmployeeFileUpload upload,
        long maximumBytes,
        string sizeErrorMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(upload.Content);

        if (upload.Length <= 0)
        {
            throw new EmployeeFileValidationException("Boş dosya yüklenemez.");
        }

        if (upload.Length > maximumBytes)
        {
            throw new EmployeeFileValidationException(sizeErrorMessage);
        }

        using var buffer = new MemoryStream(capacity: checked((int)upload.Length));
        var chunk = new byte[81920];
        long totalBytes = 0;

        while (true)
        {
            var bytesRead = await upload.Content.ReadAsync(chunk, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;
            if (totalBytes > maximumBytes)
            {
                throw new EmployeeFileValidationException(sizeErrorMessage);
            }

            await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
        }

        if (totalBytes == 0)
        {
            throw new EmployeeFileValidationException("Boş dosya yüklenemez.");
        }

        if (totalBytes != upload.Length)
        {
            throw new EmployeeFileValidationException(
                "Dosya boyutu yükleme bilgisiyle eşleşmiyor.");
        }

        return buffer.ToArray();
    }

    private static string NormalizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new EmployeeFileValidationException("Dosya adı boş olamaz.");
        }

        var normalized = Path.GetFileName(fileName.Replace('\\', '/')).Trim();
        if (normalized.Length == 0
            || normalized.Length > 255
            || normalized.Any(char.IsControl))
        {
            throw new EmployeeFileValidationException("Dosya adı geçersiz.");
        }

        return normalized;
    }

    private static bool HasExpectedSignature(string extension, byte[] content)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" => content.Length >= 3
                && content[0] == 0xFF
                && content[1] == 0xD8
                && content[2] == 0xFF,
            ".png" => content.AsSpan().StartsWith(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => content.Length >= 12
                && content.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && content.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            ".pdf" => content.AsSpan().StartsWith("%PDF-"u8),
            ".docx" => IsDocx(content),
            _ => false
        };
    }

    private static bool IsDocx(byte[] content)
    {
        if (content.Length < 4
            || content[0] != 0x50
            || content[1] != 0x4B)
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            return archive.GetEntry("[Content_Types].xml") is not null
                && archive.GetEntry("word/document.xml") is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}

public sealed record ValidatedEmployeeFile(
    string OriginalFileName,
    string Extension,
    string ContentType,
    byte[] Content);
