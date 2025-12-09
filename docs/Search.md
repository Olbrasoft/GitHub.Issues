# Semantic Search

GitHub.Issues uses vector embeddings for semantic search, allowing you to find issues by meaning rather than exact keyword matches.

## How It Works

```
┌────────────────┐     ┌────────────────┐     ┌────────────────┐
│  User Query    │────▶│    Ollama      │────▶│ Query Embedding│
│ "auth bug"     │     │ (embed model)  │     │   [768 floats] │
└────────────────┘     └────────────────┘     └───────┬────────┘
                                                      │
                                                      ▼
┌────────────────┐     ┌────────────────┐     ┌────────────────┐
│ Ranked Results │◀────│  PostgreSQL    │◀────│ Cosine Search  │
│ [Issue1, ...]  │     │   + pgvector   │     │ ORDER BY dist  │
└────────────────┘     └────────────────┘     └────────────────┘
```

### Steps

1. **User enters query**: Natural language search like "authentication bug"
2. **Generate query embedding**: Ollama converts query to 768-dimensional vector
3. **Vector similarity search**: PostgreSQL orders by cosine distance
4. **Return ranked results**: Most similar issues first

## IssueSearchService

```csharp
public class IssueSearchService
{
    public async Task<List<IssueSearchResult>> SearchAsync(
        string query,
        string state = "all",  // "all", "open", "closed"
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        // Generate embedding for query
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

        // Vector similarity search
        var results = await _dbContext.Issues
            .Include(i => i.Repository)
            .Where(StateFilter(state))
            .OrderBy(i => i.TitleEmbedding.CosineDistance(queryEmbedding))
            .Take(limit)
            .Select(i => new IssueSearchResult
            {
                Title = i.Title,
                Url = i.Url,
                IsOpen = i.IsOpen,
                RepositoryName = i.Repository.FullName,
                Similarity = 1 - i.TitleEmbedding.CosineDistance(queryEmbedding)
            })
            .ToListAsync();

        return results;
    }
}
```

## Search Result

```csharp
public class IssueSearchResult
{
    public int Id { get; set; }
    public string Title { get; set; }
    public bool IsOpen { get; set; }
    public string Url { get; set; }
    public string RepositoryName { get; set; }
    public double Similarity { get; set; }  // 0.0 to 1.0
}
```

## Vector Distance Functions

pgvector supports several distance functions:

| Function | SQL | Use Case |
|----------|-----|----------|
| Cosine Distance | `<=>` | Normalized vectors (recommended) |
| L2 Distance | `<->` | Euclidean distance |
| Inner Product | `<#>` | Dot product |

### Cosine Similarity

We use cosine distance because:
- Normalized: Values between 0 and 1
- Direction-based: Ignores vector magnitude
- Best for text embeddings

```sql
-- Cosine distance (lower = more similar)
SELECT title, title_embedding <=> query_embedding AS distance
FROM issues
ORDER BY distance
LIMIT 20;

-- Convert to similarity (higher = more similar)
SELECT title, 1 - (title_embedding <=> query_embedding) AS similarity
FROM issues
ORDER BY similarity DESC
LIMIT 20;
```

## Filtering

### By State

```csharp
// Open issues only
var results = await SearchAsync("bug", state: "open");

// Closed issues only
var results = await SearchAsync("feature", state: "closed");

// All issues
var results = await SearchAsync("performance", state: "all");
```

### By Repository

```csharp
// Add repository filter
var results = await _dbContext.Issues
    .Where(i => i.Repository.FullName == "owner/repo")
    .OrderBy(i => i.TitleEmbedding.CosineDistance(queryEmbedding))
    .Take(20)
    .ToListAsync();
```

## Performance

### Indexing

For large datasets, create a vector index:

```sql
-- IVFFlat index (good for 1K-1M vectors)
CREATE INDEX ix_issues_embedding_ivfflat
ON issues USING ivfflat (title_embedding vector_cosine_ops)
WITH (lists = 100);

-- HNSW index (better recall, more memory)
CREATE INDEX ix_issues_embedding_hnsw
ON issues USING hnsw (title_embedding vector_cosine_ops)
WITH (m = 16, ef_construction = 64);
```

### Query Performance

| Dataset Size | Without Index | IVFFlat | HNSW |
|-------------|---------------|---------|------|
| 1,000 | 5ms | 2ms | 1ms |
| 10,000 | 50ms | 5ms | 2ms |
| 100,000 | 500ms | 20ms | 5ms |

## Embedding Model

### nomic-embed-text

- **Dimensions**: 768
- **Context Length**: 8192 tokens
- **Optimized for**: Semantic similarity, retrieval

### Model Characteristics

| Aspect | Value |
|--------|-------|
| Provider | Nomic AI |
| Size | ~274 MB |
| Speed | ~50ms per embedding |
| Quality | High (comparable to OpenAI) |

### Alternative Models

| Model | Dimensions | Notes |
|-------|------------|-------|
| nomic-embed-text | 768 | **Recommended** |
| mxbai-embed-large | 1024 | Slightly better quality |
| all-minilm | 384 | Faster, lower quality |

## Example Searches

| Query | Finds Issues About |
|-------|-------------------|
| "authentication problem" | Login, OAuth, JWT issues |
| "slow performance" | Optimization, caching, bottlenecks |
| "UI not working" | Frontend bugs, rendering issues |
| "database error" | SQL, EF Core, connection issues |

## Web UI

The Razor Pages UI provides a simple search interface:

```
┌─────────────────────────────────────────────────────────┐
│  GitHub Issues Search                                   │
├─────────────────────────────────────────────────────────┤
│  [___________________________] [Search]                 │
│                                                         │
│  ☐ Open only  ☐ Closed only                            │
│                                                         │
│  Results:                                               │
│  ┌───────────────────────────────────────────────────┐ │
│  │ ✓ Fix authentication timeout #42      (95% match) │ │
│  │   Olbrasoft/GitHub.Issues                         │ │
│  ├───────────────────────────────────────────────────┤ │
│  │ ✓ OAuth login fails on mobile #38     (89% match) │ │
│  │   Olbrasoft/GitHub.Issues                         │ │
│  └───────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```
