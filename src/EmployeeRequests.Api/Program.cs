using EmployeeRequests.Api.Domain;
using EmployeeRequests.Api.Endpoints;
using EmployeeRequests.Api.HubSpot;

var builder = WebApplication.CreateBuilder(args);

// Routing table lives in its own file. Environment variables are re-added after it so that
// e.g. Routing__Teams__payroll__Email set on Render still overrides the file.
builder.Configuration
    .AddJsonFile("routing.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<HubSpotOptions>(builder.Configuration.GetSection("HubSpot"));
builder.Services.Configure<RoutingOptions>(builder.Configuration.GetSection("Routing"));
builder.Services.Configure<SlaOptions>(builder.Configuration.GetSection("Sla"));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new Classifier(ClassificationRules.Load(Path.Combine(AppContext.BaseDirectory, "rules.json"))));
builder.Services.AddTransient<RetryHandler>();
builder.Services.AddHttpClient<HubSpotClient>(c =>
{
    c.BaseAddress = new Uri("https://api.hubapi.com/");
    c.Timeout = TimeSpan.FromSeconds(20);
}).AddHttpMessageHandler<RetryHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// HubSpot failures become a clean 502 instead of a stack trace.
app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
{
    var error = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    ctx.RequestServices.GetRequiredService<ILogger<Program>>().LogError(error, "Unhandled error");
    ctx.Response.StatusCode = error is HubSpotException ? StatusCodes.Status502BadGateway : StatusCodes.Status500InternalServerError;
    await ctx.Response.WriteAsJsonAsync(new { error = "The request could not be completed right now. Please try again in a moment." });
}));

// React build (web/ → wwwroot) is served as static files.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapRequestEndpoints();

// Keep-alive ping for cron-job.org. Never touches HubSpot.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Client-side routes (/board, /track, …) fall back to the React app.
app.MapFallbackToFile("index.html");

app.Run();
