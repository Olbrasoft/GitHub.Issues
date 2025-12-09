using Pgvector;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

public interface IEmbeddingService
{
    Task<Vector?> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    bool IsConfigured { get; }
}
