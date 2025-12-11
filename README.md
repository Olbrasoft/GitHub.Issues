# GitHub.Issues

[![Build and Deploy](https://github.com/Olbrasoft/GitHub.Issues/actions/workflows/deploy.yml/badge.svg)](https://github.com/Olbrasoft/GitHub.Issues/actions/workflows/deploy.yml)

Semantic search for GitHub issues using vector embeddings. Supports **dual embedding providers**: Ollama (local) and Cohere (cloud). Synchronizes issues from multiple GitHub repositories and enables natural language search.

**Live Demo:** https://github-issues.azurewebsites.net

## Features

- **Semantic Search**: Find issues by meaning, not just keywords (using vector embeddings)
- **Dual Embedding Providers**: Ollama (local, 768d) or Cohere (cloud, 1024d)
- **AI Issue Summarization**: Automatic issue summaries using OpenRouter/Ollama with provider rotation
- **Multi-Repository Support**: Sync and search across multiple GitHub repositories
- **Repository Filter**: Filter search results by specific repositories
- **Smart Incremental Sync**: Only sync changed issues using stored timestamps
- **Sub-Issues Hierarchy**: Track parent-child relationships between issues
- **Issue Events**: Track issue lifecycle events (opened, closed, labeled, etc.)
- **Labels Sync**: Full label synchronization with colors
- **Multi-Provider Database**: PostgreSQL (local) or SQL Server (Azure)
- **Clean Architecture**: Layered design with CQRS pattern, 104+ unit tests

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
User Request → Business Service → Command/Query → Handler → DbContext → PostgreSQL
                     ↓
              Uses IMediator.Send()
                     ↓
            Auto-routes to Handler
```

## Multi-Provider Support

The project supports both **PostgreSQL** and **SQL Server** databases with separate migration assemblies.

### Database Providers

| Provider | Use Case | Vector Storage | Migration Project |
|----------|----------|----------------|-------------------|
| PostgreSQL | Development | `vector(768)` (pgvector) | `Migrations.PostgreSQL` |
| SQL Server | Production (Azure) | `varbinary(max)` | `Migrations.SqlServer` |

### Embedding Providers

| Provider | Use Case | Dimensions | Model |
|----------|----------|------------|-------|
| Ollama | Local development | 768 | `nomic-embed-text` |
| Cohere | Azure production | 1024 | `embed-multilingual-v3.0` |

### Configuration

Configure the provider in `appsettings.json`:

```json
{
  "Database": {
    "Provider": "PostgreSQL"  // or "SqlServer"
  },
  "ConnectionStrings": {
    "PostgreSQLConnection": "Host=localhost;Database=github_issues;...",
    "SqlServerConnection": "Server=localhost;Database=GitHubIssues;..."
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
│   └── Olbrasoft.GitHub.Issues.AspNetCore.RazorPages/    # Web UI
│       ├── Pages/
│       │   ├── Index.cshtml                # Search page
│       │   └── Index.cshtml.cs             # Page model
│       ├── Services/
│       │   └── IssueSearchService.cs       # Search service
│       └── Program.cs                      # DI configuration
│
├── test/                                    # 104+ Unit Tests
│   ├── Olbrasoft.GitHub.Issues.Data.Tests/
│   ├── Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests/
│   ├── Olbrasoft.GitHub.Issues.Business.Tests/
│   ├── Olbrasoft.GitHub.Issues.Sync.Tests/  # Including API client tests
│   └── Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Tests/
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
┌─────────────────┐  ┌────────────────┐  ┌─────────────┐
│ Data.EFCore     │  │   Business     │  │   (future   │
│ (DbContext,     │  │  (Services,    │  │    API)     │
│  Handlers)      │  │   IMediator)   │  │             │
└────────┬────────┘  └───────┬────────┘  └─────────────┘
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

- .NET 9.0+
- PostgreSQL 15+ with pgvector extension
- Ollama with `nomic-embed-text` model

## Quick Start

### 1. Install Prerequisites

```bash
# PostgreSQL with pgvector
sudo apt install postgresql postgresql-contrib
sudo -u postgres psql -c "CREATE EXTENSION IF NOT EXISTS vector;"

# Ollama
curl -fsSL https://ollama.com/install.sh | sh
ollama pull nomic-embed-text
```

### 2. Setup Database

```bash
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
  "Host=localhost;Database=github;Username=github_user;Password=your_password"
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

The project includes 104+ unit tests using xUnit and Moq.

```bash
# Run all tests
dotnet test

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
| `Issue` | Issue with title, state, URL, vector embedding (768d) |
| `Label` | Repository labels with colors |
| `IssueLabel` | Many-to-many: Issue ↔ Label |
| `EventType` | Event types (opened, closed, labeled, etc.) |
| `IssueEvent` | Issue events with actor and timestamp |

Vector dimensions: **768** (Ollama/nomic-embed-text) or **1024** (Cohere/embed-multilingual-v3.0)

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
