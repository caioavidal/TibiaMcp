# 🎮 TibiaMcp — MCP Server for Tibia

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16+-336791?logo=postgresql)](https://www.postgresql.org/)
[![MCP](https://img.shields.io/badge/MCP-Model%20Context%20Protocol-6C47FF)](https://modelcontextprotocol.io)

**TibiaMcp** is a [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server that exposes Tibia game data through AI-friendly tools. It automatically crawls the [Tibia Fandom Wiki](https://tibia.fandom.com) and provides structured access to conditions, players, and guilds.

AI assistants like Claude, ChatGPT, and others can use MCP tools to query Tibia data in real time — no scraping needed.

---

## ✨ Features

- **🧠 MCP Tools** — Query players, guilds, and special conditions via standardized MCP tools
- **🕷️ Wiki Crawler** — Automatically scrapes the Tibia Wiki for special conditions with sections
- **🗄️ PostgreSQL Storage** — Persists data with Entity Framework Core and efficient upserts
- **🔄 Auto-Migration** — Database schema is created automatically on startup
- **⚡ Async First** — Fully asynchronous, non-blocking architecture
- **🧩 Extensible** — Easy to add new crawlers or MCP tools

---

## 📋 Available MCP Tools

### Players

| Tool | Description |
|------|-------------|
| `getPlayers` | List players with optional filters (vocation, minLevel, isOnline) |
| `getPlayerById` | Get a player by ID |
| `getPlayerByName` | Get a player by name |
| `createPlayer` | Create a new player |
| `updatePlayerLevel` | Update a player's level and experience |
| `setPlayerOnlineStatus` | Set a player's online status |
| `deletePlayer` | Delete a player by ID |

### Guilds

| Tool | Description |
|------|-------------|
| `getAllGuilds` | Get all guilds with their owners |
| `getGuildById` | Get a guild by ID with members |
| `createGuild` | Create a new guild with an owner |
| `addGuildMember` | Add a member to a guild |
| `removeGuildMember` | Remove a member from a guild |
| `deleteGuild` | Delete a guild and its members |

### Conditions

| Tool | Description |
|------|-------------|
| `getConditions` | Get all conditions, optionally filtered by type or name search |
| `getConditionById` | Get a condition by ID with its sections |
| `getConditionByName` | Get a condition by name with its sections |
| `runConditionCrawler` | Manually trigger a full re-crawl of the Special Conditions wiki |

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     AI Assistant (Claude, etc.)              │
└─────────────────────────┬───────────────────────────────────┘
                          │  MCP Protocol (Streamable HTTP)
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                   TibiaMcp MCP Server                        │
│  ┌───────────────────────────────────────────────────────┐   │
│  │  Tools Layer (PlayerTools, GuildTools, ConditionTools) │   │
│  ├───────────────────────────────────────────────────────┤   │
│  │  Crawler Layer (ConditionCrawler, CrawlerRunner)       │   │
│  ├───────────────────────────────────────────────────────┤   │
│  │  Data Layer (AppDbContext, EF Core + Npgsql)           │   │
│  └───────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                          │
                          ▼
              ┌──────────────────────┐
              │    PostgreSQL DB     │
              └──────────────────────┘
```

### Tech Stack

- **.NET 10** — ASP.NET Core Minimal API
- **ModelContextProtocol** — [MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk) for .NET
- **HtmlAgilityPack** — HTML parsing for wiki crawling
- **Entity Framework Core** — ORM with PostgreSQL provider
- **Npgsql** — PostgreSQL driver

---

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL](https://www.postgresql.org/download/) 16 or later

### 1. Clone & Setup

```bash
git clone https://github.com/your-username/TibiaMcp.git
cd TibiaMcp
```

### 2. Configure the Database

Edit `appsettings.json` with your PostgreSQL connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=tibiamcp;Username=postgres;Password=your-password"
  }
}
```

### 3. Create the Database

```bash
createdb tibiamcp
```

Or via `psql`:

```sql
CREATE DATABASE tibiamcp;
```

### 4. Run the Server

```bash
dotnet run
```

The server will:
- ✅ Auto-create database tables
- 🕷️ Start crawling the Tibia Wiki for special conditions
- 🔌 Expose the MCP endpoint at `http://localhost:5000/mcp`

### 5. Health Check

```bash
curl http://localhost:5000/
```

Expected response:

```json
{
  "service": "TibiaMcp MCP Server",
  "status": "running",
  "mcpEndpoint": "/mcp"
}
```

---

## 🔌 Connecting an AI Assistant

### Claude Desktop

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "tibia-mcp": {
      "type": "http",
      "url": "http://localhost:5000/mcp"
    }
  }
}
```

### Other MCP Clients

Connect to the Streamable HTTP endpoint: `http://localhost:5000/mcp`

---

## 🧪 Development

### Project Structure

```
TibiaMcp/
├── Crawlers/           # Wiki scraping logic
│   ├── CrawlerBase.cs         # Abstract base class (HTTP, HTML helpers)
│   ├── ConditionCrawler.cs    # Special Conditions crawler
│   └── CrawlerRunner.cs       # Orchestrator + persistence
├── Data/
│   └── AppDbContext.cs        # EF Core DbContext
├── Models/
│   ├── Player.cs              # Player entity
│   ├── Guild.cs               # Guild entity
│   ├── GuildMember.cs         # Guild membership entity
│   ├── Condition.cs           # Condition entity
│   └── ConditionSection.cs    # Condition section entity
├── Tools/
│   ├── PlayerTools.cs         # Player MCP tools
│   ├── GuildTools.cs          # Guild MCP tools
│   └── ConditionTools.cs      # Condition MCP tools
├── Program.cs                 # Entry point, DI, MCP config
├── appsettings.json           # Configuration
└── TibiaMcp.Server.csproj    # Project file
```

### Adding a New Tool

1. Create a class decorated with `[McpServerToolType]`
2. Add methods decorated with `[McpServerTool]`
3. Register it in `Program.cs` with `.WithTools<YourToolClass>()`

Example:

```csharp
[McpServerToolType]
public class MyTools
{
    [McpServerTool]
    public string Hello(string name) => $"Hello, {name}!";
}
```

### Adding a New Crawler

1. Create a class inheriting from `CrawlerBase`
2. Use `FetchPageAsync()` and the HTML helpers for parsing
3. Register the crawler in DI in `Program.cs`

---

## ⚙️ Configuration

All configuration is in `appsettings.json`:

| Key | Description | Default |
|-----|-------------|---------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string | — |
| `Logging:LogLevel:Default` | Default log level | `Information` |

### Environment Variables

Override settings via environment variables:

```bash
export ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=..."
```

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- [Tibia Fandom Wiki](https://tibia.fandom.com) — the amazing community-driven wiki
- [Model Context Protocol](https://modelcontextprotocol.io) — the MCP specification and .NET SDK
- [CipSoft](https://www.tibia.com) — the creators of Tibia

---

> **Disclaimer:** TibiaMcp is an independent open-source project and is not affiliated with CipSoft GmbH. All game data is sourced from the publicly available Tibia Fandom Wiki under fair use.
