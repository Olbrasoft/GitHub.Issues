# GitHub.Issues

Semantic search for GitHub issues using vector embeddings (pgvector) and Ollama. Synchronizes issues from multiple GitHub repositories and enables natural language search.

## Features

- **Semantic Search**: Find issues by meaning, not just keywords (using vector embeddings)
- **Multi-Repository Support**: Sync and search across multiple GitHub repositories
- **Smart Incremental Sync**: Only sync changed issues using stored timestamps
- **Sub-Issues Hierarchy**: Track parent-child relationships between issues
- **Issue Events**: Track issue lifecycle events (opened, closed, labeled, etc.)
- **Labels Sync**: Full label synchronization with colors
- **Auto-Start Ollama**: Automatically starts Ollama service if not running

## Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   GitHub API    │────▶│   Sync Tool     │────▶│   PostgreSQL    │
│                 │     │   (CLI app)     │     │   + pgvector    │
└─────────────────┘     └─────────────────┘     └────────┬────────┘
                                                         │
                        ┌─────────────────┐              │
                        │     Ollama      │◀─────────────┤
                        │ (nomic-embed)   │              │
                        └─────────────────┘              │
                                                         ▼
                        ┌─────────────────┐     ┌─────────────────┐
                        │    Web UI       │────▶│  Search Service │
                        │  (Razor Pages)  │     │ (vector search) │
                        └─────────────────┘     └─────────────────┘
```

## Project Structure

```
GitHub.Issues/
├── src/
│   ├── Olbrasoft.GitHub.Issues.Data/                     # Domain entities
│   ├── Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore/ # EF Core, DbContext, Services
│   │   └── Services/
│   │       ├── IEmbeddingService.cs          # Embedding generation interface
│   │       ├── IServiceLifecycleManager.cs   # Service lifecycle (start/stop)
│   │       ├── IProcessRunner.cs             # Process execution abstraction
│   │       ├── IServiceManager.cs            # Systemd service management
│   │       └── OllamaEmbeddingService.cs     # Ollama implementation
│   ├── Olbrasoft.GitHub.Issues.Sync/                     # CLI sync tool
│   │   └── Services/
│   │       ├── IGitHubSyncService.cs         # Orchestrator interface
│   │       ├── IRepositorySyncService.cs     # Repository sync
│   │       ├── ILabelSyncService.cs          # Label sync
│   │       ├── IIssueSyncService.cs          # Issue sync with embeddings
│   │       ├── IEventSyncService.cs          # Event sync
│   │       └── IGitHubApiClient.cs           # GitHub API abstraction
│   └── Olbrasoft.GitHub.Issues.AspNetCore.RazorPages/    # Web UI
│       └── Services/
│           └── IIssueSearchService.cs        # Search interface
├── test/
│   └── Olbrasoft.GitHub.Issues.Tests/        # Unit tests (xUnit + Moq)
├── GitHub.Issues.sln
└── README.md
```

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
# Create database and user
sudo -u postgres psql
CREATE DATABASE github;
CREATE USER github_user WITH PASSWORD 'your_password';
GRANT ALL PRIVILEGES ON DATABASE github TO github_user;
\c github
CREATE EXTENSION vector;
```

### 3. Configure Credentials

