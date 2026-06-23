using System.Net;
using System.Net.Http.Json;
using System.Text;
using MyArchive.Shared;

namespace MyArchive.Client.Services;

public sealed class ArchiveApiClient(HttpClient httpClient, ArchiveClientOptions options)
{
    public string ExportJsonUrl => options.BuildExportUrl("json");
    public string ExportCsvUrl => options.BuildExportUrl("csv");
    public string ExportMarkdownUrl => options.BuildExportUrl("markdown");

    public async Task<DashboardSummary> GetDashboardAsync()
        => await httpClient.GetFromJsonAsync<DashboardSummary>("api/dashboard")
           ?? throw new InvalidOperationException("Nao foi possivel carregar o dashboard.");

    public async Task<ArchiveCatalog> GetCatalogAsync(ArchiveQuery query)
    {
        var path = new StringBuilder("api/items");
        var values = new List<string>();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            values.Add($"search={Uri.EscapeDataString(query.Search)}");
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            values.Add($"type={Uri.EscapeDataString(query.Type)}");
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            values.Add($"tag={Uri.EscapeDataString(query.Tag)}");
        }

        if (query.Status.HasValue)
        {
            values.Add($"status={query.Status.Value}");
        }

        values.Add($"groupBy={query.GroupBy}");

        if (values.Count > 0)
        {
            path.Append('?');
            path.Append(string.Join('&', values));
        }

        return await httpClient.GetFromJsonAsync<ArchiveCatalog>(path.ToString())
               ?? throw new InvalidOperationException("Nao foi possivel carregar o catalogo.");
    }

    public async Task<ItemEditorModel?> GetItemAsync(Guid id)
    {
        var response = await httpClient.GetAsync($"api/items/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ItemEditorModel>();
    }

    public async Task<ItemEditorModel> SaveItemAsync(ItemEditorModel model)
    {
        using var response = model.Id.HasValue
            ? await httpClient.PutAsJsonAsync($"api/items/{model.Id.Value}", model)
            : await httpClient.PostAsJsonAsync("api/items", model);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ItemEditorModel>()
               ?? throw new InvalidOperationException("A API retornou um item vazio.");
    }

    public async Task DeleteItemAsync(Guid id)
    {
        using var response = await httpClient.DeleteAsync($"api/items/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<ImportSummary> ImportAsync(Stream stream, string fileName, ImportFormat format)
    {
        using var multipart = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new("application/octet-stream");
        multipart.Add(fileContent, "file", fileName);

        using var response = await httpClient.PostAsync($"api/library/import?format={format}", multipart);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ImportSummary>()
               ?? throw new InvalidOperationException("A API nao retornou o resumo da importacao.");
    }

    public static ItemEditorModel CreateNewItem()
        => new()
        {
            Type = ArchiveMetadata.SuggestedTypes.First(),
            CreatedAt = DateTime.Now
        };
}
