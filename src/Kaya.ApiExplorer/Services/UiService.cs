using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Kaya.ApiExplorer.Configuration;

namespace Kaya.ApiExplorer.Services;

public interface IUIService
{
    Task<string> GetUIAsync();
}

[ExcludeFromCodeCoverage(Justification = "Serves embedded HTML/CSS/JS resources — requires a running host to test meaningfully.")]
public class UIService(KayaApiExplorerOptions options) : IUIService
{
    public async Task<string> GetUIAsync()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            // Read embedded resources directly
            var htmlContent = await ReadEmbeddedResourceAsync(assembly, "UI.ApiExplorer.index.html");
            var cssContent = await ReadEmbeddedResourceAsync(assembly, "UI.ApiExplorer.styles.css");
            var authJsContent = await ReadEmbeddedResourceAsync(assembly, "UI.ApiExplorer.auth.js");
            var jsContent = await ReadEmbeddedResourceAsync(assembly, "UI.ApiExplorer.script.js");
            var favIconContent = await ReadEmbeddedResourceAsync(assembly, "UI.ApiExplorer.icon.svg");

            // Inject theme configuration into the HTML
            var themeScript = GenerateThemeScript();

            var svgBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(favIconContent));

            var finalHtml = htmlContent
                .Replace("<link rel=\"stylesheet\" href=\"styles.css\">", $"<style>{cssContent}</style>")
                .Replace("<script src=\"script.js\"></script>", $"{themeScript}<script>{authJsContent}</script><script>{jsContent}</script>")
                .Replace("<link rel=\"icon\" type=\"image/svg+xml\" href=\"icon.svg\">", $"<link rel=\"icon\" type=\"image/svg+xml\" href=\"data:image/svg+xml;base64,{svgBase64}\">");

            return finalHtml;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load UI: {ex.Message}", ex);
        }
    }

    private string GenerateThemeScript()
    {
        var defaultTheme = options.Middleware.DefaultTheme.ToLower();
        
        if (defaultTheme != "light" && defaultTheme != "dark")
        {
            defaultTheme = "light";
        }

        var signalRRoute = options.SignalRDebug.RoutePrefix;
        var signalREnabled = options.SignalRDebug.Enabled;
        var routePrefix = options.Middleware.RoutePrefix;

        return $@"
<script>
    // Kaya API Explorer Configuration
    window.KayaApiExplorerConfig = {{
        routePrefix: '{routePrefix}',
        defaultTheme: '{defaultTheme}',
        signalRRoute: '{signalRRoute}',
        signalREnabled: {signalREnabled.ToString().ToLower()}
    }};
</script>";
    }

    private static async Task<string> ReadEmbeddedResourceAsync(Assembly assembly, string resourceName)
    {
        var fullResourceName = $"Kaya.ApiExplorer.{resourceName}";
        using var stream = assembly.GetManifestResourceStream(fullResourceName) ?? throw new InvalidOperationException($"Embedded resource '{fullResourceName}' not found. Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
