using System.Reflection;
using Kaya.McpServer.Core;
using Kaya.McpServer.Mcp;

var showHelp = args.Any(static a => string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase)
					  || string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase));
var isInteractive = !Console.IsInputRedirected && !Console.IsOutputRedirected;

if (args.Length == 0 && isInteractive)
{
	CliBranding.PrintLogo(Console.Out);
	Console.WriteLine("MCP stdio server for HTTP, SignalR and gRPC invocation through Kaya explorers.");
	Console.WriteLine($"Version: {GetVersion()}");
	Console.WriteLine();
	PrintUsage();
	return 0;
}

await CliBranding.TryRenderAsync(args, includeAnimation: !showHelp);

if (showHelp)
{
	Console.WriteLine("Kaya.McpServer (kaya-mcp)");
	Console.WriteLine("MCP stdio server for HTTP and gRPC invocation through Kaya explorers.");
	Console.WriteLine();
	PrintUsage();
	return 0;
}

await McpHost.RunAsync(args);
return 0;

static void PrintUsage()
{
	Console.WriteLine("Usage:");
	Console.WriteLine("  kaya-mcp [--api-url <url>] [--grpc-proxy-url <url>] [--config <path>]");
	Console.WriteLine();
	Console.WriteLine("Options:");
	Console.WriteLine("  --api-url <url>          Override KAYA_API_BASE_URL");
	Console.WriteLine("  --grpc-proxy-url <url>   Override KAYA_GRPC_PROXY_BASE_URL");
	Console.WriteLine("  --signalr-debug-route    Override KAYA_SIGNALR_DEBUG_ROUTE");
	Console.WriteLine("  --config <path>          Path to kaya.mcp.config.json");
	Console.WriteLine("  -h, --help               Show this help and exit");
}

static string GetVersion()
{
	var assembly = typeof(McpHost).Assembly;
	var informational = assembly
		.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
		?.InformationalVersion;

	if (!string.IsNullOrWhiteSpace(informational))
	{
		return informational.Split('+')[0];
	}

	return assembly.GetName().Version?.ToString() ?? "unknown";
}
