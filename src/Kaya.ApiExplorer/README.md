# Kaya API Explorer

A lightweight, Swagger-like API documentation tool for ASP.NET Core. Drops into your application as a single middleware, discovers every controller and Minimal API endpoint at runtime, and serves an interactive UI plus a valid OpenAPI 3.0 export.

> Looking for the gRPC equivalent? See [Kaya.GrpcExplorer](../Kaya.GrpcExplorer/README.md).

---

## Table of Contents

- [Features](#features)
- [Quick Start](#quick-start)
- [Configuration Reference](#configuration-reference)
- [Recipes](#recipes)
  - [Customise the UI route](#customise-the-ui-route)
  - [Enrich the OpenAPI metadata](#enrich-the-openapi-metadata)
  - [Hide internal endpoints](#hide-internal-endpoints)
  - [Enable SignalR debugging](#enable-signalr-debugging)
  - [Bind from `appsettings.json`](#bind-from-appsettingsjson)
- [How It Works](#how-it-works)
- [Captured Endpoint Metadata](#captured-endpoint-metadata)
- [XML Documentation](#xml-documentation)
- [Troubleshooting](#troubleshooting)
- [License](#license)

---

## Features

- **Automatic discovery** -- controllers, Minimal API endpoints, and route attributes scanned via reflection.
- **Interactive UI** -- invoke endpoints from the browser with real-time responses.
- **Authentication** -- Bearer tokens, API keys, OAuth 2.0, and cookies.
- **SignalR debugging** -- a separate UI for live hub testing and event monitoring.
- **XML doc comments** -- surfaced alongside parameters and responses.
- **OpenAPI 3.0 export** -- a valid spec served at `{routePrefix}/openapi.json`.
- **Code snippets** -- copy-ready request examples in multiple languages.
- **Request history** -- replay previous calls without retyping payloads.
- **Data annotations** -- `[Required]`, `[Range]`, `[StringLength]`, etc., shown in the UI.

---

## Quick Start

### 1. Install

```bash
dotnet add package Kaya.ApiExplorer
```

### 2. Register and enable

```csharp
using Kaya.ApiExplorer.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddKayaApiExplorer();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseKayaApiExplorer();
}

app.MapControllers();
app.Run();
```

### 3. Open the UI

Navigate to **`/kaya`** on your application (for example `http://localhost:5000/kaya`).

---

## Configuration Reference

All options live under `KayaApiExplorerOptions`.

### `Middleware`

| Option | Type | Default | Purpose |
|--------|------|---------|---------|
| `RoutePrefix` | `string` | `"/kaya"` | URL path where the UI is served. |
| `ExcludePathPatterns` | `List<string>` | `[]` | Regex patterns (case-insensitive). Endpoints whose path matches any pattern are skipped during scanning. |

### `SignalRDebug`

| Option | Type | Default | Purpose |
|--------|------|---------|---------|
| `Enabled` | `bool` | `false` | Turns on the SignalR debugging UI and route. |
| `RoutePrefix` | `string` | `"/kaya-signalr"` | URL path where the SignalR debugger is served. |

### `Documentation`

Used by the UI header **and** the exported OpenAPI spec.

| Option | Type | Default | Purpose |
|--------|------|---------|---------|
| `Title` | `string` | `"API Documentation"` | OpenAPI `info.title`. |
| `Version` | `string` | `"1.0.0"` | OpenAPI `info.version`. |
| `Description` | `string` | `""` | OpenAPI `info.description`. |
| `TermsOfService` | `string?` | `null` | OpenAPI `info.termsOfService`. |
| `Contact` | `ContactOptions?` | `null` | OpenAPI `info.contact` (`Name`, `Email`, `Url`). |
| `License` | `LicenseOptions?` | `null` | OpenAPI `info.license` (`Name`, `Url`). |
| `Servers` | `List<ServerOptions>` | `[]` | OpenAPI `servers` array (`Url`, `Description`). |

> Theme selection is a per-user UI concern. The theme toggle in the top bar persists the choice in browser `localStorage`.

---

## Recipes

### Customise the UI route

```csharp
builder.Services.AddKayaApiExplorer(routePrefix: "/api-explorer");
```

### Enrich the OpenAPI metadata

```csharp
builder.Services.AddKayaApiExplorer(options =>
{
    options.Documentation.Title          = "Acme Orders API";
    options.Documentation.Version        = "v3";
    options.Documentation.Description    = "Manages orders, line items, and fulfilment workflows.";
    options.Documentation.TermsOfService = "https://acme.com/terms";

    options.Documentation.Contact = new ContactOptions
    {
        Name  = "Acme API Support",
        Email = "api@acme.com",
        Url   = "https://acme.com/support"
    };

    options.Documentation.License = new LicenseOptions
    {
        Name = "MIT",
        Url  = "https://opensource.org/licenses/MIT"
    };

    options.Documentation.Servers =
    [
        new ServerOptions { Url = "https://api.acme.com/v3", Description = "Production" },
        new ServerOptions { Url = "http://localhost:5000",   Description = "Local"      }
    ];
});
```

The exported spec at `/{routePrefix}/openapi.json` includes a full, valid OpenAPI 3.0 `info` object.

### Hide internal endpoints

`ExcludePathPatterns` accepts standard .NET regex syntax. Patterns are matched **case-insensitively** against the endpoint's route path (always prefixed with `/`).

```csharp
builder.Services.AddKayaApiExplorer(options =>
{
    options.Middleware.ExcludePathPatterns =
    [
        @"^/health$",       // exact match
        @"^/metrics",       // prefix match
        @"^/internal/",     // entire subtree
        @"/_diagnostics/"   // anywhere in the path
    ];
});
```

When all endpoints of a controller match, the controller itself is omitted from the UI.

### Enable SignalR debugging

```csharp
builder.Services.AddSignalR();

builder.Services.AddKayaApiExplorer(options =>
{
    options.SignalRDebug.Enabled     = true;
    options.SignalRDebug.RoutePrefix = "/kaya-signalr";
});
```

Open the SignalR debugger at `/kaya-signalr`. From the UI you can connect to hubs, invoke methods, and register handlers for server-sent events.

### Bind from `appsettings.json`

```jsonc
{
  "KayaApiExplorer": {
    "Middleware": {
      "RoutePrefix": "/kaya",
      "ExcludePathPatterns": [ "^/health$", "^/internal/" ]
    },
    "SignalRDebug": {
      "Enabled": true,
      "RoutePrefix": "/kaya-signalr"
    },
    "Documentation": {
      "Title": "Acme Orders API",
      "Version": "v3"
    }
  }
}
```

```csharp
builder.Services.AddKayaApiExplorer(options =>
    builder.Configuration.GetSection("KayaApiExplorer").Bind(options));
```

---

## How It Works

1. **Discovery** -- scans loaded assemblies for `ControllerBase` subclasses and inspects the registered `EndpointDataSource` for Minimal API routes.
2. **Analysis** -- reads HTTP method attributes, `[ProducesResponseType]`, route templates (including constraints such as `{id:Guid}`), and binding attributes.
3. **Metadata** -- combines parameter types, sources, validation attributes, and XML doc comments.
4. **Filtering** -- applies `ExcludePathPatterns` so internal endpoints never reach the UI or the OpenAPI export.
5. **UI & spec** -- serves an interactive HTML UI at `{RoutePrefix}` and a valid OpenAPI 3.0 document at `{RoutePrefix}/openapi.json`.

---

## Captured Endpoint Metadata

For each endpoint, the scanner captures:

- HTTP method (GET, POST, PUT, DELETE, PATCH, ...).
- Route path with parameter names (constraints like `:Guid` are normalised away).
- Controller and action name, or the delegate name for Minimal APIs.
- Parameters with type, source (`Route`, `Query`, `Body`, `Header`, `Form`, `File`), and required flag.
- Data annotation constraints (`[Required]`, `[Range]`, `[StringLength]`, `[EmailAddress]`, ...).
- Response types and descriptions per status code via `[ProducesResponseType]`.
- Authorization requirements (`[Authorize]`, roles, `[AllowAnonymous]`).
- Obsolete flag and message via `[Obsolete]`.

---

## XML Documentation

To surface `<summary>`, `<param>`, `<returns>`, and `<response>` comments in the UI, enable XML documentation generation in your project:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

If the XML file is absent, the explorer falls back to generated default descriptions.

---

## Troubleshooting

| Symptom | Likely cause / fix |
|--------|--------------------|
| UI loads but no endpoints appear | The middleware is registered only inside `IsDevelopment()` -- confirm the environment, or move `UseKayaApiExplorer()` outside the check. |
| Endpoint appears with `{id:Guid}` in the path | Upgrade to a version that strips route constraints during scanning. The path should display as `{id}` regardless of the constraint. |
| Internal endpoints leak into the UI | Add a regex to `Middleware.ExcludePathPatterns`. |
| OpenAPI export missing `contact` / `license` | Populate `Documentation.Contact` and `Documentation.License` -- they are omitted from the spec when null. |
| Parameter descriptions are blank | Enable `<GenerateDocumentationFile>` in the controller project so the XML file ships alongside the assembly. |
| SignalR UI returns 404 | `SignalRDebug.Enabled` defaults to `false`. Set it to `true`. |

---

## License

MIT -- see the [LICENSE](../../LICENSE) file at the repository root.
