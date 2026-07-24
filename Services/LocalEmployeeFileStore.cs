using Microsoft.Extensions.Options;

namespace IK.Web.Services;

public sealed class LocalEmployeeFileStore : IEmployeeFileStore
{
    private readonly string rootPath;
    private readonly string rootPathWithSeparator;
    private readonly StringComparison pathComparison;
    private readonly ILogger<LocalEmployeeFileStore> logger;

    public LocalEmployeeFileStore(
        IOptions<EmployeeFileStorageOptions> options,
        IWebHostEnvironment environment,
        ILogger<LocalEmployeeFileStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);
        this.logger = logger;

        var configuredRoot = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            throw new InvalidOperationException(
                $"{EmployeeFileStorageOptions.SectionName}:RootPath boş olamaz.");
        }

        rootPath = Path.GetFullPath(
            Path.IsPathRooted(configuredRoot)
                ? configuredRoot
                : Path.Combine(environment.ContentRootPath, configuredRoot));
        rootPathWithSeparator = rootPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!string.IsNullOrWhiteSpace(environment.WebRootPath))
        {
            var webRootPath = Path.GetFullPath(environment.WebRootPath);
            var webRootPathWithSeparator = webRootPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (string.Equals(rootPath, webRootPath, pathComparison)
                || rootPath.StartsWith(webRootPathWithSeparator, pathComparison))
            {
                throw new InvalidOperationException(
                    $"{EmployeeFileStorageOptions.SectionName}:RootPath wwwroot veya altındaki bir klasör olamaz.");
            }
        }
    }

    public async Task SaveAsync(
        string canonicalKey,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var absolutePath = ResolveAbsolutePath(canonicalKey);
        var directory = Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException("Dosya klasörü çözümlenemedi.");

        Directory.CreateDirectory(directory);

        var temporaryPath = $"{absolutePath}.{Guid.NewGuid():N}.uploading";

        try
        {
            await using (var destination = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await content.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, absolutePath, overwrite: false);
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception cleanupException)
            {
                logger.LogError(
                    cleanupException,
                    "Incomplete employee file upload could not be removed. TemporaryPath={TemporaryPath}",
                    temporaryPath);
            }

            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(
        string canonicalKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var absolutePath = ResolveAbsolutePath(canonicalKey);
        Stream? stream = File.Exists(absolutePath)
            ? new FileStream(
                absolutePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null;

        return Task.FromResult(stream);
    }

    public Task DeleteIfExistsAsync(
        string canonicalKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var absolutePath = ResolveAbsolutePath(canonicalKey);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    private string ResolveAbsolutePath(string canonicalKey)
    {
        if (string.IsNullOrWhiteSpace(canonicalKey)
            || Path.IsPathRooted(canonicalKey)
            || canonicalKey.Contains('\\', StringComparison.Ordinal)
            || canonicalKey.StartsWith("/", StringComparison.Ordinal)
            || canonicalKey.EndsWith("/", StringComparison.Ordinal)
            || canonicalKey.Contains("//", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Geçersiz employee file canonical key.");
        }

        var segments = canonicalKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0
            || segments.Any(segment =>
                segment is "." or ".."
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new InvalidOperationException("Geçersiz employee file canonical key.");
        }

        var absolutePath = Path.GetFullPath(Path.Combine([rootPath, .. segments]));
        if (!absolutePath.StartsWith(rootPathWithSeparator, pathComparison))
        {
            throw new InvalidOperationException(
                "Employee file canonical key yapılandırılmış klasörün dışına çıkamaz.");
        }

        return absolutePath;
    }
}
