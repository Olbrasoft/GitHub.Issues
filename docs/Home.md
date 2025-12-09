# GitHub.Issues Wiki

Welcome to the GitHub.Issues documentation. This project provides semantic search for GitHub issues using vector embeddings.

## Quick Links

- [Architecture](Architecture) - System design and components
- [Database Schema](Database-Schema) - Entity relationships and data model
- [Sync Service](Sync-Service) - GitHub synchronization process
- [Search](Search) - Semantic search functionality
- [Configuration](Configuration) - Setup and configuration options

## Overview

GitHub.Issues is a .NET application that:

1. **Synchronizes** issues from GitHub repositories to a local PostgreSQL database
2. **Generates embeddings** for issue titles using Ollama (nomic-embed-text model)
3. **Enables semantic search** using pgvector's cosine similarity

### Key Features

| Feature | Description |
|---------|-------------|
| Semantic Search | Find issues by meaning, not just keywords |
| Multi-Repository | Sync multiple GitHub repositories |
| Sub-Issues | Track parent-child issue hierarchy |
| Events | Track issue lifecycle (opened, closed, labeled) |
| Labels | Full label sync with colors |
| Auto-Start | Automatically starts Ollama if not running |

### Tech Stack

- **.NET 9.0** - Runtime and framework
- **ASP.NET Core Razor Pages** - Web UI
- **Entity Framework Core** - ORM
- **PostgreSQL + pgvector** - Database with vector search
- **Ollama** - Local embedding generation
- **nomic-embed-text** - Embedding model (768 dimensions)
- **Octokit** - GitHub API client

## Getting Started

See the [README](https://github.com/Olbrasoft/GitHub.Issues#readme) for quick start instructions.

## Projects

| Project | Description |
|---------|-------------|
| `Olbrasoft.GitHub.Issues.Data` | Domain entities |
| `Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore` | DbContext, EF configurations, pgvector |
| `Olbrasoft.GitHub.Issues.Sync` | CLI tool for GitHub sync |
| `Olbrasoft.GitHub.Issues.AspNetCore.RazorPages` | Web UI for search |
