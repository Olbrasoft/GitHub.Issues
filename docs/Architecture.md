# Architecture

## System Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              GitHub.Issues                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐                  │
│  │  GitHub API  │───▶│  Sync Tool   │───▶│  PostgreSQL  │                  │
│  │  (REST v3)   │    │    (CLI)     │    │  + pgvector  │                  │
│  └──────────────┘    └──────┬───────┘    └───────┬──────┘                  │
│                             │                    │                          │
│                             ▼                    │                          │
│                      ┌──────────────┐            │                          │
│                      │    Ollama    │            │                          │
│                      │ (embeddings) │            │                          │
│                      └──────────────┘            │                          │
│                                                  │                          │
│  ┌──────────────┐    ┌──────────────┐           │                          │
│  │    User      │───▶│   Web UI     │◀──────────┘                          │
│  │  (Browser)   │    │(Razor Pages) │                                      │
│  └──────────────┘    └──────────────┘                                      │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Components

### 1. Sync Tool (`Olbrasoft.GitHub.Issues.Sync`)

CLI application that synchronizes GitHub data to local database.

**Responsibilities:**
- Fetch issues from GitHub API (bulk endpoint with pagination)
- Fetch issue events (bulk endpoint)
- Fetch labels
- Generate embeddings via Ollama
- Store data in PostgreSQL

**Sync Process:**
```
1. EnsureRepository     → Create/update repository record
2. SyncLabels           → Sync all labels for repository
3. SyncIssues           → Bulk fetch issues, parse parent_issue_url, generate embeddings
4. SyncEvents           → Bulk fetch events, link to issues
```

### 2. Web UI (`Olbrasoft.GitHub.Issues.AspNetCore.RazorPages`)

ASP.NET Core Razor Pages application for semantic search.

**Responsibilities:**
- Accept search queries from users
- Generate query embeddings via Ollama
- Perform vector similarity search
- Display ranked results

### 3. Data Layer (`Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore`)

Entity Framework Core with PostgreSQL and pgvector.

**Key Services:**
- `GitHubDbContext` - Database context
- `OllamaEmbeddingService` - Embedding generation
- `IssueSearchService` - Vector similarity search

### 4. Ollama Integration

Local LLM service for embedding generation.

**Model:** `nomic-embed-text` (768 dimensions)

**Features:**
- Auto-start via `systemctl --user start ollama`
- Health check via `/api/tags`
- Embedding generation via `/api/embeddings`

## Data Flow

### Sync Flow

```
GitHub API                    Sync Tool                    Database
    │                            │                            │
    │  GET /issues?state=all     │                            │
    │◀───────────────────────────│                            │
    │  [{number, title, ...}]    │                            │
    │───────────────────────────▶│                            │
    │                            │  Generate embedding        │
    │                            │─────────────────────────▶  │
    │                            │  (Ollama)                  │
    │                            │◀─────────────────────────  │
    │                            │                            │
    │                            │  INSERT/UPDATE Issue       │
    │                            │───────────────────────────▶│
    │                            │                            │
```

### Search Flow

```
User                     Web UI                   Database
  │                         │                         │
  │  "authentication bug"   │                         │
  │────────────────────────▶│                         │
  │                         │  Generate embedding     │
  │                         │────────────────────────▶│
  │                         │  (Ollama)               │
  │                         │                         │
  │                         │  SELECT ... ORDER BY    │
  │                         │  cosine_distance(...)   │
  │                         │────────────────────────▶│
  │                         │                         │
  │                         │  [Issue1, Issue2, ...]  │
  │                         │◀────────────────────────│
  │  Ranked results         │                         │
  │◀────────────────────────│                         │
```

## API Calls

### GitHub API (Bulk Endpoints)

The sync service uses only 2 main bulk API calls per repository:

| Endpoint | Description |
|----------|-------------|
| `GET /repos/{owner}/{repo}/issues?state=all&per_page=100` | All issues with pagination |
| `GET /repos/{owner}/{repo}/issues/events?per_page=100` | All events with pagination |
| `GET /repos/{owner}/{repo}/labels` | All labels |

### Ollama API

| Endpoint | Description |
|----------|-------------|
| `GET /api/tags` | Health check |
| `POST /api/embeddings` | Generate embedding for text |

## Performance Considerations

### Bulk API Strategy

Previous implementation used N+1 queries:
- 1 call for issues list
- N calls for sub-issues (per issue)
- N calls for events (per issue)

Current implementation uses bulk endpoints:
- 1 paginated call for all issues (with `parent_issue_url`)
- 1 paginated call for all events

### Vector Search

- Uses pgvector's `CosineDistance` function
- Indexed via IVFFlat or HNSW for large datasets
- Query embedding generated once per search
