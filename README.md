# GitHub.Issues

GitHub issues management with semantic search - ASP.NET Core application.

## Project Structure

```
GitHub.Issues/
├── src/
│   ├── Olbrasoft.GitHub.Issues.AspNetCore.Mvc/    # Web UI (Razor Pages/MVC)
│   ├── Olbrasoft.GitHub.Issues.Data/              # Entities
│   ├── Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore/  # DbContext, QueryHandlers
│   └── Olbrasoft.GitHub.Issues.Business/          # Services
├── test/
│   └── Olbrasoft.GitHub.Issues.AspNetCore.Mvc.Tests/
├── GitHub.Issues.sln
└── README.md
```

## Features

- Semantic search for GitHub issues using vector embeddings (pgvector)
- Czech language support
- REST API for OpenCode/Claude integration
- MCP plugin for AI assistants

## Requirements

- .NET 8.0+
- PostgreSQL with pgvector extension
- Ollama with nomic-embed-text model (for embeddings)

## Getting Started

```bash
# Clone repository
git clone https://github.com/Olbrasoft/GitHub.Issues.git

# Restore packages
dotnet restore

# Run migrations
dotnet ef database update -p src/Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore

# Run application
dotnet run --project src/Olbrasoft.GitHub.Issues.AspNetCore.Mvc
```

## License

MIT
