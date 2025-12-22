# CLAUDE.md

This file contains **CRITICAL** configuration and constraints for Claude Code when working with this repository.

⚠️ **DO NOT MODIFY** these settings without explicit user approval!

---

## Project: GitHub.Issues Search Application

ASP.NET Core Razor Pages application for searching GitHub issues with semantic search using embeddings.

---

## 🔴🔴🔴 KRITICKÉ VAROVÁNÍ - OPAKUJÍCÍ SE PROBLÉM!!! 🔴🔴🔴

### ⚠️ DATABÁZE: POUZE MICROSOFT SQL SERVER 2025 (DOCKER)!!!

**TENTO PROJEKT POUŽÍVÁ:**
- **Microsoft SQL Server 2025** (Development Edition)
- **Docker kontejner** `mssql` na `localhost:1433`
- **Database:** `GitHubIssues`
- **User:** `sa`
- **Password:** `Tuma/*-+`

**Connection String:**
```
Server=localhost,1433;Database=GitHubIssues;User Id=sa;Password=Tuma/*-+;TrustServerCertificate=True;Encrypt=True;
```

**⛔ NIKDY NEPOUŽÍVAT:**
- ❌ PostgreSQL (ani `localhost:5432`)
- ❌ Azure SQL Server (`olbrasoft-mssql.database.windows.net`)
- ❌ Databázi `github` (je na Azure, ne lokálně!)
- ❌ Databázi `github_issues` (neexistuje!)

**🚨 POKUD DOSTANEŠ CHYBU S DATABÁZÍ:**
1. ZASTAV SE
2. ZKONTROLUJ connection string - MUSÍ být `Server=localhost,1433;Database=GitHubIssues`
3. ZKONTROLUJ Docker kontejner: `docker ps | grep mssql`

---

### ⚠️ TESTOVÁNÍ: Integrační testy se přeskakují automaticky na CI

**Správný příkaz pro testy:**
```bash
dotnet test --verbosity minimal
```

**Jak to funguje:**
- Integrační testy používají `[SkipOnCIFact]` atribut z NuGet package `Olbrasoft.Testing.Xunit.Attributes`
- Atribut **automaticky detekuje CI prostředí** (GitHub Actions, Azure DevOps, atd.)
- Na CI se integrační testy **přeskočí automaticky**
- Lokálně se integrační testy **spustí normálně**

**Proč:** Integrační testy volají externí API (GitHub, Cohere) → nelze spouštět na CI.

**Více informací:** https://github.com/Olbrasoft/Testing

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
dotnet test --verbosity minimal
```
**Note:** Integration tests skip automatically on CI via `[SkipOnCIFact]` attribute

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

## GitHub Actions Self-Hosted Runner

### Runner Configuration
- **Runner Name:** `debian-github-issues`
- **Location:** `~/actions-runner-github-issues/`
- **Service:** `actions.runner.Olbrasoft-GitHub.Issues.debian-github-issues.service`
- **Status Check:** `sudo systemctl status actions.runner.Olbrasoft-GitHub.Issues.debian-github-issues.service`

### ⚠️ CRITICAL: PATH Configuration

**Self-hosted runner MUST have .NET 10 SDK in PATH!**

The runner systemd service MUST include this PATH:
```ini
Environment="PATH=/home/jirka/.dotnet:/home/jirka/.local/bin:/usr/local/bin:/usr/bin:/bin"
```

**Why:** System-wide `dotnet` is .NET SDK 8.0 (in `/usr/share/dotnet/`), but this project requires .NET 10 SDK (in `~/.dotnet/`).

**Location:** `/etc/systemd/system/actions.runner.Olbrasoft-GitHub.Issues.debian-github-issues.service`

**If workflow fails with "NETSDK1045: .NET SDK nepodporuje cílení .NET 10.0":**
1. Edit systemd service file
2. Add PATH environment variable with `~/.dotnet` FIRST
3. Reload: `sudo systemctl daemon-reload`
4. Restart: `sudo systemctl restart actions.runner.Olbrasoft-GitHub.Issues.debian-github-issues.service`

### Workflow File
- **Location:** `.github/workflows/deploy-local.yml`
- **Trigger:** Push to `main` branch
- **Steps:** Build → Test → Deploy → Restart

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

### GitHub Actions workflow fails with "NETSDK1045" error
- **Cause:** Runner using system .NET SDK 8.0 instead of .NET 10
- **Fix:** Check PATH in systemd service (see GitHub Actions Runner section above)
- **Verify:** `sudo systemctl cat actions.runner.Olbrasoft-GitHub.Issues.debian-github-issues.service | grep Environment`

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

## Testing Plan (Post-Deployment)

After deployment, **ALWAYS test the entire application functionality** using Playwright:

### 1. Test GitHub Authentication
- Click "Přihlásit přes GitHub" button
- Verify OAuth flow initializes correctly
- **Tests:** GitHub OAuth authentication handler

### 2. Test Semantic Search (Cohere Embeddings)
- Enter search query (e.g., "deployment")
- Click "Hledat" button
- Verify relevant results appear
- **Tests:** Cohere embeddings, semantic search functionality

### 3. Test Issue Detail View
- Click on one of the found issues
- Verify detail page displays correctly
- **Tests:** Routing, detail view rendering

### 4. Test AI Summary
- On issue detail, check if AI summary is displayed
- **Tests:** OpenAICompatible summarization (Cerebras)

### 5. Test Translation
- Verify title translation to Czech is displayed
- **Tests:** Cohere translation or fallback

### 6. Test Filtering
- Filter by repository name
- Change state to "Zavřený" (Closed)
- **Tests:** Database queries, filtering logic

**CRITICAL:** Application is NOT fully functional until ALL tests pass!

---

## Notes for Claude Code

1. **ALWAYS read this file first** before making changes
2. **NEVER change port 5156** - it's hardcoded in ngrok and multiple places
3. **NEVER change database from SQL Server to PostgreSQL**
4. **NEVER commit connection string passwords** to Git
5. **ALWAYS test after deployment** before marking as complete
6. **CREATE and EXECUTE testing plan** (see Testing Plan section above)
7. **ASK USER** if unsure about any configuration change

---

Last Updated: 2025-12-20
