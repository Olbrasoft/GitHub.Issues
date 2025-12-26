# AGENTS.md - AI Agent Guide

This file provides essential context for AI agents (Claude Code, GitHub Copilot, etc.) working on this repository.

## 🎯 Current Project State (2025-12-26)

### Production Deployment

- **Status**: ✅ RUNNING
- **URL**: https://plumbaginous-zoe-unexcusedly.ngrok-free.dev
- **Port**: 5156 (HTTP only)
- **Location**: `/opt/olbrasoft/github-issues/app/`
- **Startup**: `/home/jirka/.local/bin/github-start.sh` (manual, not systemd)
- **Logs**: `~/.local/state/github-issues/app.log`

### Technology Stack

| Component | Technology | Version | Notes |
|-----------|------------|---------|-------|
| Framework | .NET | 10.0 | Target framework: `net10.0` |
| Database | SQL Server | 2025 | Docker container `mssql` (localhost:1433) |
| Embeddings | Cohere | API | Default: embed-multilingual-v3.0 (1024d) |
| AI Summary | Cerebras/Groq | API | With fallback to OpenRouter/Ollama |
| Translation | Cohere/Google/Azure/Bing | API | With fallback chain |
| Web Framework | ASP.NET Core Razor Pages | 10.0 | Clean Architecture |
| Testing | xUnit + Moq | - | 393+ tests |

### Database Configuration

**CRITICAL**: This project uses **SQL Server 2025** (NOT PostgreSQL) as primary database.

```
Server: localhost,1433
Database: GitHubIssues
User: sa
Password: Stored in environment variable (NOT in JSON)
Connection String: Server=localhost,1433;Database=GitHubIssues;User Id=sa;Password=xxx;TrustServerCertificate=True;Encrypt=True;
```

**PostgreSQL support exists but is OPTIONAL** - only use if explicitly requested by user.

### Recent Refactorings (Issues #278-#280)

#### Issue #280: Repository Pattern Abstractions
- **Date**: 2025-12-26
- **Changes**:
  - Created `ITranslationRepository` abstraction for data access
  - Extracted translation-related queries from `GitHubDbContext`
  - Implemented `EfCoreTranslationRepository` wrapper
- **Impact**: Business layer (`TitleTranslationService`, `IssueSummaryService`) no longer depends on `GitHubDbContext`

#### Issue #278: IssueDetailService Refactoring
- **Date**: 2025-12-26
- **Changes**:
  - Refactored `IssueDetailService` to follow SRP
  - Created specialized services: `TitleTranslationService`, `IssueSummaryService`
  - Delegated responsibilities to specialized services
- **Impact**: Cleaner separation of concerns, easier to test

#### Issue #279: GitHubGraphQLClient Refactoring
- **Date**: 2025-12-26
- **Changes**:
  - Extracted query building logic → `IGraphQLQueryBuilder` / `GraphQLQueryBuilder`
  - Extracted response parsing logic → `IGraphQLResponseParser` / `GraphQLResponseParser`
  - Removed private `BuildQuery()` and `ParseResponse()` methods
- **Impact**: Better testability, SRP compliance

### CI/CD Verification Rules (CRITICAL)

**BEFORE reporting "deployment complete" to user, you MUST verify:**

1. ✅ **GitHub Actions CI passed**
   ```bash
   sleep 30  # Wait for CI to start
   gh run list --limit 1 | grep "completed.*success"
   ```

2. ✅ **Application is running** (if web app)
   ```bash
   ss -tulpn | grep 5156  # Port listening
   curl -I http://localhost:5156  # HTTP 200 OK
   ```

3. ✅ **Playwright test passes**
   ```javascript
   mcp__playwright__browser_navigate({ url: "http://localhost:5156" })
   ```

**Location**: See `~/GitHub/Olbrasoft/engineering-handbook/development-guidelines/ci-cd/local-apps/CLAUDE.md` for full details.

## 📁 Architecture Overview

### Clean Architecture + CQRS

```
PRESENTATION (RazorPages, Sync CLI)
    ↓
BUSINESS (Services + IMediator) → Commands/Queries
    ↓
DATA (Entities, Command/Query definitions)
    ↓
INFRASTRUCTURE (Data.EntityFrameworkCore)
    - GitHubDbContext (ONLY here!)
    - QueryHandlers, CommandHandlers
```

**Key Rule**: `GitHubDbContext` exists ONLY in `Data.EntityFrameworkCore`. All other projects use `IMediator` + Commands/Queries.

### Data Flow

```
User Request → Business Service → Command/Query → IMediator → Handler → DbContext → SQL Server
```

### Project Structure

