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

## Available MCP Tools

- `http_invoke`
- `grpc_invoke`
- `grpc_stream_start`
- `grpc_stream_send`
- `grpc_stream_events`
- `grpc_stream_end`

Kaya invocation requires the target application to have `Kaya.ApiExplorer` and `Kaya.GrpcExplorer` middleware configured and running.
