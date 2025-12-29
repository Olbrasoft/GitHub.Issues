using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// Entity Framework Core implementation of IRepositoryRepository.
/// </summary>
public class EfCoreRepositoryRepository : IRepositoryRepository
{
    private readonly GitHubDbContext _context;

    public EfCoreRepositoryRepository(GitHubDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Repositories.CountAsync(cancellationToken);
    }

    public async Task<Repository?> GetByIdAsync(int repositoryId, CancellationToken cancellationToken = default)
    {
        return await _context.Repositories.FindAsync(new object[] { repositoryId }, cancellationToken);
    }

    public async Task<Repository?> GetByGitHubIdAsync(long gitHubId, CancellationToken cancellationToken = default)
    {
        return await _context.Repositories
            .FirstOrDefaultAsync(r => r.GitHubId == gitHubId, cancellationToken);
    }

    public async Task<Repository?> GetByFullNameAsync(string fullName, CancellationToken cancellationToken = default)
    {
        return await _context.Repositories
            .FirstOrDefaultAsync(r => r.FullName == fullName, cancellationToken);
    }
}
