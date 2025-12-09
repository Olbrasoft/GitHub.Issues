# Configuration

## Overview

GitHub.Issues uses standard .NET configuration with:
- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- User Secrets - Sensitive data (tokens, passwords)
- Environment variables - Production overrides

## Configuration Sections

### ConnectionStrings

PostgreSQL connection string with pgvector support.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=github;Username=github_user;Password=your_password"
  }
}
```

| Parameter | Description | Default |
|-----------|-------------|---------|
| `Host` | PostgreSQL server | localhost |
| `Port` | PostgreSQL port | 5432 |
| `Database` | Database name | github |
| `Username` | Database user | - |
| `Password` | Database password | - |

### Embeddings

Ollama configuration for vector embeddings.

```json
{
  "Embeddings": {
    "BaseUrl": "http://localhost:11434",
    "Model": "nomic-embed-text"
  }
}
```

| Parameter | Description | Default |
|-----------|-------------|---------|
| `BaseUrl` | Ollama API URL | http://localhost:11434 |
| `Model` | Embedding model | nomic-embed-text |

### GitHub

GitHub API configuration (Sync tool only).

```json
{
  "GitHub": {
    "Token": "",
    "Repositories": [
      "owner/repo1",
      "owner/repo2"
    ]
  }
}
```

| Parameter | Description | Default |
|-----------|-------------|---------|
| `Token` | GitHub personal access token | "" (anonymous) |
| `Repositories` | List of repos to sync | [] |

### Logging

Standard .NET logging configuration.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

## User Secrets

**Recommended** for sensitive data like tokens and passwords.

### Setup

```bash
# Initialize user secrets
cd src/Olbrasoft.GitHub.Issues.Sync
dotnet user-secrets init

# Set GitHub token
dotnet user-secrets set "GitHub:Token" "ghp_your_token_here"

# Set connection string
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Password=..."
```

### List Secrets

```bash
dotnet user-secrets list
```

### Remove Secrets

```bash
dotnet user-secrets remove "GitHub:Token"
```

## Environment Variables

Override configuration via environment variables:

```bash
# Connection string
export ConnectionStrings__DefaultConnection="Host=localhost;Database=github;..."

# GitHub token
export GitHub__Token="ghp_xxx"

# Embeddings
export Embeddings__BaseUrl="http://localhost:11434"
export Embeddings__Model="nomic-embed-text"
```

## Project-Specific Configuration

### Sync Tool

`src/Olbrasoft.GitHub.Issues.Sync/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=github;..."
  },
  "Embeddings": {
    "BaseUrl": "http://localhost:11434",
    "Model": "nomic-embed-text"
  },
  "GitHub": {
    "Token": "",
    "Repositories": ["Olbrasoft/GitHub.Issues"]
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

### Web UI

`src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=github;..."
  },
  "Embeddings": {
    "BaseUrl": "http://localhost:11434",
    "Model": "nomic-embed-text"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

## GitHub Token

### When Needed

| Scenario | Token Required |
|----------|---------------|
| Public repos (< 60 req/hour) | No |
| Public repos (> 60 req/hour) | Yes |
| Private repos | Yes |
| Organization repos | Yes (with access) |

### Creating a Token

1. Go to GitHub Settings → Developer settings → Personal access tokens
2. Generate new token (classic)
3. Select scopes:
   - `repo` - Full access to private repos
   - `public_repo` - Public repos only (recommended)
4. Copy token and store securely

### Rate Limits

| Authentication | Rate Limit |
|---------------|------------|
| Anonymous | 60 requests/hour |
| With Token | 5,000 requests/hour |

## Database Setup

### PostgreSQL Installation

```bash
# Debian/Ubuntu
sudo apt install postgresql postgresql-contrib

# Create extension
sudo -u postgres psql -c "CREATE EXTENSION IF NOT EXISTS vector;"
```

### Create Database

```sql
CREATE DATABASE github;
CREATE USER github_user WITH PASSWORD 'secure_password';
GRANT ALL PRIVILEGES ON DATABASE github TO github_user;

\c github
CREATE EXTENSION vector;
GRANT ALL ON SCHEMA public TO github_user;
```

### Run Migrations

```bash
cd GitHub.Issues
dotnet ef database update -p src/Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore
```

## Ollama Setup

### Installation

```bash
curl -fsSL https://ollama.com/install.sh | sh
```

### Pull Model

```bash
ollama pull nomic-embed-text
```

### Verify

```bash
# Check if running
curl http://localhost:11434/api/tags

# Test embedding
curl http://localhost:11434/api/embeddings -d '{
  "model": "nomic-embed-text",
  "prompt": "test"
}'
```

### Systemd Service

Ollama typically runs as a systemd service:

```bash
# Check status
systemctl --user status ollama

# Start/stop
systemctl --user start ollama
systemctl --user stop ollama

# Enable on boot
systemctl --user enable ollama
```
