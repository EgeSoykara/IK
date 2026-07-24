namespace IK.Web.Services;

public interface IEmployeeFileStore
{
    Task SaveAsync(
        string canonicalKey,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string canonicalKey,
        CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(
        string canonicalKey,
        CancellationToken cancellationToken = default);
}
