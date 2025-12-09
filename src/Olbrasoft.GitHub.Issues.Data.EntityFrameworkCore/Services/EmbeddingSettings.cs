namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

public class EmbeddingSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "nomic-embed-text";
}
