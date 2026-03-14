namespace Kaya.McpServer.Core;

internal static class CliBranding
{
    private static readonly string[] Banner =
    [
        " _  __                 __  __  ____ ___",
        "| |/ /__ _ _   _  __ _|  \\/  |/ ___|  _ \\",
        "| ' // _` | | | |/ _` | |\\/| | |   | |_) |",
        "| . \\ (_| | |_| | (_| | |  | | |___|  __/",
        "|_|\\_\\__,_|\\__, |\\__,_|_|  |_|\\____|_|",
        "             |__/"
    ];

    private static readonly string[] Frames = ["|", "/", "-", "\\\\"];

    private static readonly string[] StartupSteps =
    [
        "Loading configuration",
        "Registering tools",
        "Binding stdio transport",
        "Starting MCP host"
    ];

    public static async Task TryRenderAsync(string[] args, bool includeAnimation)
    {
        if (!ShouldRender(args))
        {
            return;
        }

        var writer = Console.Error;

        WriteBannerBlock(writer, canColor: !Console.IsOutputRedirected && !Console.IsErrorRedirected);

        if (!includeAnimation)
        {
            return;
        }

        foreach (var step in StartupSteps)
        {
            await AnimateStepAsync(writer, step);
        }

        writer.WriteLine();
    }

    public static void PrintLogo(TextWriter writer)
    {
        var canColor = !Console.IsOutputRedirected && !Console.IsErrorRedirected;
        WriteBannerBlock(writer, canColor);
    }

    private static bool ShouldRender(string[] args)
    {
        // Keep stdout clean for MCP JSON-RPC when stdio is redirected by hosts.
        return !Console.IsOutputRedirected && !Console.IsInputRedirected;
    }

    private static void WriteBannerBlock(TextWriter writer, bool canColor)
    {
        if (canColor)
        {
            writer.Write("\u001b[38;5;39m");
        }

        foreach (var line in Banner)
        {
            writer.WriteLine(line);
        }

        if (canColor)
        {
            writer.Write("\u001b[0m");
        }
    }

    private static async Task AnimateStepAsync(TextWriter writer, string step)
    {
        for (var i = 0; i < 6; i++)
        {
            var frame = Frames[i % Frames.Length];
            writer.Write($"\r[{frame}] {step}...");
            await Task.Delay(45);
        }

        writer.Write($"\r[OK] {step}.\n");
    }
}