| Project | Layer | Purpose |
|---------|-------|---------|
| `Data` | Domain | Entities, Commands, Queries (no DB access) |
| `Data.EntityFrameworkCore` | Infrastructure | DbContext, Handlers (CQRS implementation) |
| `Business` | Business | Services using IMediator for data operations |
| `Text.Transformation.*` | Business | Embedding/translation/summarization providers |
| `Sync` | Presentation | CLI sync tool with SRP API clients |
| `AspNetCore.RazorPages` | Presentation | Web UI with Extension methods (SRP) |

### Current Service Architecture

#### Business Layer Services

| Service | Purpose | Dependencies |
|---------|---------|-------------|
| `IssueSearchService` | Semantic vector search | IMediator |
| `IssueDetailService` | Issue detail orchestration | Specialized services below |
| `TitleTranslationService` | Title translation with cache | ITranslationRepository, ITranslator, INotifier |
| `IssueSummaryService` | AI summary with cache | ITranslationRepository, ISummarizationService, INotifier |
| `TranslationCacheService` | Cache management | GitHubDbContext |
| `IssueSyncBusinessService` | Issue CRUD via CQRS | IMediator |
| `LabelSyncBusinessService` | Label CRUD via CQRS | IMediator |
| `RepositorySyncBusinessService` | Repository CRUD via CQRS | IMediator |
| `EventSyncBusinessService` | Event CRUD via CQRS | IMediator |
| `GitHubGraphQLClient` | GitHub GraphQL API | IGraphQLQueryBuilder, IGraphQLResponseParser |

#### Specialized Translation/Summarization Services

| Service | Provider | Purpose |
|---------|----------|---------|
| `CohereEmbeddingService` | Cohere | Embeddings (1024d) |
| `OllamaEmbeddingService` | Ollama | Embeddings (768d) |
| `CohereTranslator` | Cohere | Translation |
| `GoogleTranslator` | Google | Translation fallback |
| `AzureTranslator` | Azure | Translation fallback |
| `BingTranslator` | Bing | Translation fallback |
| `OpenAICompatibleSummarizationService` | Cerebras/Groq/OpenRouter | AI summaries |

## 🧪 Testing Strategy

### Test Count: 393+ tests

### Integration Tests

- **Attribute**: `[SkipOnCIFact]` from NuGet package `Olbrasoft.Testing.Xunit.Attributes`
- **Behavior**: Automatically skip on CI environments (GitHub Actions, Azure DevOps)
- **Run locally**: Executes normally
- **Why**: Integration tests call external APIs (GitHub, Cohere) - not suitable for CI

### Test Structure

Each source project has its own test project:

```
src/ProjectName.Core/
    Models/
        MyClass.cs
tests/ProjectName.Core.Tests/
    Models/
        MyClassTests.cs  ← [Fact] / [Theory] / [SkipOnCIFact]
```

### Running Tests

```bash
# All tests (integration tests skip automatically on CI)
dotnet test --verbosity minimal

# Specific project
dotnet test test/Olbrasoft.GitHub.Issues.Business.Tests
```

## 🚀 Deployment

### Production Deployment Path

```
/opt/olbrasoft/github-issues/
├── app/          # Binaries (deployed here via deploy.sh)
├── config/       # Configuration files
├── data/         # Runtime data
└── logs/         # Application logs
```

### Deployment Script

**Location**: `~/Olbrasoft/GitHub.Issues/deploy/deploy.sh`

**CRITICAL**: Always use this script (NOT manual `dotnet publish`)

```bash
cd ~/Olbrasoft/GitHub.Issues
sudo ./deploy/deploy.sh /opt/olbrasoft/github-issues
```

### Starting/Stopping Application

```bash
# Start (runs ngrok, GitHub Actions runners, application)
gi start  # Alias for /home/jirka/.local/bin/github-start.sh

# Stop
gi stop

# Check status
ss -tulpn | grep 5156
curl -I http://localhost:5156
```

### Environment Variables (Production)

**CRITICAL**: Production requires environment variables (NOT appsettings.json):

```bash
ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=GitHubIssues;User Id=sa;Password=xxx;TrustServerCertificate=True;Encrypt=True;"
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://localhost:5156  # MUST be set (launchSettings.json ignored in production)
GitHub__Token="ghp_xxx"
GitHub__ClientSecret="xxx"
AiProviders__Cohere__Keys__0="xxx"
AiProviders__Cerebras__Keys__0="xxx"
AiProviders__Groq__Keys__0="xxx"
```

## 🔄 Git Workflow

### Branch Naming

- `fix/issue-N-description` - Bug fixes
- `feature/issue-N-description` - New features

### Commit Requirements

- **Frequency**: Commit + push after every significant change
- **Tests**: MUST pass before merge
- **Issue Closure**: Only close issues with user approval

### GitHub Actions Self-Hosted Runner

