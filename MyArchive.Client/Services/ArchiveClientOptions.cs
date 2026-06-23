namespace MyArchive.Client.Services;

public sealed class ArchiveClientOptions(string baseUrl)
{
    public string BaseUrl { get; } = baseUrl.TrimEnd('/');

    public string BuildExportUrl(string format) => $"{BaseUrl}/api/library/export/{format}";
}