See [Credentials Management](#credentials-management) section below.

### 4. Run Migrations & Sync

```bash
cd GitHub.Issues

# Run migrations
dotnet ef database update -p src/Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore

# Sync issues (see Sync CLI section for all options)
cd src/Olbrasoft.GitHub.Issues.Sync
dotnet run -- sync --repo Olbrasoft/GitHub.Issues

# Run web UI
cd ../Olbrasoft.GitHub.Issues.AspNetCore.RazorPages
dotnet run
```

---

## Sync CLI Tool

The sync tool synchronizes GitHub issues to the local PostgreSQL database with vector embeddings.

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
| `sync --repo X --repo Y` | Sync multiple repositories |
| `sync --since TIMESTAMP` | Incremental sync (changes since timestamp) |

### Options

| Option | Description |
|--------|-------------|
| `--repo Owner/Repo` | Target specific repository (can be repeated) |
| `--since TIMESTAMP` | ISO 8601 timestamp for incremental sync |
| `--smart` | Use stored `last_synced_at` from database |

### Examples

```bash
# Full sync of all repositories from config
dotnet run -- sync

# Smart incremental sync (recommended for regular use)
dotnet run -- sync --smart

# Sync specific repository
dotnet run -- sync --repo Olbrasoft/VirtualAssistant

# Sync multiple repositories
dotnet run -- sync --repo Olbrasoft/GitHub.Issues --repo Olbrasoft/Data

# Incremental sync since specific date
dotnet run -- sync --since 2025-12-01T00:00:00Z

# Incremental sync of specific repo
dotnet run -- sync --since 2025-12-01T00:00:00Z --repo Olbrasoft/GitHub.Issues

# Smart sync of specific repo
dotnet run -- sync --smart --repo Olbrasoft/GitHub.Issues
```

### Sync Process

1. **Repository Discovery**: Fetches repository list from config or GitHub API
2. **Labels Sync**: Synchronizes repository labels with colors
3. **Issues Sync**:
   - Fetches issues via GitHub API (with optional `since` filter)
   - Generates vector embeddings using Ollama
   - Stores issues with embeddings in PostgreSQL
4. **Events Sync**: Synchronizes issue events (opened, closed, labeled, etc.)
5. **Parent-Child Links**: Updates sub-issue relationships

---

## Web Application Development

### Project Location

The web application is located at:
```
src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages/
```

### File Structure

```
AspNetCore.RazorPages/
├── Pages/
│   ├── Index.cshtml          # Main search page (Razor view)
│   ├── Index.cshtml.cs       # Page model with search logic
│   ├── Error.cshtml          # Error page
│   └── _Layout.cshtml        # Shared layout
├── Services/
│   ├── IIssueSearchService.cs    # Search interface
│   ├── IssueSearchService.cs     # Vector search implementation
│   └── SearchSettings.cs         # Configurable settings
├── Models/
│   ├── SearchResultPage.cs       # Paginated results
│   └── IssueSearchResult.cs      # Single search result
├── wwwroot/
│   └── css/site.css          # Styles
├── Program.cs                # DI configuration
└── appsettings.json          # Configuration
```

### Running in Debug Mode

```bash
cd src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages

# Run with hot reload
dotnet watch run

# Or standard run
dotnet run
```

The web app will be available at `http://localhost:5000` (or configured port).

### Development Workflow

1. **Make changes** to `.cshtml` or `.cs` files
2. **Hot reload** automatically picks up changes (with `dotnet watch`)
3. **Browser refresh** shows updated content
4. **Check logs** in terminal for errors

### Key Configuration

In `appsettings.json`:
```json
{
  "Search": {
    "DefaultPageSize": 10,
    "PageSizeOptions": [10, 25, 50]
  }
}
```

---

## Credentials Management

### Required Credentials

| Credential | Purpose | Storage |
|------------|---------|---------|
| GitHub Token | API access (higher rate limits, private repos) | User Secrets |
| Database Password | PostgreSQL connection | User Secrets |

### User Secrets Pattern (Recommended)

User Secrets store sensitive data outside the repository, in your user profile.

#### Setup for Sync Tool

```bash
cd src/Olbrasoft.GitHub.Issues.Sync

# Initialize user secrets (if not already done)
dotnet user-secrets init

# Set GitHub token
dotnet user-secrets set "GitHub:Token" "ghp_your_token_here"

# Set database password (optional, can also be in connection string)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=github;Username=github_user;Password=your_password"
```

#### Setup for Web App

```bash
cd src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages

# Initialize user secrets
dotnet user-secrets init

# Set database password
dotnet user-secrets set "DbPassword" "your_password"
```

### Getting a GitHub Token

1. Go to [GitHub Settings > Developer Settings > Personal Access Tokens](https://github.com/settings/tokens)
2. Click "Generate new token (classic)"
3. Select scopes:
   - `repo` - Full control of private repositories (for private repos)
   - `public_repo` - Access public repositories only (for public repos)
4. Generate and copy the token
5. Store using User Secrets (see above)

### Configuration Files

**appsettings.json** (version controlled - NO secrets):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=github;Username=github_user"
  },
  "GitHub": {
    "Token": "",
    "Owner": "Olbrasoft",
    "OwnerType": "user"
  }
}
```

**User Secrets** (not version controlled - secrets go here):
```json
{
  "GitHub:Token": "ghp_xxxxx",
  "ConnectionStrings:DefaultConnection": "Host=localhost;Database=github;Username=github_user;Password=secret"
}
```

### User Secrets Location

- **Linux**: `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`
- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`

The `UserSecretsId` is in each project's `.csproj` file.

---

## Configuration

### Sync Tool Configuration

`src/Olbrasoft.GitHub.Issues.Sync/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=github;Username=github_user"
  },
  "Embeddings": {
    "BaseUrl": "http://localhost:11434",
    "Model": "nomic-embed-text",
    "MaxStartupRetries": 30,
    "StartupRetryDelayMs": 1000
  },
  "GitHub": {
    "Token": "",
    "Owner": "Olbrasoft",
    "OwnerType": "user",
    "IncludeArchived": false,
    "IncludeForks": false,
    "Repositories": []
  },
  "Sync": {
    "GitHubApiPageSize": 100,
    "BatchSaveSize": 100,
    "MaxEmbeddingTextLength": 8000
  }
}
```

### Web App Configuration

`src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=github;Username=github_user"
  },
  "Embeddings": {
    "BaseUrl": "http://localhost:11434",
    "Model": "nomic-embed-text"
  },
  "Search": {
    "DefaultPageSize": 10,
    "PageSizeOptions": [10, 25, 50]
  }
}
```

---

## Testing

The project includes 70+ unit tests using xUnit and Moq.

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~GitHubSyncServiceTests"
```

---

## Database Schema

| Entity | Description |
|--------|-------------|
| `Repository` | GitHub repository (owner/name, URL, last_synced_at) |
| `Issue` | Issue with title, state, URL, vector embedding (768d) |
| `Label` | Repository labels with colors |
| `IssueLabel` | Many-to-many: Issue ↔ Label |
| `EventType` | Event types (opened, closed, labeled, etc.) |
| `IssueEvent` | Issue events with actor and timestamp |

Issues support parent-child hierarchy via `ParentIssueId` self-reference.

Vector dimension: **768** (nomic-embed-text)

---

## Documentation

See the [Wiki](https://github.com/Olbrasoft/GitHub.Issues/wiki) for detailed documentation:

- [Home](https://github.com/Olbrasoft/GitHub.Issues/wiki) - Overview
- [Architecture](https://github.com/Olbrasoft/GitHub.Issues/wiki/Architecture) - System design and SOLID principles
- [Database Schema](https://github.com/Olbrasoft/GitHub.Issues/wiki/Database-Schema) - Entity relationships
- [Sync Service](https://github.com/Olbrasoft/GitHub.Issues/wiki/Sync-Service) - GitHub sync process
- [Search](https://github.com/Olbrasoft/GitHub.Issues/wiki/Search) - Semantic search
- [Configuration](https://github.com/Olbrasoft/GitHub.Issues/wiki/Configuration) - Setup and configuration

---

## License

MIT
