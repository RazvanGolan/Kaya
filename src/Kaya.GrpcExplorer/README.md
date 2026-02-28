# Kaya gRPC Explorer

A gRPC service explorer that uses Server Reflection to discover and test gRPC services with support for all four RPC types (Unary, Server Streaming, Client Streaming, Bidirectional Streaming).

## Features

- **Automatic Service Discovery** - Uses gRPC Server Reflection to enumerate services and methods
- **All RPC Types** - Full support for Unary, Server Streaming, Client Streaming, and Bidirectional Streaming
- **Real-Time Streaming via SSE** - Streaming responses are pushed to the browser in real time using Server-Sent Events (SSE); no polling required
- **Interactive Stream Control** - Start, send messages into, and end client/bidirectional streams directly from the UI
- **Protobuf Schema** - Automatically generates JSON schemas from Protobuf message definitions
- **Interactive Testing** - Execute gRPC methods with JSON payloads directly from the browser
- **Server Configuration** - Connect to local or remote gRPC servers with custom metadata
- **Authentication** - Support for metadata-based authentication (Bearer tokens, API keys)

## Quick Start

### 1. Install the Package

```bash
dotnet add package Kaya.GrpcExplorer
```

### 2. Configure Your gRPC Application

Add Kaya gRPC Explorer to your `Program.cs`:

```csharp
using Kaya.GrpcExplorer.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add gRPC services
builder.Services.AddGrpc();

// Add Kaya gRPC Explorer (automatically registers gRPC reflection)
// No need to call AddGrpcReflection() - Kaya handles it!
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddKayaGrpcExplorer();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Enable Kaya gRPC Explorer UI (automatically maps gRPC reflection endpoint)
    // No need to call MapGrpcReflectionService() - Kaya handles it!
    app.UseKayaGrpcExplorer();
}

// Map your gRPC services
app.MapGrpcService<YourGrpcService>();

app.Run();
```

### 3. Access the UI

