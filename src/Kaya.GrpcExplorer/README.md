# Kaya gRPC Explorer

A browser-based explorer for gRPC services. Uses [gRPC Server Reflection](https://github.com/grpc/grpc/blob/master/doc/server-reflection.md) to discover services at runtime and lets you invoke every RPC type -- Unary, Server Streaming, Client Streaming, and Bidirectional Streaming -- directly from the UI.

> Looking for the HTTP equivalent? See [Kaya.ApiExplorer](../Kaya.ApiExplorer/README.md).

---

## Table of Contents

- [Features](#features)
- [Quick Start](#quick-start)
- [Configuration Reference](#configuration-reference)
- [Recipes](#recipes)
  - [Customise the UI route](#customise-the-ui-route)
  - [Connect to a non-default gRPC endpoint](#connect-to-a-non-default-grpc-endpoint)
  - [Run gRPC over plain HTTP (no TLS)](#run-grpc-over-plain-http-no-tls)
  - [Hide reflection or internal methods](#hide-reflection-or-internal-methods)
  - [Bind from `appsettings.json`](#bind-from-appsettingsjson)
- [How It Works](#how-it-works)
- [Streaming via SSE](#streaming-via-sse)
- [Demo Project](#demo-project)
- [Troubleshooting](#troubleshooting)
- [License](#license)

---

## Features

- **Automatic service discovery** via gRPC Server Reflection -- no `.proto` files to upload.
- **All four RPC types** supported with a single, consistent UI.
- **Real-time streaming** pushed to the browser over Server-Sent Events (SSE) -- no polling.
- **Interactive stream control** -- start, send into, and end client and bidirectional streams.
- **Protobuf-aware schema** -- JSON request and response schemas generated directly from message descriptors.
- **Request history** -- re-run previous calls without retyping payloads.
- **Metadata-based auth** -- Bearer tokens, API keys, custom headers.
- **Configurable server target** -- switch between local and remote gRPC servers from the UI.

---

## Quick Start

### 1. Install

```bash
dotnet add package Kaya.GrpcExplorer
```

### 2. Register and enable

```csharp
using Kaya.GrpcExplorer.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddKayaGrpcExplorer();
}

var app = builder.Build();

app.MapGrpcService<YourGrpcService>();

if (app.Environment.IsDevelopment())
{
    app.UseKayaGrpcExplorer();
}

app.Run();
```

`AddKayaGrpcExplorer()` registers gRPC Server Reflection for you. `UseKayaGrpcExplorer()` maps the reflection endpoint. You do **not** need to call `AddGrpcReflection()` or `MapGrpcReflectionService()` separately.

### 3. Open the UI

Navigate to **`/grpc-explorer`** on your application (for example `https://localhost:5001/grpc-explorer`).

---

## Configuration Reference

All options live under `KayaGrpcExplorerOptions.Middleware`.

| Option | Type | Default | Purpose |
|--------|------|---------|---------|
| `RoutePrefix` | `string` | `"/grpc-explorer"` | URL path where the UI is served. |
| `DefaultServerAddress` | `string` | `"localhost:5001"` | Pre-populates the server-address field in the UI when nothing is stored in the browser. |
| `AllowInsecureConnections` | `bool` | `false` | Permits HTTP/2 cleartext (`h2c`) and skips certificate validation. **Development only.** |
| `ExcludePathPatterns` | `List<string>` | `[]` | Regex patterns (case-insensitive). Methods whose `package.Service/Method` path matches any pattern are hidden from the UI. |

> Theme selection is a per-user UI concern. The theme toggle in the top bar persists the choice in browser `localStorage`.

---

## Recipes

### Customise the UI route

```csharp
builder.Services.AddKayaGrpcExplorer(options =>
{
    options.Middleware.RoutePrefix = "/grpc-ui";
});
```

### Connect to a non-default gRPC endpoint

```csharp
builder.Services.AddKayaGrpcExplorer(options =>
{
    options.Middleware.DefaultServerAddress = "https://api.acme.internal:5443";
});
```

The user can override this from the UI; the configured value is only the initial default when no browser state exists.

### Run gRPC over plain HTTP (no TLS)

gRPC requires HTTP/2. Browsers refuse HTTP/2 cleartext (`h2c`), so a single Kestrel endpoint cannot serve both gRPC traffic and the Kaya UI. The fix is to expose **two endpoints**: HTTP/2 for gRPC, HTTP/1.1 for the browser.

```csharp
using Kaya.GrpcExplorer.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

const int grpcPort   = 5000;
const int kayaUiPort = 5010;

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(grpcPort, o => o.Protocols = HttpProtocols.Http2);

    if (builder.Environment.IsDevelopment())
    {
        options.ListenLocalhost(kayaUiPort, o => o.Protocols = HttpProtocols.Http1);
    }
});

builder.Services.AddGrpc();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddKayaGrpcExplorer(options =>
    {
        options.Middleware.AllowInsecureConnections = true;
        options.Middleware.DefaultServerAddress     = $"localhost:{grpcPort}";
    });
}

var app = builder.Build();

app.MapGrpcService<YourGrpcService>();

if (app.Environment.IsDevelopment())
{
    app.UseKayaGrpcExplorer();
}

app.Run();
```

Then open `http://localhost:5010/grpc-explorer`.

### Hide reflection or internal methods

`ExcludePathPatterns` accepts standard .NET regex syntax. Patterns are matched **case-insensitively** against the method's fully-qualified path in the form `package.Service/Method`.

```csharp
builder.Services.AddKayaGrpcExplorer(options =>
{
    options.Middleware.ExcludePathPatterns =
    [
        @"^grpc\.reflection\.",   // hide reflection service itself
        @"^grpc\.health\.",       // hide standard health-check service
        @"/Internal[A-Z]"         // hide methods starting with "Internal..."
    ];
});
```

Excluded methods are dropped before the UI renders. If every method on a service is excluded, the service still appears (without methods).

### Bind from `appsettings.json`

```jsonc
{
  "KayaGrpcExplorer": {
    "Middleware": {
      "RoutePrefix": "/grpc-explorer",
      "DefaultServerAddress": "localhost:5001",
      "AllowInsecureConnections": false,
      "ExcludePathPatterns": [ "^grpc\\.reflection\\." ]
    }
  }
}
```

```csharp
builder.Services.AddKayaGrpcExplorer(options =>
    builder.Configuration.GetSection("KayaGrpcExplorer").Bind(options));
```

---

## How It Works

1. **Connect** -- opens a gRPC Server Reflection channel to the configured (or user-supplied) address.
2. **List services** -- queries the reflection service for available services.
3. **Fetch descriptors** -- downloads the `FileDescriptorSet` (request file plus its transitive dependencies).
4. **Analyse methods** -- determines RPC type (Unary / Server / Client / Bidi) and resolves request and response message descriptors.
5. **Generate schemas** -- builds JSON schemas and example payloads directly from Protobuf descriptors.
6. **Filter** -- applies `ExcludePathPatterns` so reflection, health, or internal methods never reach the UI.
7. **Serve UI** -- provides the explorer at `{RoutePrefix}` and an SSE channel for streaming RPCs.

Descriptors and method handles are cached per server address; calling `ClearCache(serverAddress)` invalidates the cache for a specific target.

---

## Streaming via SSE

All streaming RPC types are bridged through a small set of HTTP endpoints backed by **Server-Sent Events**.

| Endpoint | Purpose |
|----------|---------|
| `POST /grpc-explorer/stream/start` | Opens a streaming session and returns a `sessionId`. |
| `POST /grpc-explorer/stream/send`  | Sends one message into an active client or bidirectional stream. |
| `POST /grpc-explorer/stream/end`   | Completes the client-side request stream. |
| `GET  /grpc-explorer/stream/events/{sessionId}` | SSE channel pushing `message`, `complete`, and `error` events to the browser. |

Behaviour per RPC type:

- **Server Streaming** -- request sent at `stream/start`; responses arrive over SSE until the server closes the stream.
- **Client Streaming** -- messages sent one at a time via `stream/send`; the server's single response arrives over SSE after `stream/end`.
- **Bidirectional Streaming** -- messages sent via `stream/send` at any time; server responses arrive over SSE concurrently; `stream/end` signals end-of-client.

---

## Demo Project

The repository includes `Demo.GrpcService`, a multi-service application showcasing every RPC pattern.

```bash
# HTTP (h2c) -- gRPC on 5000, UI on 5010
cd src/Demo.GrpcService
dotnet run --launch-profile Demo.GrpcOrdersService.Http
# Open: http://localhost:5010/grpc-explorer

# HTTPS -- both gRPC and UI on 5001
dotnet run --launch-profile Demo.GrpcOrdersService.Https
# Open: https://localhost:5001/grpc-explorer
```

The HTTPS profile uses a self-signed certificate; trust it in your browser or system to avoid warnings.

---

## Troubleshooting

| Symptom | Likely cause / fix |
|--------|--------------------|
| `Error scanning services from <address>` in the server log | Server unreachable, or the target does not have gRPC reflection enabled. `AddKayaGrpcExplorer` registers reflection for the host process -- make sure you are pointing at it. |
| Browser shows `ERR_INVALID_HTTP_RESPONSE` on the UI | gRPC port configured for HTTP/2 only; the browser cannot load HTML over `h2c`. Add a second HTTP/1.1 endpoint (see the recipe above). |
| TLS certificate error during scan | Set `AllowInsecureConnections = true` (development only), or trust the server certificate locally. |
| Reflection service appears in the UI | Add `^grpc\.reflection\.` to `ExcludePathPatterns`. |
| Streaming hangs after `stream/start` | Some reverse proxies buffer SSE. Disable buffering for `/grpc-explorer/stream/events/*` or bypass the proxy in development. |
| Methods missing from a known service | Confirm the proto is published with reflection (default for `Grpc.AspNetCore`), and that `ExcludePathPatterns` is not filtering them out. |

---

## License

MIT -- see the [LICENSE](../../LICENSE) file at the repository root.
