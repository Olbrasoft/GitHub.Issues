using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// Entity Framework Core implementation of ILabelRepository.
/// </summary>
public class EfCoreLabelRepository : ILabelRepository
{
    private readonly GitHubDbContext _context;

    public EfCoreLabelRepository(GitHubDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<List<Label>> GetByRepositoryIdAsync(int repositoryId, CancellationToken cancellationToken = default)
    {
        return await _context.Labels
            .Where(l => l.RepositoryId == repositoryId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Label?> GetByIdAsync(int labelId, CancellationToken cancellationToken = default)
    {
        return await _context.Labels.FindAsync(new object[] { labelId }, cancellationToken);
    }

    public async Task<Label?> GetByNameAsync(int repositoryId, string name, CancellationToken cancellationToken = default)
    {
        return await _context.Labels
            .FirstOrDefaultAsync(l => l.RepositoryId == repositoryId && l.Name == name, cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Labels.CountAsync(cancellationToken);
    }
}
