# 🎮 TibiaMcp — MCP Server for Tibia

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-Model%20Context%20Protocol-6C47FF)](https://modelcontextprotocol.io)

**TibiaMcp** is a [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server that exposes Tibia game data through AI-friendly tools. It fetches live data from the [Tibia Fandom Wiki](https://tibia.fandom.com) and other sources, providing structured access to game information.

AI assistants like Claude, ChatGPT, and others can use MCP tools to query Tibia data in real time — no scraping needed.

> 📌 **Currently available:** Special Conditions tools. More tools (creatures, spells, items, etc.) are planned.

---

## ✨ Features

- **🧠 MCP Tools** — Query and search game data via standardized MCP tools
- **🌐 Live Wiki Fetching** — Pulls data directly from the Tibia Fandom Wiki via the MediaWiki API
- **🛡️ Cloudflare Bypass** — Automatically handles Fandom's Cloudflare anti-bot protection by capturing the `__cf_bm` cookie from challenge responses and reusing it on the API endpoint
- **💾 In-Memory Cache** — Results are cached for 2 hours (wiki content is very stable), so repeated queries are instant
- **🧩 Section Extraction** — Parses detail pages into structured heading/content sections
- **⚡ Async First** — Fully asynchronous, non-blocking architecture
- **🧩 Extensible** — Easy to add new tools as the project grows

---

## 📋 Available MCP Tools

### Conditions

| Tool | Description |
|------|-------------|
| `getConditions` | List all special conditions with optional filters (`type`, `search`) |
| `getConditionByName` | Get a single condition by name with its detailed description and sections |

**`getConditions`** returns the full listing with condition name, type (Harmful, Positive, Neutral, Mixed, Taints), and short effect description. Supports filtering by `type` or `search` (name substring).

**`getConditionByName`** returns a single condition with its detailed description and extracted sections (Effect, Notes, History, etc.).

### Spells

| Tool | Description |
|------|-------------|
| `getSpells` | List all instant spells with optional filters (`search`, `group`, `vocation`) |
| `getSpellByName` | Get a single spell by name with full infobox data and sections |
| `getSpellByWords` | Find a spell by its magic words (e.g., `"exori gran ico"`) |

**`getSpells`** returns the full instant-spell listing with name, words, level, mana, group, premium status, and effect. Filter by `search` (name or words), `group` (Attack, Healing, Support, etc.), or `vocation` (Knight, Sorcerer, Druid, Paladin).

**`getSpellByName`** returns detailed infobox properties (magic type, cooldown, base power, version, status), plus the introductory description and sections.

**`getSpellByWords`** looks up a spell by its cast command — useful when you see a spell being used and want to identify it.

---

### Tech Stack

- **.NET 10** — ASP.NET Core Minimal API
- **ModelContextProtocol** — [MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk) for .NET
- **HtmlAgilityPack** — HTML parsing for wiki page content extraction

---

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### 1. Clone & Run

```bash
git clone https://github.com/your-username/TibiaMcp.git
cd TibiaMcp
dotnet run
```

The server will start at `http://localhost:5000`.

### 2. Health Check

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

### 3. Test the Tools

```bash
# List all conditions
curl -s -X POST "http://localhost:5000/mcp" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
      "name": "getConditions",
      "arguments": {}
    },
    "id": 1
  }' | jq .

# Get a specific condition
curl -s -X POST "http://localhost:5000/mcp" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
      "name": "getConditionByName",
      "arguments": { "name": "Haste" }
    },
    "id": 1
  }' | jq .

# List all spells
curl -s -X POST "http://localhost:5000/mcp" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
      "name": "getSpells",
      "arguments": {}
    },
    "id": 1
  }' | jq .

# Get a spell by its magic words
curl -s -X POST "http://localhost:5000/mcp" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
      "name": "getSpellByWords",
      "arguments": { "words": "exori gran ico" }
    },
    "id": 1
  }' | jq .
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
├── Models/
│   ├── Condition.cs              # Condition entity
│   └── ConditionSection.cs       # Condition section entity
├── Services/
│   └── ConditionWikiService.cs   # Wiki fetching, parsing, caching
├── Tools/
│   └── ConditionTools.cs         # MCP tool definitions
├── Program.cs                    # Entry point, DI, MCP config
├── appsettings.json              # Configuration
└── TibiaMcp.Server.csproj       # Project file
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

---

## ⚙️ Configuration

All configuration is in `appsettings.json`:

| Key | Description | Default |
|-----|-------------|---------|
| `Logging:LogLevel:Default` | Default log level | `Information` |

No database is required — all data is fetched live from the wiki and cached in memory.

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
