var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// React build (web/ → wwwroot) is served as static files.
app.UseDefaultFiles();
app.UseStaticFiles();

// Keep-alive ping for cron-job.org. Never touches HubSpot.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Client-side routes (/board, /track, …) fall back to the React app.
app.MapFallbackToFile("index.html");

app.Run();
