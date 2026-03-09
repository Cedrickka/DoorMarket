namespace DoorMarket.Application.Interfaces.Storage;

public interface IFileStorage
{
    Task<(string storagePath, string publicUrl, long sizeBytes)> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken ct);
}
