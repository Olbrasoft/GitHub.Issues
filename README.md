# GitHub.Issues

GitHub issues management with semantic search - ASP.NET Core Razor Pages application.

## Project Structure

```
GitHub.Issues/
├── src/
│   ├── Olbrasoft.GitHub.Issues.AspNetCore.RazorPages/    # Web UI (Razor Pages)
│   ├── Olbrasoft.GitHub.Issues.Data/                      # Entities
│   ├── Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore/  # DbContext, Configurations
│   └── Olbrasoft.GitHub.Issues.Business/                  # Services
├── test/
│   └── Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Tests/
├── plugins/
│   └── mcp-github-issues/                                 # MCP plugin for OpenCode
├── GitHub.Issues.sln
└── README.md
```

## Features

- Semantic search for GitHub issues using vector embeddings (pgvector)
- Czech language UI
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
dotnet run --project src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages
```

## Namespaces

Following Microsoft naming conventions:

| Project | Namespace |
|---------|-----------|
| Web UI | `Olbrasoft.GitHub.Issues.AspNetCore.RazorPages` |
| Entities | `Olbrasoft.GitHub.Issues.Data` |
| EF Core | `Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore` |
| Services | `Olbrasoft.GitHub.Issues.Business` |

## License

MIT
