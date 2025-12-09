# GitHub.Issues

Semantic search for GitHub issues using vector embeddings (pgvector) and Ollama. Synchronizes issues from multiple GitHub repositories and enables natural language search.

## Features

- **Semantic Search**: Find issues by meaning, not just keywords (using vector embeddings)
- **Multi-Repository Support**: Sync and search across multiple GitHub repositories
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
│   ├── Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore/ # EF Core, DbContext, pgvector
│   ├── Olbrasoft.GitHub.Issues.Sync/                     # CLI sync tool
│   └── Olbrasoft.GitHub.Issues.AspNetCore.RazorPages/    # Web UI
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

### 3. Configure & Run

```bash
# Clone repository
git clone https://github.com/Olbrasoft/GitHub.Issues.git
cd GitHub.Issues

# Set GitHub token (for private repos or higher rate limits)
cd src/Olbrasoft.GitHub.Issues.Sync
dotnet user-secrets set "GitHub:Token" "ghp_your_token_here"

# Update connection string in appsettings.json
# Run migrations
dotnet ef database update -p src/Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore

# Sync issues
dotnet run --project src/Olbrasoft.GitHub.Issues.Sync -- sync Olbrasoft/GitHub.Issues

# Run web UI
dotnet run --project src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages
```

## Usage

### Sync Tool

```bash
# Sync all configured repositories
dotnet run --project src/Olbrasoft.GitHub.Issues.Sync -- sync

# Sync specific repository
dotnet run --project src/Olbrasoft.GitHub.Issues.Sync -- sync owner/repo
```

### Web UI

Open `http://localhost:5000` to search issues using natural language.

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=github;Username=github_user;Password=xxx"
  },
  "Embeddings": {
    "BaseUrl": "http://localhost:11434",
    "Model": "nomic-embed-text"
  },
  "GitHub": {
    "Token": "",
    "Repositories": [
      "Olbrasoft/GitHub.Issues"
    ]
  }
}
```

### User Secrets (recommended for tokens)

```bash
cd src/Olbrasoft.GitHub.Issues.Sync
dotnet user-secrets set "GitHub:Token" "ghp_your_token_here"
```

## Database Schema

| Entity | Description |
|--------|-------------|
| `Repository` | GitHub repository (owner/name, URL) |
| `Issue` | Issue with title, state, URL, vector embedding |
| `Label` | Repository labels with colors |
| `IssueLabel` | Many-to-many: Issue ↔ Label |
| `EventType` | Event types (opened, closed, labeled, etc.) |
| `IssueEvent` | Issue events with actor and timestamp |

Issues support parent-child hierarchy via `ParentIssueId` self-reference.

## API Endpoints

The sync service uses GitHub REST API v3:

- `GET /repos/{owner}/{repo}/issues?state=all` - Bulk fetch all issues
- `GET /repos/{owner}/{repo}/issues/events` - Bulk fetch all events
- `GET /repos/{owner}/{repo}/labels` - Fetch labels

Vector dimension: **768** (nomic-embed-text)

## Documentation

See the [Wiki](https://github.com/Olbrasoft/GitHub.Issues/wiki) for detailed documentation:

- [Home](https://github.com/Olbrasoft/GitHub.Issues/wiki) - Overview
- [Architecture](https://github.com/Olbrasoft/GitHub.Issues/wiki/Architecture) - System design
- [Database Schema](https://github.com/Olbrasoft/GitHub.Issues/wiki/Database-Schema) - Entity relationships
- [Sync Service](https://github.com/Olbrasoft/GitHub.Issues/wiki/Sync-Service) - GitHub sync process
- [Search](https://github.com/Olbrasoft/GitHub.Issues/wiki/Search) - Semantic search
- [Configuration](https://github.com/Olbrasoft/GitHub.Issues/wiki/Configuration) - Setup and configuration

## License

MIT