**Name**: `debian-github-issues`
**Location**: `~/actions-runner-github-issues/`
**Service**: `actions.runner.Olbrasoft-GitHub.Issues.debian-github-issues.service`

**CRITICAL PATH Configuration**:
```ini
Environment="PATH=/home/jirka/.dotnet:/home/jirka/.local/bin:/usr/local/bin:/usr/bin:/bin"
```

**Why**: System-wide `dotnet` is .NET SDK 8.0, but project requires .NET 10 SDK (in `~/.dotnet/`).

## ⚠️ Common Issues & Solutions

### Issue: Wrong Database Provider

**Symptom**: Code references PostgreSQL or tries to use `localhost:5432`

**Fix**:
- Use **SQL Server** (`localhost:1433`)
- Database name: `GitHubIssues` (NOT `github` or `github_issues`)
- Connection string format: `Server=localhost,1433;Database=GitHubIssues;User Id=sa;Password=xxx;TrustServerCertificate=True;Encrypt=True;`

### Issue: Application Runs on Wrong Port

**Symptom**: Application listens on port 5000 instead of 5156

**Fix**: Ensure `ASPNETCORE_URLS=http://localhost:5156` environment variable is set in startup script

### Issue: Tests Fail on CI

**Symptom**: Integration tests fail on GitHub Actions

**Fix**: Ensure tests use `[SkipOnCIFact]` attribute (NOT `[Fact]`)

### Issue: CI Build Fails - "NETSDK1045"

**Symptom**: GitHub Actions workflow fails with ".NET SDK does not support targeting .NET 10.0"

**Fix**: Check PATH in runner systemd service - must include `/home/jirka/.dotnet` FIRST

### Issue: Missing Secrets

**Symptom**: Application starts but features fail (translation, embeddings, etc.)

**Fix**: Check environment variables in startup script (`github-start.sh`)

## 📚 References

### Documentation

- **Main README**: `~/Olbrasoft/GitHub.Issues/README.md`
- **Project CLAUDE.md**: `~/Olbrasoft/GitHub.Issues/CLAUDE.md` (CRITICAL constraints)
- **Wiki**: https://github.com/Olbrasoft/GitHub.Issues/wiki
- **Engineering Handbook**: `~/GitHub/Olbrasoft/engineering-handbook/`

### Engineering Handbook Key Files

| Topic | File |
|-------|------|
| Git Workflow | `development-guidelines/workflow/CLAUDE.md` |
| CI/CD (Local Apps) | `development-guidelines/ci-cd/local-apps/CLAUDE.md` |
| SOLID Principles | `solid-principles/CLAUDE.md` |
| Design Patterns | `design-patterns/CLAUDE.md` |

### External Services

| Service | URL | Purpose |
|---------|-----|---------|
| Cohere API | https://cohere.com | Embeddings + Translation |
| Cerebras API | https://cerebras.net | AI Summarization (primary) |
| Groq API | https://groq.com | AI Summarization (fallback) |
| GitHub API | https://api.github.com | Issue synchronization |

## 🤖 AI Agent Best Practices

### When Working on This Project

1. **ALWAYS read `CLAUDE.md` first** - contains CRITICAL constraints
2. **Check Engineering Handbook** for workflow/standards before starting
3. **Verify current state** (database, ports, services) before making assumptions
4. **Test CI before reporting completion** - see CI Verification Rules above
5. **Commit frequently** - after every significant change
6. **Send notifications** - use `mcp__notify__notify` with `issueIds` parameter
7. **NEVER close issues** without user approval
8. **Research first** - search internet for solutions before implementing

### Notification Requirements

**Format**:
```javascript
// Task start
mcp__notify__notify({
  text: "Začínám pracovat na issue 15...",
  issueIds: [15]
})

// Task complete
mcp__notify__notify({
  text: "Issue 15 dokončeno, CI prošlo, aplikace nasazena.",
  issueIds: [15]
})
```

### Testing Requirements

- **Framework**: xUnit + Moq (NOT NUnit/NSubstitute)
- **Structure**: Separate test project per source project
- **Integration tests**: Use `[SkipOnCIFact]` attribute
- **Run before deploy**: `dotnet test --verbosity minimal`

## 📊 Project Statistics (2025-12-26)

- **Total Tests**: 393+
- **Projects**: 14 source + 9 test
- **Lines of Code**: ~25,000+
- **Database Tables**: 9 (Repository, Issue, Label, IssueLabel, EventType, IssueEvent, CachedText, Language, TextType)
- **API Endpoints**: ~15 (search, detail, status, health, etc.)
- **GitHub Issues Synced**: 1000+ across multiple repositories

---

**Last Updated**: 2025-12-26 by Claude Code
**Project Version**: 1.0 (continuous deployment)
