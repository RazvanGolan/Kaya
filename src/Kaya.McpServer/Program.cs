using Kaya.McpServer.Mcp;

if (args.Any(static a => string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase)
					  || string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase)))
{
	Console.WriteLine("Kaya.McpServer (kaya-mcp)");
	Console.WriteLine("MCP stdio server for HTTP and gRPC invocation through Kaya explorers.");
	Console.WriteLine();
	Console.WriteLine("Usage:");
	Console.WriteLine("  kaya-mcp [--api-url <url>] [--grpc-proxy-url <url>] [--config <path>]");
	Console.WriteLine();
	Console.WriteLine("Options:");
	Console.WriteLine("  --api-url <url>          Override KAYA_API_BASE_URL");
	Console.WriteLine("  --grpc-proxy-url <url>   Override KAYA_GRPC_PROXY_BASE_URL");
	Console.WriteLine("  --config <path>          Path to kaya.mcp.config.json");
	Console.WriteLine("  -h, --help               Show this help and exit");
	return 0;
}

await McpHost.RunAsync(args);
return 0;
