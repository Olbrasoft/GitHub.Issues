# GitHub.Issues

[![Build and Deploy](https://github.com/Olbrasoft/GitHub.Issues/actions/workflows/deploy.yml/badge.svg)](https://github.com/Olbrasoft/GitHub.Issues/actions/workflows/deploy.yml)
[![Uptime Status](https://img.shields.io/uptimerobot/status/m801985171-b443ff66e892b834307a53a1?label=Uptime&style=flat-square)](https://stats.uptimerobot.com/ueMqtXp5wJ)

Semantic search for GitHub issues using vector embeddings. Supports **dual embedding providers**: Cohere (cloud) and Ollama (local). Synchronizes issues from multiple GitHub repositories and enables natural language search.

**Production:** https://plumbaginous-zoe-unexcusedly.ngrok-free.dev
**Demo:** https://github-issues.azurewebsites.net

## Features

- **Semantic Search**: Find issues by meaning, not just keywords (using vector embeddings)
- **Dual Embedding Providers**: Cohere (cloud, 1024d) or Ollama (local, 768d)
- **AI Issue Summarization**: Automatic issue summaries using Cerebras/Groq/OpenRouter/Ollama with provider rotation
- **AI Translation**: Czech translations via Cohere/Google/Azure/Bing with fallback support
- **Multi-Repository Support**: Sync and search across multiple GitHub repositories
- **Repository Filter**: Filter search results by specific repositories
- **Smart Incremental Sync**: Only sync changed issues using stored timestamps
- **Sub-Issues Hierarchy**: Track parent-child relationships between issues
- **Issue Events**: Track issue lifecycle events (opened, closed, labeled, etc.)
- **Labels Sync**: Full label synchronization with colors
- **Multi-Provider Database**: SQL Server 2025 (Docker) or PostgreSQL + pgvector
- **Clean Architecture**: Layered design with CQRS pattern, **393+ unit tests**

## Architecture

The project follows **Clean Architecture** with **CQRS (Command Query Responsibility Segregation)** pattern:

```
┌─────────────────────────────────────────────────────────────────────┐
│  PRESENTATION LAYER                                                 │
│  ┌─────────────────────┐  ┌─────────────────────┐                  │
│  │ AspNetCore.RazorPages│  │ Sync (CLI Worker)   │                  │
│  │ (Web UI + Search)   │  │ (GitHub → Database) │                  │
│  └──────────┬──────────┘  └──────────┬──────────┘                  │
│             └──────────────┬─────────┘                             │
│                            ▼                                        │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │  BUSINESS LAYER (Olbrasoft.GitHub.Issues.Business)          │   │
│  │  ┌─────────────────┐  ┌─────────────────────────────────┐   │   │
│  │  │ IssueSearchSvc  │  │ IssueSyncBusinessService        │   │   │
│  │  │ (IMediator)     │  │ LabelSyncBusinessService        │   │   │
│  │  │                 │  │ RepositorySyncBusinessService   │   │   │
│  │  │                 │  │ EventSyncBusinessService        │   │   │
│  │  └────────┬────────┘  └────────────────┬────────────────┘   │   │
│  │           └──────────────┬─────────────┘                    │   │
│  └──────────────────────────┼──────────────────────────────────┘   │
│                             ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │  DATA LAYER (Olbrasoft.GitHub.Issues.Data)                  │   │
│  │  ┌────────────┐  ┌────────────┐  ┌────────────────────────┐ │   │
│  │  │ Entities   │  │ Commands   │  │ Queries                │ │   │
│  │  │ Issue      │  │ IssueSave  │  │ IssueByRepoAndNumber   │ │   │
│  │  │ Label      │  │ LabelSave  │  │ IssuesByRepository     │ │   │
│  │  │ Repository │  │ ...        │  │ ...                    │ │   │
│  │  └────────────┘  └────────────┘  └────────────────────────┘ │   │
│  │  NO database access - just definitions (abstractions)       │   │
│  └─────────────────────────┬───────────────────────────────────┘   │
│                            ▼                                        │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │  INFRASTRUCTURE (Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore)│
│  │  ┌─────────────────┐  ┌─────────────────────────────────┐   │   │
│  │  │ GitHubDbContext │  │ QueryHandlers                   │   │   │
│  │  │ (ONLY here!)    │  │ CommandHandlers                 │   │   │
│  │  │                 │  │ (Implement CQRS)                │   │   │
│  │  └─────────────────┘  └─────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘

External Services:
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   GitHub API    │     │     Ollama      │     │     Cohere      │
│   (REST/GraphQL)│     │ (nomic-embed)   │     │ (embed-multi)   │
└─────────────────┘     └─────────────────┘     └─────────────────┘
                        ┌─────────────────┐
                        │   OpenRouter    │
                        │ (AI summaries)  │
                        └─────────────────┘
```

### Data Flow

```
User Request → Business Service → Command/Query → Handler → DbContext → SQL Server / PostgreSQL
                     ↓
              Uses IMediator.Send()
                     ↓
            Auto-routes to Handler
```

## Multi-Provider Support

The project supports both **SQL Server** and **PostgreSQL** databases with separate migration assemblies.

### Database Providers

| Provider | Use Case | Vector Storage | Migration Project |
|----------|----------|----------------|-------------------|
| SQL Server | Production (Docker) | `varbinary(max)` | `Migrations.SqlServer` |
| PostgreSQL | Optional | `vector(768)` (pgvector) | `Migrations.PostgreSQL` |

### Embedding Providers

| Provider | Use Case | Dimensions | Model |
|----------|----------|------------|-------|
| Cohere | Production (default) | 1024 | `embed-multilingual-v3.0` |
| Ollama | Local development (optional) | 768 | `nomic-embed-text` |

### Configuration

Configure the provider in `appsettings.json`:

```json
{
  "Database": {
    "Provider": "SqlServer"  // or "PostgreSQL"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=GitHubIssues;User Id=sa;Password=xxx;TrustServerCertificate=True;Encrypt=True;"
  }
}
```

### EF Core Migrations

#### Adding a New Migration

```bash
# PostgreSQL
dotnet ef migrations add MigrationName \
  --startup-project ./src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages \
  --project ./src/Olbrasoft.GitHub.Issues.Migrations.PostgreSQL \
  -- --provider PostgreSQL

# SQL Server
dotnet ef migrations add MigrationName \
  --startup-project ./src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages \
  --project ./src/Olbrasoft.GitHub.Issues.Migrations.SqlServer \
  -- --provider SqlServer
```

#### Applying Migrations

```bash
# PostgreSQL (development)
dotnet ef database update \
  --startup-project ./src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages \
  --project ./src/Olbrasoft.GitHub.Issues.Migrations.PostgreSQL \
  -- --provider PostgreSQL

# SQL Server (production)
dotnet ef database update \
  --startup-project ./src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages \
  --project ./src/Olbrasoft.GitHub.Issues.Migrations.SqlServer \
  -- --provider SqlServer
```

#### Removing Last Migration

```bash
dotnet ef migrations remove \
  --startup-project ./src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages \
  --project ./src/Olbrasoft.GitHub.Issues.Migrations.PostgreSQL \
  -- --provider PostgreSQL
```

## Project Structure

```
GitHub.Issues/
├── src/
│   ├── Olbrasoft.GitHub.Issues.Data/                     # Domain Layer
│   │   ├── Entities/                    # Domain entities
│   │   │   ├── Issue.cs                 # Issue with embedding
│   │   │   ├── Label.cs                 # Repository label
│   │   │   ├── Repository.cs            # GitHub repository
│   │   │   ├── EventType.cs             # Event type enum
│   │   │   └── IssueEvent.cs            # Issue event
│   │   ├── Commands/                    # CQRS Commands
│   │   │   ├── IssueCommands/           # IssueSave, IssueUpdateEmbedding, etc.
│   │   │   ├── LabelCommands/           # LabelSave
│   │   │   ├── RepositoryCommands/      # RepositorySave, UpdateLastSynced
│   │   │   └── EventCommands/           # IssueEventsSaveBatch
│   │   └── Queries/                     # CQRS Queries
│   │       ├── IssueQueries/            # IssueByRepoAndNumber, IssuesByRepository
│   │       ├── LabelQueries/            # LabelByRepoAndName, LabelsByRepository
│   │       ├── RepositoryQueries/       # RepositoryByFullName
│   │       └── EventQueries/            # EventTypesAll, IssueEventIdsByRepository
│   │
│   ├── Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore/ # Infrastructure
│   │   ├── GitHubDbContext.cs           # EF Core DbContext (ONLY here!)
│   │   ├── QueryHandlers/               # Query implementations
│   │   │   ├── IssueQueryHandlers/
│   │   │   ├── LabelQueryHandlers/
│   │   │   ├── RepositoryQueryHandlers/
│   │   │   └── EventQueryHandlers/
│   │   ├── CommandHandlers/             # Command implementations
│   │   │   ├── IssueCommandHandlers/
│   │   │   ├── LabelCommandHandlers/
│   │   │   ├── RepositoryCommandHandlers/
│   │   │   └── EventCommandHandlers/
│   │   └── Services/
│   │       ├── OllamaEmbeddingService.cs   # Embedding generation
│   │       └── SystemdServiceManager.cs    # Service management
│   │
│   ├── Olbrasoft.GitHub.Issues.Migrations.PostgreSQL/    # PostgreSQL Migrations
│   │   └── Migrations/                     # PostgreSQL-specific migrations
│   │
│   ├── Olbrasoft.GitHub.Issues.Migrations.SqlServer/     # SQL Server Migrations
│   │   └── Migrations/                     # SQL Server-specific migrations
│   │
│   ├── Olbrasoft.GitHub.Issues.Business/                 # Business Layer
│   │   ├── Services/
│   │   │   ├── IssueSearchService.cs       # Semantic search
│   │   │   ├── IssueDetailService.cs       # Issue detail with AI summary
│   │   │   ├── IssueSyncBusinessService.cs # Issue sync operations
│   │   │   ├── LabelSyncBusinessService.cs # Label sync operations
│   │   │   ├── RepositorySyncBusinessService.cs
│   │   │   ├── EventSyncBusinessService.cs
│   │   │   ├── GitHubGraphQLClient.cs      # GitHub GraphQL API
│   │   │   ├── AiSummarizationService.cs   # AI summaries (OpenRouter/Ollama)
│   │   │   └── DatabaseStatusService.cs    # DB health checks
│   │   ├── Models/OpenAi/                  # DTO models (SRP extraction)
│   │   │   ├── OpenAiMessage.cs
│   │   │   ├── OpenAiRequest.cs
│   │   │   ├── OpenAiResponse.cs
│   │   │   ├── OpenAiChoice.cs
│   │   │   └── OpenAiJsonContext.cs
│   │   ├── I*Service.cs                    # Service interfaces
│   │   └── *Settings.cs                    # Configuration classes
│   │
│   ├── Olbrasoft.GitHub.Issues.Sync/                     # CLI Sync Tool
│   │   ├── Program.cs                      # Entry point
│   │   ├── ApiClients/
│   │   │   ├── IGitHubIssueApiClient.cs    # Issue API abstraction
│   │   │   ├── GitHubIssueApiClient.cs     # Issue HTTP/JSON client
│   │   │   ├── IGitHubEventApiClient.cs    # Event API abstraction
│   │   │   ├── GitHubEventApiClient.cs     # Event HTTP/JSON client
│   │   │   ├── IGitHubRepositoryApiClient.cs # Repo API abstraction
│   │   │   └── GitHubRepositoryApiClient.cs  # Repo HTTP/JSON client
│   │   └── Services/
│   │       ├── GitHubSyncService.cs        # Orchestrator
│   │       ├── IssueSyncService.cs         # Issue sync (pure orchestrator)
│   │       ├── LabelSyncService.cs         # Label sync (uses Business)
│   │       ├── RepositorySyncService.cs    # Repo sync (pure orchestrator)
│   │       ├── EventSyncService.cs         # Event sync (pure orchestrator)
│   │       └── OctokitGitHubApiClient.cs   # Legacy Octokit wrapper
│   │
│   ├── Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions/ # Abstractions
│   │   ├── Settings/                       # Configuration classes
│   │   │   ├── EmbeddingSettings.cs
│   │   │   ├── TranslationSettings.cs
│   │   │   └── SummarizationSettings.cs
│   │   ├── Results/                        # Result DTOs
│   │   │   ├── TranslationResult.cs
│   │   │   └── SummarizationResult.cs
│   │   ├── I*Service.cs                    # Service interfaces
│   │   └── Enums/                          # Enumerations
│   │
│   ├── Olbrasoft.GitHub.Issues.Text.Transformation.Cohere/  # Cohere Provider
│   │   ├── CohereEmbeddingService.cs       # Cohere embeddings
│   │   └── CohereTranslationService.cs     # Cohere translation
│   │
│   ├── Olbrasoft.GitHub.Issues.Text.Transformation.Ollama/  # Ollama Provider
│   │   └── OllamaEmbeddingService.cs       # Ollama embeddings
│   │
│   ├── Olbrasoft.GitHub.Issues.Text.Transformation.OpenAICompatible/ # OpenAI-compatible
│   │   ├── OpenAICompatibleTranslationService.cs    # Translation
│   │   └── OpenAICompatibleSummarizationService.cs  # Summarization
│   │
│   └── Olbrasoft.GitHub.Issues.AspNetCore.RazorPages/    # Web UI
│       ├── Pages/
│       │   ├── Index.cshtml                # Search page
│       │   └── Index.cshtml.cs             # Page model
│       ├── Extensions/                     # DI extension methods (SRP)
│       │   ├── ServiceCollectionExtensions.cs
│       │   └── ApplicationBuilderExtensions.cs
│       ├── Endpoints/                      # Minimal API endpoints (SRP)
│       │   └── StatusEndpoints.cs
│       ├── Services/
│       │   └── IssueSearchService.cs       # Search service
│       └── Program.cs                      # Entry point (51 lines)
│
├── test/                                    # 393+ Unit Tests
│   ├── Olbrasoft.GitHub.Issues.Data.Tests/
│   ├── Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests/
│   ├── Olbrasoft.GitHub.Issues.Business.Tests/
│   ├── Olbrasoft.GitHub.Issues.Sync.Tests/  # Including API client tests
│   ├── Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Tests/
│   ├── Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions.Tests/  # 26 tests
│   ├── Olbrasoft.GitHub.Issues.Text.Transformation.Cohere.Tests/        # 18 tests
│   ├── Olbrasoft.GitHub.Issues.Text.Transformation.Ollama.Tests/        # 11 tests
│   └── Olbrasoft.GitHub.Issues.Text.Transformation.OpenAICompatible.Tests/ # 16 tests
│
└── GitHub.Issues.sln
```

### Project Dependencies (Clean Architecture Rules)

```
                    ┌───────────────────┐
                    │      Data         │  ← No dependencies (core)
                    │   (Entities,      │
                    │  Commands/Queries)│
                    └─────────┬─────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
┌─────────────────┐  ┌────────────────┐  ┌─────────────────┐
│ Data.EFCore     │  │   Business     │  │   (future       │
│ (DbContext,     │  │  (Services,    │  │    API)         │
│  Handlers)      │  │   IMediator)   │  │                 │
└────────┬────────┘  └───────┬────────┘  └─────────────────┘
         │                   │
         └───────────────────┤
                             ▼
              ┌──────────────────────────┐
              │  Sync / RazorPages       │
              │  (Presentation)          │
              └──────────────────────────┘
```

**Key Rule**: `DbContext` (GitHubDbContext) exists ONLY in `Data.EntityFrameworkCore` project. No other project directly accesses it.

## Requirements

- .NET 10.0+
- SQL Server 2025 (Docker) or PostgreSQL 15+ with pgvector extension
- Cohere API key or Ollama with `nomic-embed-text` model

## Quick Start

### 1. Install Prerequisites

```bash
# SQL Server (Docker - default)
co start mssql

# OR PostgreSQL with pgvector (optional)
sudo apt install postgresql postgresql-contrib
sudo -u postgres psql -c "CREATE EXTENSION IF NOT EXISTS vector;"

# Cohere API key (production)
# Get from https://cohere.com

# OR Ollama (local development)
curl -fsSL https://ollama.com/install.sh | sh
ollama pull nomic-embed-text
```

### 2. Setup Database

```bash
# SQL Server is auto-created by migrations
# Default: Server=localhost,1433; Database=GitHubIssues; User=sa

# OR PostgreSQL (if using)
sudo -u postgres psql
CREATE DATABASE github;
CREATE USER github_user WITH PASSWORD 'your_password';
GRANT ALL PRIVILEGES ON DATABASE github TO github_user;
\c github
CREATE EXTENSION vector;
```

### 3. Configure Credentials

```bash
cd src/Olbrasoft.GitHub.Issues.Sync
dotnet user-secrets init
dotnet user-secrets set "GitHub:Token" "ghp_your_token_here"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost,1433;Database=GitHubIssues;User Id=sa;Password=your_password;TrustServerCertificate=True;Encrypt=True;"
dotnet user-secrets set "Embeddings:ApiKey" "your_cohere_api_key"
```

### 4. Run Migrations & Sync

```bash
cd GitHub.Issues

# Run migrations
dotnet ef database update -p src/Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore

# Sync issues
cd src/Olbrasoft.GitHub.Issues.Sync
dotnet run -- sync --repo Olbrasoft/GitHub.Issues

# Run web UI
cd ../Olbrasoft.GitHub.Issues.AspNetCore.RazorPages
dotnet run
```

## Sync CLI Tool

### Usage

```bash
cd src/Olbrasoft.GitHub.Issues.Sync
dotnet run -- [command] [options]
```

### Commands

| Command | Description |
|---------|-------------|
| `sync` | Full sync of all configured repositories |
| `sync --smart` | Smart sync using stored `last_synced_at` timestamps |
| `sync --repo Owner/Repo` | Sync specific repository |
| `sync --since TIMESTAMP` | Incremental sync (changes since timestamp) |

### Examples

```bash
# Smart incremental sync (recommended)
dotnet run -- sync --smart

# Sync specific repository
dotnet run -- sync --repo Olbrasoft/VirtualAssistant

# Incremental sync since specific date
dotnet run -- sync --since 2025-12-01T00:00:00Z
```

## Testing

The project includes **393+ unit tests** using xUnit and Moq.

```bash
# Run all tests (integration tests skip automatically on CI via [SkipOnCIFact] attribute)
dotnet test --verbosity minimal

# Run specific test project
dotnet test test/Olbrasoft.GitHub.Issues.Business.Tests
```

## CI/CD Pipeline

The project uses GitHub Actions for continuous integration and deployment to Azure.

### Workflow

1. **Trigger**: Push to `main` branch or manual dispatch
2. **Build & Test**: Restore, build, and run all tests
3. **Deploy**: Publish to Azure Web App (only if tests pass)

### Required GitHub Secrets

| Secret | Description |
|--------|-------------|
| `AZURE_WEBAPP_PUBLISH_PROFILE` | Azure Web App publish profile (download from Azure Portal → Web App → Deployment Center → Manage publish profile) |

### Manual Trigger

You can manually trigger deployment via GitHub Actions → "Build and Deploy" → "Run workflow".

## Database Schema

| Entity | Description |
|--------|-------------|
| `Repository` | GitHub repository (owner/name, URL, last_synced_at) |
| `Issue` | Issue with title, state, URL, vector embedding |
| `Label` | Repository labels with colors |
| `IssueLabel` | Many-to-many: Issue ↔ Label |
| `EventType` | Event types (opened, closed, labeled, etc.) |
| `IssueEvent` | Issue events with actor and timestamp |
| `CachedText` | Cached translations (title, summary) with language and text type |

Vector dimensions: **1024** (Cohere - default) or **768** (Ollama - optional)

## CQRS Pattern

### Adding a New Query

1. Create query class in `Data/Queries/`:
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

    public override async Task<MyResult> HandleAsync(MyQuery query, CancellationToken ct)
    {
        return await Context.Entities.Where(...).ToListAsync(ct);
    }
}
```

3. Use in Business service:
```csharp
var query = new MyQuery(Mediator) { Parameter = value };
var result = await query.ToResultAsync(ct);
```

### Adding a New Command

Same pattern as queries, but use `BaseCommand<TResult>` and `GitHubDbCommandHandler`.

## Documentation

See the [Wiki](https://github.com/Olbrasoft/GitHub.Issues/wiki) for detailed documentation:

- [Home](https://github.com/Olbrasoft/GitHub.Issues/wiki) - Overview
- [Architecture](https://github.com/Olbrasoft/GitHub.Issues/wiki/Architecture) - Clean Architecture & CQRS
- [Database Schema](https://github.com/Olbrasoft/GitHub.Issues/wiki/Database-Schema) - Entity relationships
- [Sync Service](https://github.com/Olbrasoft/GitHub.Issues/wiki/Sync-Service) - GitHub sync process
- [Search](https://github.com/Olbrasoft/GitHub.Issues/wiki/Search) - Semantic search
- [Configuration](https://github.com/Olbrasoft/GitHub.Issues/wiki/Configuration) - Setup and configuration

## License

MIT
