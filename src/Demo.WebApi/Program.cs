using Demo.WebApi.Authentication;
using Demo.WebApi.Hubs;
using Kaya.ApiExplorer.Extensions;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add OpenAPI services
builder.Services.AddOpenApi();

// Add SignalR
builder.Services.AddSignalR(options => options.EnableDetailedErrors = true);

// Add SignalR services
builder.Services.AddSingleton<StockTickerService>();

// Configure CORS to allow any origin for SignalR
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(origin => true); // allow any external site
    });
});

// Add mock authentication that allows all requests and assigns roles
builder.Services.AddAuthentication("MockAuth")
    .AddScheme<AuthenticationSchemeOptions, MockAuthenticationHandler>("MockAuth", null);

builder.Services.AddAuthorization();

// Add Kaya API Explorer with SignalR debugging enabled
builder.Services.AddKayaApiExplorer(options =>
{
    options.Middleware.RoutePrefix = "/kaya";
    options.Middleware.DefaultTheme = "light";
    options.SignalRDebug.Enabled = true;
    options.SignalRDebug.RoutePrefix = "/kaya-signalr";
});

// Alternative: Simple configuration (SignalR debug disabled by default)
// builder.Services.AddKayaApiExplorer(routePrefix: "/api-explorer", defaultTheme: "dark");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseKayaApiExplorer();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hubs
app.MapHub<NotificationHub>("/hubs/notification");
app.MapHub<ChatHub>("/chat");
app.MapHub<StockTickerHub>("/stockticker");

app.MapControllers();

// Map OpenAPI endpoint
app.MapOpenApi();

// --- Minimal API example ---
var todosGroup = app.MapGroup("/api/todos").WithTags("Todos");

todosGroup.MapGet("/", () => Results.Ok(new[]
{
    new { Id = 1, Title = "Learn Kaya", Done = false },
    new { Id = 2, Title = "Build Minimal APIs", Done = true }
}))
.WithSummary("Get all todos")
.WithName("GetAllTodos");

todosGroup.MapGet("/search", (string? title, bool? done, int skip = 0, int take = 10) =>
    Results.Ok(new[] { new { Id = 1, Title = "Learn Kaya", Done = false } }))
.WithSummary("Search todos")
.WithName("SearchTodos");

todosGroup.MapGet("/{id:int}", (int id) =>
    Results.Ok(new { Id = id, Title = $"Todo #{id}", Done = false }))
.WithSummary("Get todo by ID")
.WithName("GetTodoById");

todosGroup.MapPost("/", (CreateTodoRequest? request) =>
    Results.Created($"/api/todos/1", new { Id = 1, Title = request?.Title ?? "", Done = request?.Done ?? false }))
.WithSummary("Create a new todo")
.WithName("CreateTodo");

todosGroup.MapPut("/{id:int}", (int id, CreateTodoRequest? request) =>
    Results.Ok(new { Id = id, Title = request?.Title ?? "", Done = request?.Done ?? false }))
.WithSummary("Update a todo")
.WithName("UpdateTodo");

todosGroup.MapDelete("/{id:int}", (int id) => Results.NoContent())
.WithSummary("Delete a todo")
.WithName("DeleteTodo")
.RequireAuthorization();

app.Run();

record CreateTodoRequest(string Title, bool Done = false);
