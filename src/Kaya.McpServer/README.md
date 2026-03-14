# Kaya.McpServer

MCP stdio server for invoking HTTP APIs, gRPC methods, and SignalR hubs through Kaya explorers.

Kaya.McpServer relies on the following packages in the target application:
- [Kaya.ApiExplorer documentation](../Kaya.ApiExplorer/README.md) ([NuGet](https://www.nuget.org/packages/Kaya.ApiExplorer))
- [Kaya.GrpcExplorer documentation](../Kaya.GrpcExplorer/README.md) ([NuGet](https://www.nuget.org/packages/Kaya.GrpcExplorer))

Without at least one of these packages configured and running, Kaya.McpServer cannot invoke HTTP/gRPC/SignalR operations.

## Install

```bash
dotnet tool install -g Kaya.McpServer
```

## Configuration

Use environment variables as the default configuration method.

| Variable | Description | Default |
|---|---|---|
| `KAYA_API_BASE_URL` | Base URL for HTTP invocations | `http://localhost:5000` |
| `KAYA_GRPC_PROXY_BASE_URL` | Base URL for Kaya gRPC proxy endpoints | `http://localhost:5000` |
| `KAYA_SIGNALR_DEBUG_ROUTE` | Route prefix for Kaya SignalR debug endpoints | `/kaya-signalr` |
| `KAYA_MCP_CONFIG` | Optional path to JSON config file with URL defaults | not set |

## Host Setup (Copilot / Cursor / Claude)

All MCP hosts need the same stdio command shape:

```json
{
  "command": "kaya-mcp",
  "env": {
    "KAYA_API_BASE_URL": "http://localhost:5121",
    "KAYA_GRPC_PROXY_BASE_URL": "http://localhost:5121",
    "KAYA_SIGNALR_DEBUG_ROUTE": "/kaya-signalr"
  }
}
```

For hosts that use an `mcpServers` map (Cursor, Claude Desktop, and similar), use:

```json
{
  "mcpServers": {
    "kaya": {
      "command": "kaya-mcp",
      "env": {
        "KAYA_API_BASE_URL": "http://localhost:5121",
        "KAYA_GRPC_PROXY_BASE_URL": "http://localhost:5121",
        "KAYA_SIGNALR_DEBUG_ROUTE": "/kaya-signalr"
      }
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
      "command": "kaya-mcp",
      "args": [],
      "env": {
        "KAYA_API_BASE_URL": "http://localhost:5121",
        "KAYA_GRPC_PROXY_BASE_URL": "http://localhost:5121",
        "KAYA_SIGNALR_DEBUG_ROUTE": "/kaya-signalr"
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
      "env": {
        "KAYA_API_BASE_URL": "http://localhost:5121",
        "KAYA_GRPC_PROXY_BASE_URL": "http://localhost:5121",
        "KAYA_SIGNALR_DEBUG_ROUTE": "/kaya-signalr"
      }
    }
  }
}
```

## Install In Claude Code

If your Claude Code version supports CLI registration:

```bash
claude mcp add kaya --env KAYA_API_BASE_URL=http://localhost:5121 --env KAYA_GRPC_PROXY_BASE_URL=http://localhost:5121 -- kaya-mcp
```

Otherwise add the same `mcpServers` JSON entry in Claude MCP configuration.

## Available MCP Tools

- `http_invoke`
- `grpc_invoke`
- `grpc_stream_start`
- `grpc_stream_send`
- `grpc_stream_events`
- `grpc_stream_end`
- `signalr_hubs`
- `signalr_connect`
- `signalr_subscribe`
- `signalr_invoke`
- `signalr_events`
- `signalr_logs`
- `signalr_disconnect`


## Optional JSON Config File

If you prefer file-based configuration instead of env vars:

```json
{
  "apiBaseUrl": "http://localhost:5121",
  "grpcProxyBaseUrl": "http://localhost:5121",
  "signalrDebugRoute": "/kaya-signalr"
}
```

Use it with:

```bash
kaya-mcp --config /absolute/path/to/kaya.mcp.config.json
```

## SignalR Prerequisites (App Side)

To make SignalR MCP tools work, your application needs:

1. SignalR enabled and hubs mapped:

```csharp
builder.Services.AddSignalR();
app.MapHub<NotificationHub>("/hubs/notification");
app.MapHub<ChatHub>("/chat");
```

2. Kaya SignalR debug enabled if you want hub discovery (`signalr_hubs`):

```csharp
builder.Services.AddKayaApiExplorer(options =>
{
  options.SignalRDebug.Enabled = true;
  options.SignalRDebug.RoutePrefix = "/kaya-signalr";
});

app.UseKayaApiExplorer();
```

3. CORS and auth configured for your client scenario.

- For local development and cross-origin clients, allow the required origins/headers.
- For authenticated hubs, pass auth headers in `signalr_connect` (`headers` JSON object), for example Bearer token.
