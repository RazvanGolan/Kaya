# Kaya.McpServer

MCP stdio server for invoking HTTP APIs and gRPC methods through Kaya explorers.

## Install

```bash
dotnet tool install -g Kaya.McpServer
```

This package exposes the command:

```bash
kaya-mcp --help
```

## Configuration

Environment variables:

| Variable | Description | Default |
|---|---|---|
| `KAYA_API_BASE_URL` | Base URL for HTTP invocations | `http://localhost:5000` |
| `KAYA_GRPC_PROXY_BASE_URL` | Base URL for Kaya gRPC proxy endpoints | `http://localhost:5000` |
| `KAYA_MCP_CONFIG` | Optional path to JSON config file with URL defaults | not set |

Command-line overrides:

```bash
kaya-mcp --api-url http://localhost:5121 --grpc-proxy-url http://localhost:5121
```

Config file format (`kaya.mcp.config.json`):

```json
{
  "apiBaseUrl": "http://localhost:5121",
  "grpcProxyBaseUrl": "http://localhost:5121"
}
```

## Host Setup (Copilot / Cursor / Claude)

All MCP hosts need the same stdio command shape:

```json
{
  "command": "kaya-mcp",
  "args": [
    "--config",
    "/absolute/path/to/kaya.mcp.config.json"
  ]
}
```

For hosts that use an `mcpServers` map (Cursor, Claude Desktop, and similar), use:

```json
{
  "mcpServers": {
    "kaya": {
      "command": "kaya-mcp",
      "args": [
        "--config",
        "/absolute/path/to/kaya.mcp.config.json"
      ]
    }
  }
}
```

## Install In VS Code GitHub Copilot

1. Install the tool globally (when published):

```bash
dotnet tool install -g Kaya.McpServer
```

2. If not yet published, use dotnet project execution in workspace `mcp.json`.

3. In VS Code, use workspace MCP config at `.vscode/mcp.json` (included in this repo), or run `MCP: Add Server`.

4. Reload VS Code window and trust/start the `kaya` server when prompted.

5. In chat, open tools and verify `kaya` tools are available.

Workspace `mcp.json` used in this repo (no external config file):

```json
{
  "servers": {
    "kaya": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "src/Kaya.McpServer",
        "--"
      ],
      "env": {
        "KAYA_API_BASE_URL": "http://localhost:5121",
        "KAYA_GRPC_PROXY_BASE_URL": "http://localhost:5121"
      }
    }
  }
}
```

## Install In Cursor

Add this server in Cursor MCP settings:

```json
{
  "mcpServers": {
    "kaya": {
      "command": "kaya-mcp",
      "args": [
        "--config",
        "/absolute/path/to/kaya.mcp.config.json"
      ]
    }
  }
}
```

## Install In Claude Code

If your Claude Code version supports CLI registration:

```bash
claude mcp add kaya -- kaya-mcp --config /absolute/path/to/kaya.mcp.config.json
```

Otherwise add the same `mcpServers` JSON entry in Claude MCP configuration.

## Available MCP Tools

- `http_invoke`
- `grpc_invoke`
- `grpc_stream_start`
- `grpc_stream_send`
- `grpc_stream_events`
- `grpc_stream_end`

Kaya invocation requires the target application to have `Kaya.ApiExplorer` and `Kaya.GrpcExplorer` middleware configured and running.
