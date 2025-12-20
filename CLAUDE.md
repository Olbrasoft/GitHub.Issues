# CLAUDE.md

This file contains **CRITICAL** configuration and constraints for Claude Code when working with this repository.

⚠️ **DO NOT MODIFY** these settings without explicit user approval!

---

## Project: GitHub.Issues Search Application

ASP.NET Core Razor Pages application for searching GitHub issues with semantic search using embeddings.

---

## 🔴 CRITICAL - DO NOT CHANGE

### Port Configuration
- **Application Port:** `5156` (HTTP only)
- **MUST be set via:** `ASPNETCORE_URLS=http://localhost:5156`
- **Why:** ngrok tunnel is configured for port 5156
- **Location:** `/home/jirka/.local/bin/github-start.sh`
- ⚠️ **NEVER change this port** - it breaks ngrok integration

### Database Configuration
- **Database:** Microsoft SQL Server (running in Docker)
- **Container:** `mssql` (managed via `co start/stop`)
- **Connection String Base:** `Server=localhost,1433;Database=GitHubIssues;User Id=sa;TrustServerCertificate=True;Encrypt=True;`
- **Password:** Stored in `github-start.sh` as environment variable `ConnectionStrings__DefaultConnection`
- ⚠️ **NEVER commit password** to Git
- ⚠️ **NEVER use PostgreSQL** for this project (it uses SQL Server)

### Deployment Paths
- **Production Location:** `/opt/olbrasoft/github-issues/`
- **Directory Structure:**
  ```
  /opt/olbrasoft/github-issues/
  ├── app/          # Compiled binaries (from dotnet publish)
  ├── config/       # Configuration files
  ├── data/         # Runtime data
  └── logs/         # Application logs
  ```
- ⚠️ **NEVER change deployment path** - it's the standard for production

### Startup Method
- **Command:** `gi start` (bash alias → calls `~/.local/bin/github-start.sh`)
- **Startup Script:** `/home/jirka/.local/bin/github-start.sh`
- **Method:** Manual startup (NO systemd autostart)
- **Services Started:**
  1. GitHub Actions runners
  2. ngrok tunnel (https://plumbaginous-zoe-unexcusedly.ngrok-free.dev)
  3. ASP.NET application
- ⚠️ **NEVER enable systemd autostart** - we need manual control

---

## Environment Variables (Production)

These **MUST** be set when starting the application:

```bash
ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=GitHubIssues;User Id=sa;Password=<PASSWORD>;TrustServerCertificate=True;Encrypt=True;"
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://localhost:5156
```

⚠️ **launchSettings.json is IGNORED in production** - port must be set via `ASPNETCORE_URLS`

---

## Build & Deploy Commands

### Build
```bash
cd ~/Olbrasoft/GitHub.Issues
dotnet build
```

### Test (MUST pass before deploy)
```bash
dotnet test --verbosity minimal --filter "FullyQualifiedName!~IntegrationTests"
```

### Deploy
```bash
cd ~/Olbrasoft/GitHub.Issues
sudo ./deploy/deploy.sh /opt/olbrasoft/github-issues
```

### Start Application
```bash
gi start
```

### Stop Application
```bash
gi stop
```

---

## Configuration Files

### appsettings.json (in `/opt/olbrasoft/github-issues/config/`)
- Contains public configuration (NO secrets)
- Database provider: `"Provider": "SqlServer"` (NOT PostgreSQL!)
- Ollama settings for embeddings
- Translation/Summarization providers

### appsettings.Production.json (in `/opt/olbrasoft/github-issues/app/`)
- Deployed with binaries
- Contains production-specific settings
- **NO connection string password** (must be in environment variable)

---

## Dependencies

| Service | Location | Purpose |
|---------|----------|---------|
| SQL Server | Docker (localhost:1433) | Main database |
| Ollama | localhost:11434 | Embeddings (nomic-embed-text) |
| ngrok | tunnel to port 5156 | Public access |
| SearXNG | localhost:8888 | Web search |

---

## Common Issues

### Application runs on wrong port (5000 instead of 5156)
- **Cause:** `ASPNETCORE_URLS` not set (uses default port 5000)
- **Fix:** Ensure `github-start.sh` sets `ASPNETCORE_URLS=http://localhost:5156`

### Connection string error / SQL authentication failed
- **Cause:** Password not in environment variable
- **Fix:** Check `ConnectionStrings__DefaultConnection` in startup script

### "Ollama is not a valid value for EmbeddingProvider"
- **Cause:** Wrong config file loaded or config merge issue
- **Fix:** Check `/opt/olbrasoft/github-issues/config/appsettings.json`

---

## 📋 Pre-Deployment Checklist

Before deploying, verify:

- [ ] Tests pass (`dotnet test`)
- [ ] Port is 5156 in startup script
- [ ] SQL Server container is running (`docker ps | grep mssql`)
- [ ] Connection string password is in environment variable (NOT in JSON)
- [ ] Deployment path is `/opt/olbrasoft/github-issues/`
- [ ] No systemd autostart enabled

---

## Git Workflow

- **Branch naming:** `feature/issue-N-desc` or `fix/issue-N-desc`
- **Commits:** Frequent commits after each step
- **Push:** Push frequently to backup work
- **Testing:** MUST pass before merge

---

## Notes for Claude Code

1. **ALWAYS read this file first** before making changes
2. **NEVER change port 5156** - it's hardcoded in ngrok and multiple places
3. **NEVER change database from SQL Server to PostgreSQL**
4. **NEVER commit connection string passwords** to Git
5. **ALWAYS test after deployment** before marking as complete
6. **ASK USER** if unsure about any configuration change

---

Last Updated: 2025-12-20
