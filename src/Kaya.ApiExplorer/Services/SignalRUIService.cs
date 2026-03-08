using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Kaya.ApiExplorer.Configuration;

namespace Kaya.ApiExplorer.Services;

public interface ISignalRUIService
{
    Task<string> GetUIAsync();
}

[ExcludeFromCodeCoverage(Justification = "Serves embedded HTML/CSS/JS resources — requires a running host to test meaningfully.")]
public class SignalRUIService(KayaApiExplorerOptions options) : ISignalRUIService
{
    public async Task<string> GetUIAsync()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            // Read embedded resources for SignalR UI
            var htmlContent = await ReadEmbeddedResourceAsync(assembly, "UI.SignalR.signalr-debug.html");
            var mainCssContent = await ReadEmbeddedResourceAsync(assembly, "UI.ApiExplorer.styles.css");
            var signalRCssContent = await ReadEmbeddedResourceAsync(assembly, "UI.SignalR.signalr-debug.css");
            var authJsContent = await ReadEmbeddedResourceAsync(assembly, "UI.ApiExplorer.auth.js");
            var jsContent = await ReadEmbeddedResourceAsync(assembly, "UI.SignalR.signalr-debug.js");
            
            // Get the icon from main UI
            var favIconContent = await ReadEmbeddedResourceAsync(assembly, "UI.ApiExplorer.icon.svg");
            var svgBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(favIconContent));

            // Inject theme configuration into the HTML
            var themeScript = GenerateThemeScript();

            var finalHtml = htmlContent
                .Replace("<link rel=\"stylesheet\" href=\"../styles.css\">", $"<style>{mainCssContent}</style>")
                .Replace("<link rel=\"stylesheet\" href=\"signalr-debug.css\">", $"<style>{signalRCssContent}</style>")
                .Replace("<script src=\"signalr-debug.js\"></script>", $"{themeScript}<script>{authJsContent}</script><script>{jsContent}</script>")
                .Replace("<link rel=\"icon\" type=\"image/svg+xml\" href=\"icon.svg\">", $"<link rel=\"icon\" type=\"image/svg+xml\" href=\"data:image/svg+xml;base64,{svgBase64}\">");

            return finalHtml;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load SignalR Debug UI: {ex.Message}", ex);
        }
    }

    private string GenerateThemeScript()
    {
        var defaultTheme = options.Middleware.DefaultTheme.ToLower();
        
        if (defaultTheme != "light" && defaultTheme != "dark")
        {
            defaultTheme = "light";
        }

        var apiExplorerRoute = options.Middleware.RoutePrefix;

        return $@"
<script>
    // Kaya SignalR Debug Configuration
    window.KayaSignalRDebugConfig = {{
        defaultTheme: '{defaultTheme}',
        apiExplorerRoute: '{apiExplorerRoute}'
    }};
</script>";
    }

    private static async Task<string> ReadEmbeddedResourceAsync(Assembly assembly, string resourceName)
    {
        var fullResourceName = $"Kaya.ApiExplorer.{resourceName}";
        await using var stream = assembly.GetManifestResourceStream(fullResourceName) 
            ?? throw new InvalidOperationException($"Embedded resource '{fullResourceName}' not found.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
