# Database Schema

## Entity Relationship Diagram

```
┌─────────────────┐       ┌─────────────────┐       ┌─────────────────┐
│   Repository    │       │      Issue      │       │     Label       │
├─────────────────┤       ├─────────────────┤       ├─────────────────┤
│ Id (PK)         │◀──┐   │ Id (PK)         │   ┌──▶│ Id (PK)         │
│ GitHubId        │   │   │ RepositoryId(FK)│───┘   │ RepositoryId(FK)│
│ FullName        │   └───│ Number          │       │ Name            │
│ HtmlUrl         │       │ Title           │       │ Color           │
└─────────────────┘       │ IsOpen          │       └─────────────────┘
                          │ Url             │              ▲
                          │ GitHubUpdatedAt │              │
                          │ TitleEmbedding  │       ┌──────┴──────┐
                          │ SyncedAt        │       │ IssueLabel  │
                          │ ParentIssueId   │───┐   ├─────────────┤
                          └────────┬────────┘   │   │ IssueId(FK) │
                                   │            │   │ LabelId(FK) │
                                   │            │   └─────────────┘
                                   │            │          ▲
                                   └────────────┴──────────┘
                                   (self-reference)

┌─────────────────┐       ┌─────────────────┐
│   EventType     │       │   IssueEvent    │
├─────────────────┤       ├─────────────────┤
│ Id (PK)         │◀──────│ Id (PK)         │
│ Name            │       │ GitHubEventId   │
└─────────────────┘       │ IssueId (FK)    │──────▶ Issue
                          │ EventTypeId(FK) │
                          │ CreatedAt       │
                          │ ActorId         │
                          │ ActorLogin      │
                          └─────────────────┘
```

## Entities

### Repository

Represents a GitHub repository.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | int | Primary key |
| `GitHubId` | long | GitHub's repository ID |
| `FullName` | string | Format: `owner/repo` |
| `HtmlUrl` | string | GitHub URL |

```csharp
public class Repository
{
    public int Id { get; set; }
    public long GitHubId { get; set; }
    public string FullName { get; set; }
    public string HtmlUrl { get; set; }

    public ICollection<Issue> Issues { get; set; }
    public ICollection<Label> Labels { get; set; }
}
```

### Issue

Represents a GitHub issue with vector embedding for semantic search.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | int | Primary key |
| `RepositoryId` | int | FK to Repository |
| `Number` | int | GitHub issue number |
| `Title` | string | Issue title |
| `IsOpen` | bool | Open/closed state |
| `Url` | string | GitHub URL |
| `GitHubUpdatedAt` | DateTimeOffset | Last update on GitHub |
| `TitleEmbedding` | vector(768) | Embedding for semantic search |
| `SyncedAt` | DateTimeOffset | Last sync timestamp |
| `ParentIssueId` | int? | FK to parent Issue (self-reference) |

```csharp
public class Issue
{
    public int Id { get; set; }
    public int RepositoryId { get; set; }
    public int Number { get; set; }
    public string Title { get; set; }
    public bool IsOpen { get; set; }
    public string Url { get; set; }
    public DateTimeOffset GitHubUpdatedAt { get; set; }
    public Vector TitleEmbedding { get; set; }
    public DateTimeOffset SyncedAt { get; set; }
    public int? ParentIssueId { get; set; }

    public Repository Repository { get; set; }
    public Issue? ParentIssue { get; set; }
    public ICollection<Issue> SubIssues { get; set; }
    public ICollection<IssueLabel> IssueLabels { get; set; }
    public ICollection<IssueEvent> Events { get; set; }
}
```

### Label

Repository labels with colors.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | int | Primary key |
| `RepositoryId` | int | FK to Repository |
| `Name` | string | Label name |
| `Color` | string | Hex color (without #) |

```csharp
public class Label
{
    public int Id { get; set; }
    public int RepositoryId { get; set; }
    public string Name { get; set; }
    public string Color { get; set; }

    public Repository Repository { get; set; }
    public ICollection<IssueLabel> IssueLabels { get; set; }
}
```

### IssueLabel

Many-to-many relationship between Issues and Labels.

| Column | Type | Description |
|--------|------|-------------|
| `IssueId` | int | FK to Issue (composite PK) |
| `LabelId` | int | FK to Label (composite PK) |

### EventType

Lookup table for GitHub event types.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | int | Primary key |
| `Name` | string | Event type name |

**Seeded Event Types:**
- `opened`, `closed`, `reopened`
- `labeled`, `unlabeled`
- `assigned`, `unassigned`
- `milestoned`, `demilestoned`
- `renamed`, `locked`, `unlocked`
- `transferred`, `pinned`, `unpinned`

### IssueEvent

Individual events on issues.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | int | Primary key |
| `GitHubEventId` | long | GitHub's event ID |
| `IssueId` | int | FK to Issue |
| `EventTypeId` | int | FK to EventType |
| `CreatedAt` | DateTimeOffset | When event occurred |
| `ActorId` | int? | GitHub user ID |
| `ActorLogin` | string? | GitHub username |

## Indexes

### Vector Index

```sql
CREATE INDEX ix_issues_title_embedding
ON issues USING ivfflat (title_embedding vector_cosine_ops)
WITH (lists = 100);
```

### Unique Constraints

- `Repository`: Unique on `FullName`
- `Issue`: Unique on `(RepositoryId, Number)`
- `Label`: Unique on `(RepositoryId, Name)`
- `IssueLabel`: Composite PK on `(IssueId, LabelId)`
- `IssueEvent`: Unique on `GitHubEventId`

## Migrations

| Migration | Description |
|-----------|-------------|
| `InitialCreate` | Base schema with Repository, Issue |
| `AddLabelsAndIssueLabels` | Labels support |
| `AddIssueHierarchy` | ParentIssueId for sub-issues |
| `FixIssueStateAndAddEvents` | EventType, IssueEvent tables |
| `AddLabelColor` | Color column for labels |
| `EmbeddingNotNullAndLabelRepositoryScope` | Required embedding, scoped labels |
| `FixVectorDimension768` | Correct vector dimension for nomic-embed-text |
