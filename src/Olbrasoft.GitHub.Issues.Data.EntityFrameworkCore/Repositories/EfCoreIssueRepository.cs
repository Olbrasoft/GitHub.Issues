using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// Entity Framework Core implementation of IIssueRepository.
/// </summary>
public class EfCoreIssueRepository : IIssueRepository
{
    private readonly GitHubDbContext _context;

    public EfCoreIssueRepository(GitHubDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<Issue?> GetIssueWithDetailsAsync(int issueId, CancellationToken cancellationToken = default)
    {
        return await _context.Issues
            .Include(i => i.Repository)
            .Include(i => i.IssueLabels)
                .ThenInclude(il => il.Label)
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);
    }

    public async Task<Issue?> GetIssueWithRepositoryAsync(int issueId, CancellationToken cancellationToken = default)
    {
        return await _context.Issues
            .Include(i => i.Repository)
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);
    }

    public async Task<List<Issue>> GetIssuesByIdsWithRepositoryAsync(List<int> issueIds, CancellationToken cancellationToken = default)
    {
        return await _context.Issues
            .Include(i => i.Repository)
            .Where(i => issueIds.Contains(i.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Issues.CountAsync(cancellationToken);
    }
}
