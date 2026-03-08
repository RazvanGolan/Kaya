using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Kaya.GrpcExplorer.Configuration;

namespace Kaya.GrpcExplorer.Services;

public interface IGrpcUiService
{
   Task<string> GetUIAsync();
}

/// <summary>
/// Service for generating the gRPC Explorer UI
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Reads embedded assembly resources (HTML/CSS/JS) that are only present in the deployed package assembly; behavior is verified through integration tests.")]
public class GrpcUiService(KayaGrpcExplorerOptions options) : IGrpcUiService
{
    private string? _cachedUi;

   /// <summary>
   /// Gets the complete UI HTML with embedded resources
   /// </summary>
   public async Task<string> GetUIAsync()
   {
       if (_cachedUi is not null)
       {
           return _cachedUi;
       }

       var assembly = Assembly.GetExecutingAssembly();

       // Read all resources from GrpcExplorer assembly
       var sharedStyles = await ReadEmbeddedResourceAsync(assembly, "UI.styles.css");
       var sharedAuth = await ReadEmbeddedResourceAsync(assembly, "UI.auth.js");
       var htmlContent = await ReadEmbeddedResourceAsync(assembly, "UI.index.html");
       var grpcStyles = await ReadEmbeddedResourceAsync(assembly, "UI.grpc-styles.css");
       var grpcScript = await ReadEmbeddedResourceAsync(assembly, "UI.script.js");
       var iconSvg = await ReadEmbeddedResourceAsync(assembly, "UI.icon.svg");

       // Generate config script
       var configScript = GenerateConfigScript();

       // Convert icon to base64 for inline embedding
       var svgBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(iconSvg));

       // Combine all resources
       _cachedUi = htmlContent
           .Replace("<!-- SHARED_STYLES -->", $"<style>{sharedStyles}</style>")
           .Replace("<!-- GRPC_STYLES -->", $"<style>{grpcStyles}</style>")
           .Replace("<!-- CONFIG_SCRIPT -->", configScript)
           .Replace("<!-- SHARED_AUTH_SCRIPT -->", $"<script>{sharedAuth}</script>")
           .Replace("<!-- GRPC_SCRIPT -->", $"<script>{grpcScript}</script>")
           .Replace("<link rel=\"icon\" type=\"image/svg+xml\" href=\"icon.svg\">", $"<link rel=\"icon\" type=\"image/svg+xml\" href=\"data:image/svg+xml;base64,{svgBase64}\">");
       
       return _cachedUi;
   }

   /// <summary>
   /// Generates JavaScript configuration
   /// </summary>
   private string GenerateConfigScript()
   {
       var config = new
       {
           routePrefix = options.Middleware.RoutePrefix,
           defaultTheme = options.Middleware.DefaultTheme,
           defaultServerAddress = options.Middleware.DefaultServerAddress,
           streamBufferSize = options.Middleware.StreamBufferSize,
           requestTimeoutSeconds = options.Middleware.RequestTimeoutSeconds
       };

       var json = JsonSerializer.Serialize(config);
       return $"<script>window.KayaGrpcExplorerConfig = {json};</script>";
   }

   /// <summary>
   /// Reads an embedded resource as string
   /// </summary>
   private static async Task<string> ReadEmbeddedResourceAsync(Assembly assembly, string resourcePath)
   {
       var fullResourceName = assembly.GetManifestResourceNames()
           .FirstOrDefault(name => name.EndsWith(resourcePath.Replace("/", ".")));

       if (fullResourceName == null)
       {
           return string.Empty;
       }

       await using var stream = assembly.GetManifestResourceStream(fullResourceName);
       if (stream is null)
       {
           return string.Empty;
       }

       using var reader = new StreamReader(stream, Encoding.UTF8);
       return await reader.ReadToEndAsync();
   }
}
