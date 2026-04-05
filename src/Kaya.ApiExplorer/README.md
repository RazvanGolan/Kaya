# Kaya API Explorer

A lightweight, Swagger-like API documentation tool for .NET applications that automatically scans your HTTP endpoints and displays them in a beautiful, interactive UI.

## Features

- **Automatic Discovery** - Scans controllers, Minimal API endpoints, and routes using reflection
- **Interactive UI** - Test endpoints directly from the browser with real-time responses
- **Authentication** - Support for Bearer tokens, API keys, OAuth 2.0, and cookies
- **SignalR Debugging** - Real-time hub testing with method invocation and event monitoring
- **XML Documentation** - Automatically reads and displays your code comments
- **Code Export** - Generate request snippets in multiple programming languages
- **Performance Metrics** - Track request duration and response size
- **Request History** - Save and reload previous requests from the UI for faster retesting
- **Data Annotations** - Validation constraints from model attributes surfaced in the UI
- **Multiple Response Codes** - Full `[ProducesResponseType]` support with per-status-code schemas

## Quick Start

### 1. Install the Package

```bash
dotnet add package Kaya.ApiExplorer
```

### 2. Configure Your Application

Add Kaya API Explorer to your `Program.cs`:

```csharp
using Kaya.ApiExplorer.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddKayaApiExplorer(); 

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseKayaApiExplorer();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### 3. Access the UI

Navigate to `http://localhost:5000/kaya` (or your app's URL) to view your API documentation.

## Demo Project

This repository includes a demo project (`Demo.WebApi`) that showcases the API Explorer with sample endpoints for users and products.

To run the demo:

```bash
cd src/Demo.WebApi
dotnet run
```

Then navigate to `http://localhost:5121/kaya` to see the API Explorer in action.

## How It Works

Kaya API Explorer uses .NET reflection to scan your application's endpoints at runtime. It:

1. **Discovers Endpoints**: Finds all `ControllerBase` controllers and Minimal API routes (`MapGet`, `MapPost`, etc.)
2. **Analyzes Actions**: Examines public methods, their HTTP attributes, and `[ProducesResponseType]` declarations
3. **Extracts Metadata**: Gathers parameters, return types, routing, and Data Annotations validation constraints
4. **Generates Documentation**: Creates a fully valid OpenAPI 3.0 JSON representation of your API
5. **Serves UI**: Provides a beautiful web interface to explore the documentation and interact with the endpoints

## API Information Captured

For each endpoint, Kaya captures:

- **HTTP Method** (GET, POST, PUT, DELETE, etc.)
- **Route Path** with parameters
- **Controller and Action Names** (or delegate name for Minimal APIs)
- **Parameters** with types, sources (query, body, route, header, cookie), and requirements
- **Data Annotation Constraints** (`[Required]`, `[Range]`, `[StringLength]`, `[EmailAddress]`, etc.)
- **Response Types** and descriptions per status code
- **Status Codes** declared via `[ProducesResponseType]`

## Configuration

You can customize Kaya API Explorer in several ways:

### Basic Configuration

```csharp
// Use default settings (route: "/kaya", theme: "light")
builder.Services.AddKayaApiExplorer();

// Customize route prefix and theme
builder.Services.AddKayaApiExplorer(routePrefix: "/api-explorer", defaultTheme: "dark");
```

### Documentation Metadata

Configure `options.Documentation` to customise the metadata shown in the UI and in the exported OpenAPI spec. All fields are optional.

```csharp
builder.Services.AddKayaApiExplorer(options =>
{
    options.Documentation.Title = "Acme Orders API";
    options.Documentation.Version = "v3";
    options.Documentation.Description = "Manages orders, line items, and fulfilment workflows.";
    options.Documentation.TermsOfService = "https://acme.com/terms";

    options.Documentation.Contact = new ContactOptions
    {
        Name = "Acme API Support",
        Email = "api@acme.com",
        Url = "https://acme.com/support"
    };

    options.Documentation.License = new LicenseOptions
    {
        Name = "MIT",
        Url = "https://opensource.org/licenses/MIT"
    };

    options.Documentation.Servers =
    [
        new ServerOptions { Url = "https://api.acme.com/v3", Description = "Production" },
        new ServerOptions { Url = "http://localhost:5000",   Description = "Local" }
    ];
});
```

These values are written directly into the OpenAPI `info`, `contact`, `license`, `termsOfService`, and `servers` objects, producing a fully valid OpenAPI 3.0 spec.

### Advanced Configuration with SignalR Debugging

```csharp
using Kaya.ApiExplorer.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(); // If using SignalR

builder.Services.AddKayaApiExplorer(options =>
{
    options.Middleware.RoutePrefix = "/kaya";
    options.Middleware.DefaultTheme = "light";
    
    // Enable SignalR debugging (optional)
    options.SignalRDebug.Enabled = true;
    options.SignalRDebug.RoutePrefix = "/signalr-debug";
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseKayaApiExplorer();
}

// Map your SignalR hubs

app.Run();
```

### SignalR Debugging

The SignalR Debug Tool provides:
- **Hub Connection Management**: Connect/disconnect from SignalR hubs with authentication support
- **Method Invocation**: Execute hub methods with parameters and see real-time responses
- **Event Handlers**: Register custom event handlers to receive server-sent messages
- **Real-time Logging**: Monitor all hub activity including connections, method calls, and incoming events
- **Interactive Testing**: Test your SignalR implementation without writing client code

### XML Documentation Support

Kaya API Explorer automatically reads XML documentation comments from your code to provide better descriptions in the UI. To enable this feature, add the following to your project file (`.csproj`):

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

If XML documentation is not available, the explorer falls back to generating default descriptions.

### Embedded UI Architecture

The UI is built with embedded HTML, CSS, and JavaScript files that are compiled into the assembly. This ensures:
- **Reliable deployment**: No external file dependencies
- **Fast loading**: Resources are served from memory
- **Consistent experience**: UI works the same across all environments

The middleware integrates seamlessly into your ASP.NET Core pipeline, serving the API Explorer at your specified route without any external dependencies or separate processes.

## License

This project is licensed under the MIT License - see the LICENSE file for details.