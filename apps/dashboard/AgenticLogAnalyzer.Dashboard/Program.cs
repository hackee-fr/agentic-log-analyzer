var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => Results.Content("""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Agentic Log Analyzer</title>
</head>
<body>
  <main>
    <h1>Agentic Log Analyzer</h1>
    <p>Dashboard foundation. UI implementation comes after the deterministic log engine.</p>
  </main>
</body>
</html>
""", "text/html"));

app.Run();
