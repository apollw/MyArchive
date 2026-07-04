namespace MyArchive.Shared;

public static class ArchiveMetadata
{
    public const string DefaultCoverPath = "/images/placeholder-cover.svg";

    public static readonly IReadOnlyList<string> Categories =
    [
        "Livros",
        "Mangas",
        "HQs Singles",
        "HQs Encadernados",
        "Games",
        "Filmes",
        "Series",
        "Series animadas",
        "Animes"
    ];

    public static readonly IReadOnlyList<string> GameMediaOptions =
    [
        "Fisica",
        "Digital"
    ];

    public static readonly IReadOnlyList<string> GamePlatformOptions =
    [
        "PS1",
        "PS2",
        "PS3",
        "PS4",
        "PS5",
        "Xbox 360",
        "Xbox One",
        "Xbox Series",
        "Nintendo Switch",
        "PC",
        "Outros"
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> StatusesByCategory =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Livros"] = ["Nao lido", "Lendo", "Lido"],
            ["Mangas"] = ["Nao lido", "Lendo", "Lido"],
            ["HQs Singles"] = ["Nao lido", "Lendo", "Lido"],
            ["HQs Encadernados"] = ["Nao lido", "Lendo", "Lido"],
            ["Games"] = ["Nao finalizado", "Jogando", "Finalizado", "Gratuito", "Outro dono", "Nao zeravel"],
            ["Filmes"] = ["Nao visto", "Vendo", "Assistido"],
            ["Series"] = ["Nao vista", "Vendo", "Assistida"],
            ["Series animadas"] = ["Nao vista", "Vendo", "Assistida"],
            ["Animes"] = ["Nao vista", "Vendo", "Assistida"]
        };

    private static readonly HashSet<string> InProgressStatuses =
    [
        "Lendo",
        "Jogando",
        "Vendo"
    ];

    private static readonly HashSet<string> CompletedStatuses =
    [
        "Lido",
        "Finalizado",
        "Assistido",
        "Assistida"
    ];

    public static IReadOnlyList<string> GetStatuses(string? category)
    {
        var normalizedCategory = NormalizeCategory(category);
        return StatusesByCategory[normalizedCategory];
    }

    public static string NormalizeCategory(string? category)
    {
        if (string.Equals(category?.Trim(), "HQs", StringComparison.OrdinalIgnoreCase))
        {
            return "HQs Encadernados";
        }

        return Categories.FirstOrDefault(item => item.Equals(category, StringComparison.OrdinalIgnoreCase))
               ?? Categories.First();
    }

    public static string NormalizeStatus(string? category, string? status)
    {
        var statuses = GetStatuses(category);
        return statuses.FirstOrDefault(item => item.Equals(status, StringComparison.OrdinalIgnoreCase))
               ?? statuses[0];
    }

    public static string GetStatusCssClass(string? status)
        => NormalizeStatusLabel(status) switch
        {
            "Lido" or "Finalizado" or "Assistido" or "Assistida" => "status-completed",
            "Lendo" or "Jogando" or "Vendo" => "status-in-progress",
            "Nao lido" or "Nao finalizado" or "Nao visto" or "Nao vista" => "status-not-started",
            "Gratuito" => "status-freebie",
            "Outro dono" => "status-borrowed",
            "Nao zeravel" => "status-nonfinishable",
            _ => "status-not-started"
        };

    public static bool IsGameCategory(string? category)
        => string.Equals(NormalizeCategory(category), "Games", StringComparison.OrdinalIgnoreCase);

    public static bool IsEpisodicCategory(string? category)
    {
        var normalizedCategory = NormalizeCategory(category);
        return normalizedCategory is "Series" or "Series animadas" or "Animes";
    }

    public static bool IsTrackableUnitCategory(string? category)
    {
        var normalizedCategory = NormalizeCategory(category);
        return normalizedCategory is "Mangas" or "HQs Singles" or "Series" or "Series animadas" or "Animes";
    }

    public static string GetUnitSingular(string? category)
    {
        var normalizedCategory = NormalizeCategory(category);
        return normalizedCategory switch
        {
            "Mangas" => "volume",
            "HQs Singles" => "numero",
            _ => "episodio"
        };
    }

    public static string GetUnitPlural(string? category)
    {
        var normalizedCategory = NormalizeCategory(category);
        return normalizedCategory switch
        {
            "Mangas" => "volumes",
            "HQs Singles" => "numeros",
            _ => "episodios"
        };
    }

    public static string GetUnitTitle(string? category, int number)
    {
        var label = GetUnitSingular(category);
        return $"{char.ToUpperInvariant(label[0])}{label[1..]} {number}";
    }

    public static bool IsNotStarted(string? category, string? status)
        => string.Equals(NormalizeStatus(category, status), GetStatuses(category).First(), StringComparison.OrdinalIgnoreCase);

    public static bool IsInProgress(string? category, string? status)
        => InProgressStatuses.Contains(NormalizeStatus(category, status));

    public static bool IsCompleted(string? category, string? status)
        => CompletedStatuses.Contains(NormalizeStatus(category, status));

    public static string GetGroupTitle(ArchiveGroupBy groupBy, string key)
        => groupBy == ArchiveGroupBy.Status ? NormalizeStatusLabel(key) : NormalizeCategory(key);

    public static string NormalizeStatusLabel(string? status)
        => status?.Trim() ?? string.Empty;
}