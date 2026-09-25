using System.Net;
using AgenticLogAnalyzer.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.FileProviders;
using Photino.NET;

namespace AgenticLogAnalyzer.Desktop;

/// <summary>
/// Desktop shell: runs the same API as the web version in-process on a random loopback port,
/// serves the built dashboard from wwwroot/ and shows it in a native window (Photino).
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var dataDirectory = DesktopPaths.GetDataDirectory();
        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.UseKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Ollama is enabled by default: when it is not installed, the assistant keeps its deterministic answers.
        var options = AnalyzerHostOptions.FromConfiguration(
            builder.Configuration,
            Path.Combine(dataDirectory, "events.sqlite3"),
            [],
            Path.Combine(dataDirectory, "events.jsonl"),
            defaultLlmProvider: "ollama");
        builder.Services.AddLogAnalyzer(options);

        var app = builder.Build();

        // The dashboard is served from the same origin as the API, so it calls relative URLs.
        app.MapGet("/app-config.js", () => Results.Text("window.__APP_CONFIG__ = { apiBase: \"\" };", "text/javascript"));
        app.MapLogAnalyzerApi();

        if (Directory.Exists(webRoot))
        {
            var files = new PhysicalFileProvider(webRoot);
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
            app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = files });
        }
        else
        {
            app.MapGet("/", () => Results.Text(
                "Dashboard files are missing. Build them with 'pnpm build' (or 'make desktop') and restart.",
                "text/plain"));
        }

        app.StartAsync().GetAwaiter().GetResult();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        var window = new PhotinoWindow()
            .SetLogVerbosity(0)
            .SetTitle("Agentic Log Analyzer")
            .SetUseOsDefaultSize(false)
            .SetSize(1440, 900)
            .SetMinSize(960, 640)
            .Center()
            .SetResizable(true)
            .Load(new Uri(address));

        window.WaitForClose();
        app.StopAsync().GetAwaiter().GetResult();
    }
}
