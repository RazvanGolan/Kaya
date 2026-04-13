# Kaya Developer Tools

[![NuGet](https://img.shields.io/nuget/v/Kaya.ApiExplorer?style=flat&label=Kaya.ApiExplorer&logo=nuget)](https://www.nuget.org/packages/Kaya.ApiExplorer)
[![NuGet](https://img.shields.io/nuget/v/Kaya.GrpcExplorer?style=flat&label=Kaya.GrpcExplorer&logo=nuget)](https://www.nuget.org/packages/Kaya.GrpcExplorer)
[![NuGet](https://img.shields.io/nuget/v/Kaya.McpServer?style=flat&label=Kaya.McpServer&logo=nuget)](https://www.nuget.org/packages/Kaya.McpServer)

A collection of lightweight development tools for .NET applications that provide automatic discovery and interactive testing capabilities.

## Tools

### <img src="src/Kaya.ApiExplorer/UI/ApiExplorer/icon.svg" width="28" height="28" align="center" /> Kaya.ApiExplorer
Swagger-like API documentation tool that automatically scans HTTP endpoints and displays them in an interactive UI.

**Features:**
- Automatic Discovery - Scans controllers and endpoints using reflection
- Interactive UI - Test endpoints directly from the browser with real-time responses
- Authentication - Support for Bearer tokens, API keys, and OAuth 2.0
- SignalR Debugging - Real-time hub testing with method invocation and event monitoring
- XML Documentation - Automatically reads and displays your code comments
- Code Export - Generate request snippets in multiple programming languages
- Performance Metrics - Track request duration and response size

![Kaya API Explorer Demo](demo/kaya-api-demo.gif)

📖 [Full Documentation](src/Kaya.ApiExplorer/README.md)

### <img src="src/Kaya.GrpcExplorer/UI/icon.svg" width="28" height="28" align="center" />  Kaya.GrpcExplorer
gRPC service explorer that uses Server Reflection to discover and test gRPC services.
**Features:**
- Automatic Service Discovery - Uses gRPC Server Reflection to enumerate services and methods
- All RPC Types - Support for Unary, Server Streaming, Client Streaming, and Bidirectional Streaming
- Protobuf Schema - Automatically generates JSON schemas from Protobuf message definitions
- Interactive Testing - Execute gRPC methods with JSON payloads directly from the browser
- Server Configuration - Connect to local or remote gRPC servers with custom metadata
- Authentication - Support for metadata-based authentication (Bearer tokens, API keys)
- Streaming Support - View streaming responses with pagination for large message volumes

![Kaya gRPC Explorer Demo](demo/kaya-grpc-demo.gif)

📖 [Full Documentation](src/Kaya.GrpcExplorer/README.md)

### <img src="src/Kaya.McpServer/UI/icon.svg" width="28" height="28" align="center" /> Kaya.McpServer
MCP stdio server for invoking HTTP APIs, gRPC methods, and SignalR hubs from MCP hosts (Copilot, Cursor, Claude).

**Features:**
- MCP Tool Surface - HTTP API, gRPC method, and SignalR hub invocation tools exposed via MCP
- Host Integration - Works with MCP-capable clients over stdio transport
- Flexible Configuration - Supports command args, env vars, and JSON config file

📖 [Full Documentation](src/Kaya.McpServer/README.md)

## License

This project is licensed under the MIT License - see the LICENSE file for details.