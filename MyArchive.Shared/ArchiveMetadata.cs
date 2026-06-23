namespace MyArchive.Shared;

public static class ArchiveMetadata
{
    public static readonly IReadOnlyList<string> SuggestedTypes =
    [
        "Livro",
        "Filme",
        "Serie",
        "Anime",
        "Jogo",
        "Curso",
        "Projeto",
        "Artigo",
        "Documento",
        "Outro"
    ];

    public static string GetLabel(this ItemStatus status) => status switch
    {
        ItemStatus.NotStarted => "Nao iniciado",
        ItemStatus.InProgress => "Em andamento",
        ItemStatus.Completed => "Concluido",
        ItemStatus.Paused => "Pausado",
        ItemStatus.Abandoned => "Abandonado",
        _ => status.ToString()
    };

    public static string GetLabel(this ItemPriority priority) => priority switch
    {
        ItemPriority.Low => "Baixa",
        ItemPriority.Medium => "Media",
        ItemPriority.High => "Alta",
        ItemPriority.Critical => "Critica",
        _ => priority.ToString()
    };

    public static string GetCssClass(this ItemPriority priority) => priority switch
    {
        ItemPriority.Low => "priority-low",
        ItemPriority.Medium => "priority-medium",
        ItemPriority.High => "priority-high",
        ItemPriority.Critical => "priority-critical",
        _ => "priority-medium"
    };

    public static string GetCssClass(this ItemStatus status) => status switch
    {
        ItemStatus.NotStarted => "status-not-started",
        ItemStatus.InProgress => "status-in-progress",
        ItemStatus.Completed => "status-completed",
        ItemStatus.Paused => "status-paused",
        ItemStatus.Abandoned => "status-abandoned",
        _ => "status-not-started"
    };
}
