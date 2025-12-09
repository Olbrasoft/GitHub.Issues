# Sync Service

The sync service (`Olbrasoft.GitHub.Issues.Sync`) is a CLI tool that synchronizes GitHub issues to the local database.

## Usage

```bash
# Sync all configured repositories
dotnet run --project src/Olbrasoft.GitHub.Issues.Sync -- sync

# Sync specific repository
dotnet run --project src/Olbrasoft.GitHub.Issues.Sync -- sync owner/repo
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=github;Username=user;Password=xxx"
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

### User Secrets (Recommended for Token)

```bash
cd src/Olbrasoft.GitHub.Issues.Sync
dotnet user-secrets init
dotnet user-secrets set "GitHub:Token" "ghp_your_token_here"
```

## Sync Process

### 1. EnsureRepository

Creates or retrieves the repository record.

```
GitHub API: GET /repos/{owner}/{repo}
Database: INSERT or SELECT Repository
```

### 2. SyncLabels

Synchronizes all labels for the repository.

```
GitHub API: GET /repos/{owner}/{repo}/labels
Database: INSERT/UPDATE Labels
```

### 3. SyncIssues

Bulk fetches all issues with pagination. Parses `parent_issue_url` for sub-issues hierarchy.

```
GitHub API: GET /repos/{owner}/{repo}/issues?state=all&per_page=100
Ollama: POST /api/embeddings (for new issues)
Database: INSERT/UPDATE Issues, IssueLabels
```

**Key Features:**
- Paginates through all issues (100 per page)
- Skips pull requests (have `pull_request` property)
- Extracts `parent_issue_url` for sub-issues
- Generates embeddings only for new issues
- Syncs label associations

### 4. SyncEvents

Bulk fetches all issue events with pagination.

```
GitHub API: GET /repos/{owner}/{repo}/issues/events?per_page=100
Database: INSERT IssueEvents
```

**Key Features:**
- Uses bulk endpoint (not per-issue)
- Links events to issues via `issue.number`
- Skips already-synced events
- Periodic saves (every 100 events)

## API Efficiency

### Before Refactoring (N+1)

```
1 call:  GET /repos/{owner}/{repo}/issues
N calls: GET /repos/{owner}/{repo}/issues/{n}/sub_issues
N calls: GET /repos/{owner}/{repo}/issues/{n}/events
```

### After Refactoring (2 Bulk Calls)

```
1 call (paginated): GET /repos/{owner}/{repo}/issues?state=all
1 call (paginated): GET /repos/{owner}/{repo}/issues/events
```

## Ollama Integration

### Auto-Start

The sync service automatically starts Ollama if not running:

```csharp
public async Task EnsureOllamaRunningAsync()
{
    if (await IsAvailableAsync()) return;

    Process.Start(new ProcessStartInfo
    {
        FileName = "systemctl",
        Arguments = "--user start ollama"
    });

    // Wait up to 30 seconds for Ollama to start
    for (int i = 0; i < 30; i++)
    {
        if (await IsAvailableAsync()) return;
        await Task.Delay(1000);
    }

    throw new InvalidOperationException("Failed to start Ollama");
}
```

### Embedding Generation

```csharp
var request = new { model = "nomic-embed-text", prompt = title };
var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request);
var embedding = response.embedding; // 768 floats
```

## Error Handling

### GitHub API Errors

- **404**: Repository/endpoint not found - skipped
- **403**: Rate limit exceeded - sync fails
- **401**: Invalid token - sync fails

### Ollama Errors

- **Connection refused**: Auto-start attempted
- **Timeout**: Embedding generation fails, issue skipped

## Logging

```
info: Olbrasoft.GitHub.Issues.Sync[0] Starting sync for Olbrasoft/GitHub.Issues
info: Olbrasoft.GitHub.Issues.Sync[0] Found 25 issues to sync
info: Olbrasoft.GitHub.Issues.Sync[0] Updating 3 parent-child relationships
info: Olbrasoft.GitHub.Issues.Sync[0] Found 150 events to process
info: Olbrasoft.GitHub.Issues.Sync[0] Synced 45 new issue events
info: Olbrasoft.GitHub.Issues.Sync[0] Completed sync for Olbrasoft/GitHub.Issues
```

## Scheduled Sync

For automated sync, use systemd timer or cron:

### systemd Timer

```ini
# ~/.config/systemd/user/github-issues-sync.timer
[Unit]
Description=GitHub Issues Sync Timer

[Timer]
OnCalendar=hourly
Persistent=true

[Install]
WantedBy=timers.target
```

```ini
# ~/.config/systemd/user/github-issues-sync.service
[Unit]
Description=GitHub Issues Sync

[Service]
Type=oneshot
WorkingDirectory=/path/to/GitHub.Issues
ExecStart=/usr/bin/dotnet run --project src/Olbrasoft.GitHub.Issues.Sync -- sync
```

### Cron

```bash
# Every hour
0 * * * * cd /path/to/GitHub.Issues && dotnet run --project src/Olbrasoft.GitHub.Issues.Sync -- sync
```
