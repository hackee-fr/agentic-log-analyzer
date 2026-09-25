using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var clientBuildPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "web", "dist"));

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "agentic-log-analyzer-dashboard" }));

if (Directory.Exists(clientBuildPath))
{
    var fileProvider = new PhysicalFileProvider(clientBuildPath);
    app.Lifetime.ApplicationStopped.Register(fileProvider.Dispose);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = fileProvider });
}
else
{
    app.MapGet("/", () => Results.Problem(
        "The React dashboard has not been built. Run 'pnpm install' and 'pnpm build' from apps/dashboard/AgenticLogAnalyzer.Dashboard/web."));
}

app.Run();
