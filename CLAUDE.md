# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ASP.NET Core Razor Pages application for semantic search of GitHub issues using vector embeddings. Supports dual embedding providers (Cohere cloud, Ollama local), AI summarization, and translation.

**Production:** https://plumbaginous-zoe-unexcusedly.ngrok-free.dev

---

## Build & Test Commands

```bash
# Build
dotnet build

# Test (integration tests skip automatically on CI via [SkipOnCIFact] attribute)
dotnet test --verbosity minimal

# Run single test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Run specific test project
dotnet test test/Olbrasoft.GitHub.Issues.Business.Tests

# Deploy
sudo ./deploy/deploy.sh /opt/olbrasoft/github-issues

# Start/Stop application
gi start
gi stop
```

### EF Core Migrations

```bash
# Add migration (SQL Server)
dotnet ef migrations add MigrationName \
  --startup-project ./src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages \
  --project ./src/Olbrasoft.GitHub.Issues.Migrations.SqlServer \
  -- --provider SqlServer

# Apply migrations
dotnet ef database update \
  --startup-project ./src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages \
  --project ./src/Olbrasoft.GitHub.Issues.Migrations.SqlServer \
  -- --provider SqlServer
```

---

## Architecture

Clean Architecture with CQRS pattern:

```
Presentation Layer (AspNetCore.RazorPages, Sync CLI)
         │
         ▼
Business Layer (Business/) ──── Uses IMediator.Send()
         │
         ▼
Data Layer (Data/) ──── Commands/Queries definitions (NO database access)
         │
         ▼
Infrastructure (Data.EntityFrameworkCore/) ──── DbContext, Handlers
```

**Key Rule**: `GitHubDbContext` exists ONLY in `Data.EntityFrameworkCore`. No other project accesses it directly.

### Main Projects

| Project | Purpose |
|---------|---------|
| `AspNetCore.RazorPages` | Web UI, SignalR hub, minimal API endpoints |
| `Business` | Services: Search, Detail, Sync, Summarization, Translation |
| `Data` | Entities, CQRS Commands/Queries (abstractions only) |
| `Data.EntityFrameworkCore` | DbContext, Query/Command handlers |
| `Sync` | CLI tool for GitHub sync |
| `Migrations.SqlServer` | SQL Server migrations |
| `Text.Transformation.*` | Embedding/Translation/Summarization providers |

### Business Services Structure

```
Business/
├── Search/         # IssueSearchService (semantic search)
├── Detail/         # IssueDetailService, IssueBodyFetchService
├── Sync/           # IssueSyncBusinessService, LabelSyncBusinessService, etc.
├── Summarization/  # IssueSummaryService, AiSummarizationService, SummaryCacheService
├── Translation/    # TitleTranslationService, TranslationFallbackService
└── Database/       # DatabaseStatusService
```

---

## Critical Configuration (DO NOT CHANGE)

### Database: SQL Server 2025 (Docker) ONLY

```
Server=localhost,1433;Database=GitHubIssues;User Id=sa;Password=Tuma/*-+;TrustServerCertificate=True;Encrypt=True;
```

**NEVER use:**
- PostgreSQL (`localhost:5432`)
- Azure SQL Server (`olbrasoft-mssql.database.windows.net`)
- Database names `github` or `github_issues`

**If database error:** Check connection string → must be `Server=localhost,1433;Database=GitHubIssues`

### Port: 5156 (HTTP only)

Set via `ASPNETCORE_URLS=http://localhost:5156` (ngrok tunnel configured for this port)

### Deployment Path: `/opt/olbrasoft/github-issues/`

```
/opt/olbrasoft/github-issues/
├── app/     # Binaries
├── config/  # Configuration
├── data/    # Runtime data
└── logs/    # Logs
```

---

## External Dependencies

| Service | Location | Purpose |
|---------|----------|---------|
| SQL Server | Docker `mssql` localhost:1433 | Main database |
| Ollama | localhost:11434 | Local embeddings (nomic-embed-text, 768d) |
| Cohere | API | Production embeddings (1024d), translation |
| Cerebras/Groq | API | AI summarization |

---

## Testing

- **Framework:** xUnit + Moq (NOT NUnit/NSubstitute)
- **Integration tests:** Use `[SkipOnCIFact]` attribute from `Olbrasoft.Testing.Xunit.Attributes`
- Integration tests skip automatically on CI (GitHub Actions, Azure DevOps)
- Locally, integration tests run normally

---

## GitHub Actions Self-Hosted Runner

**Runner:** `debian-github-issues` at `~/actions-runner-github-issues/`

**Critical PATH configuration** (for .NET 10 SDK):
```ini
Environment="PATH=/home/jirka/.dotnet:/home/jirka/.local/bin:/usr/local/bin:/usr/bin:/bin"
```

Location: `/etc/systemd/system/actions.runner.Olbrasoft-GitHub.Issues.debian-github-issues.service`

If "NETSDK1045" error: Edit systemd service, add PATH with `~/.dotnet` first, then `daemon-reload` and restart.

---

## CQRS Pattern

### Adding a Query

1. Create in `Data/Queries/`:
```csharp
public class MyQuery : BaseQuery<MyResult>
{
    public MyQuery(IMediator mediator) : base(mediator) { }
    public int Parameter { get; set; }
}
```

2. Create handler in `Data.EntityFrameworkCore/QueryHandlers/`:
```csharp
public class MyQueryHandler : GitHubDbQueryHandler<Entity, MyQuery, MyResult>
{
    public MyQueryHandler(GitHubDbContext context) : base(context) { }
    public override async Task<MyResult> HandleAsync(MyQuery query, CancellationToken ct) => ...
}
```

3. Use: `var result = await new MyQuery(Mediator) { Parameter = value }.ToResultAsync(ct);`

---

## Common Issues

| Issue | Cause | Fix |
|-------|-------|-----|
| Wrong port (5000) | `ASPNETCORE_URLS` not set | Check `github-start.sh` |
| SQL auth failed | Password not in env var | Check `ConnectionStrings__DefaultConnection` |
| "Ollama is not valid" | Wrong config loaded | Check `/opt/olbrasoft/github-issues/config/appsettings.json` |
| NETSDK1045 on CI | Wrong .NET SDK | Fix PATH in runner systemd service |

---

## Post-Deployment Testing

Test with Playwright:
1. GitHub OAuth login
2. Semantic search (Cohere embeddings)
3. Issue detail view
4. AI summary display
5. Czech translation
6. Repository filtering

---

## Package Versioning

**Olbrasoft packages use `*` (wildcard) version** - always get the latest version automatically:
```xml
<PackageReference Include="Olbrasoft.Text.Translation.Abstractions" Version="*" />
<PackageReference Include="Olbrasoft.Data.Cqrs" Version="*" />
```

**NEVER use specific versions for Olbrasoft.* packages** - use `*` instead to ensure automatic updates.

---

## Notes for Claude Code

1. **Database is SQL Server** - never PostgreSQL
2. **Port 5156** - hardcoded in ngrok, never change
3. **Never commit passwords** to Git
4. **Test after deployment** before marking complete
5. **Ask user** if unsure about configuration changes
6. **NEVER run sync for ALL repositories** - this will overwhelm API rate limits and cause failures; always sync ONE repository at a time
7. **Olbrasoft packages use `*` version** - never use specific versions like `10.0.3`, always use `*`