Navigate to `https://localhost:5000/grpc-explorer` (or your app's URL) to explore your gRPC services.

> **ℹ️ Note**: Kaya automatically registers gRPC Server Reflection when you call `AddKayaGrpcExplorer()` and maps the reflection endpoint when you call `UseKayaGrpcExplorer()`.

## How It Works

Kaya gRPC Explorer uses the gRPC Server Reflection API to discover services at runtime:

1. **Connects to Server**: Uses gRPC Server Reflection client to connect to the configured server
2. **Lists Services**: Queries the reflection service for all available gRPC services
3. **Retrieves Descriptors**: Downloads FileDescriptorSet containing Protobuf schemas
4. **Analyzes Methods**: Examines each service method to determine RPC type and message schemas
5. **Generates Schemas**: Creates JSON schemas from Protobuf MessageDescriptor definitions
6. **Serves UI**: Provides a web interface to explore services and invoke methods
## Service Information Captured

For each gRPC method, it captures:

- **RPC Type** (Unary, Server Streaming, Client Streaming, Bidirectional Streaming)
- **Full Method Name** and Description
- **Request and Response Message Schemas** with field types and descriptions
- **Example JSON payloads** for testing
- **Server metadata** requirements

## Configuration

### Basic Configuration

```csharp
// Use default settings
builder.Services.AddKayaGrpcExplorer();
```

### Advanced Configuration

```csharp
builder.Services.AddKayaGrpcExplorer(options =>
{
    options.Middleware.RoutePrefix = "/grpc-explorer";
    options.Middleware.DefaultTheme = "dark";
    options.Middleware.DefaultServerAddress = "https://localhost:5000";
    options.Middleware.RequestTimeoutSeconds = 30;
});
```

**Configuration Options:**
- `RoutePrefix`: URL path for the explorer UI (default: `/grpc-explorer`)
- `DefaultTheme`: UI theme - `"light"` or `"dark"` (default: `"light"`)
- `DefaultServerAddress`: Default gRPC server to connect to
- `AllowInsecureConnections`: Bypass certificate validation for HTTPS (default: `false`)
- `RequestTimeoutSeconds`: Timeout for gRPC requests (default: 30)

### HTTP-Only Endpoints (No TLS)

If your gRPC service runs over **plain HTTP without TLS** you need this special configuration because:

1. **gRPC requires HTTP/2**, but Kestrel's `Http1AndHttp2` mode without TLS **falls back to HTTP/1.1 only** (no ALPN negotiation available).
2. **Browsers don't support HTTP/2 cleartext (h2c)**, so the Kaya UI page can't be served over an HTTP/2-only endpoint.

The solution is to configure **two endpoints**: HTTP/2 for gRPC traffic and HTTP/1.1 for the Kaya browser UI.

```csharp
using Kaya.GrpcExplorer.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

const int grpcPort = 5000;
const int kayaUiPort = 5010;

builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP/2 cleartext (h2c) required for gRPC without TLS
    options.ListenLocalhost(grpcPort, o => o.Protocols = HttpProtocols.Http2);

    // HTTP/1.1 for browser access to Kaya UI (development only)
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
        // Point to the HTTP/2 gRPC port, not the browser UI port
        options.Middleware.DefaultServerAddress = $"localhost:{grpcPort}";
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

Then open `http://localhost:5010/grpc-explorer` in your browser.

### Streaming via SSE

For all streaming RPC types the UI communicates with the explorer backend over a set of lightweight HTTP endpoints backed by **Server-Sent Events (SSE)**:

| Endpoint | Purpose |
|---|---|
| `POST /grpc-explorer/stream/start` | Opens a streaming session and returns a `sessionId` |
| `POST /grpc-explorer/stream/send` | Sends one message into an active client/bidirectional stream |
| `POST /grpc-explorer/stream/end` | Completes the client-side request stream |
| `GET /grpc-explorer/stream/events/{sessionId}` | SSE channel — pushes `message`, `complete`, and `error` events to the browser in real time |

Each streaming method type works as follows:

- **Server Streaming** — the initial request is sent at `stream/start`; responses are streamed back via SSE until the server closes the stream.
- **Client Streaming** — messages are sent one at a time via `stream/send`; the server's single response is pushed over SSE after `stream/end` completes the request stream.
- **Bidirectional Streaming** — messages are sent via `stream/send` at any time while server responses arrive over SSE concurrently; `stream/end` signals the end of client messages.

## Demo Project

This repository includes a comprehensive demo gRPC service showcasing all four RPC types across three different services.

### Demo.GrpcService

A multi-service gRPC application with examples for all RPC patterns.

**Running the Demo:**

```bash
cd src/Demo.GrpcService
# For HTTP (plain-text gRPC)
dotnet run --launch-profile Demo.GrpcOrdersService.Http
# Then open: http://localhost:5010/grpc-explorer

# For HTTPS (TLS gRPC)
dotnet run --launch-profile Demo.GrpcOrdersService.Https  
# Then open: https://localhost:5001/grpc-explorer
```

> **Note on HTTPS**: If you use the HTTPS launch profile, the server uses a self-signed certificate. You'll need to trust it in your browser or system to avoid certificate warnings.

> **Note**: When running with the HTTP profile, gRPC traffic runs on port 5000 (HTTP/2) and the Kaya UI is served on port 5010 (HTTP/1.1). With HTTPS, both run on port 5001.

## Embedded UI Architecture

The UI is built with embedded HTML, CSS, and JavaScript files that are compiled into the assembly. This ensures:
- **Reliable deployment**: No external file dependencies
- **Fast loading**: Resources are served from memory
- **Consistent experience**: UI works the same across all environments

The middleware integrates seamlessly into your ASP.NET Core pipeline, serving the gRPC Explorer at your specified route without any external dependencies or separate processes.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
