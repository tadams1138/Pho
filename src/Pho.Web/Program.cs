using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Pho.Domain;
using Pho.Infrastructure;
using Pho.Web;
using Pho.Web.Components;

var builder = WebApplication.CreateBuilder(args);

var adminPort = builder.Configuration.GetValue<int?>("Pho:AdminPort") ?? 8931;
var mockPort = builder.Configuration.GetValue<int?>("Pho:MockPort") ?? 8932;

// Two ports: admin UI (8931) and mock-serving surface (8932). Skipped under Testing,
// where the in-memory TestServer has no real sockets.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(adminPort);
        options.ListenAnyIP(mockPort);
    });
}

var connectionString = builder.Configuration.GetConnectionString("Pho") ?? "Data Source=pho.db";
builder.Services.AddDbContext<PhoDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<IStubStore, EfStubStore>();
builder.Services.AddScoped<IStubRepository, EfStubRepository>();
builder.Services.AddScoped<IGroupRepository, EfGroupRepository>();
builder.Services.AddScoped<IConfigHistoryStore, EfConfigHistoryStore>();

var retentionHours = builder.Configuration.GetValue<double?>("Pho:ReceivedRequestRetentionHours") ?? 24;
builder.Services.AddSingleton(new ReceivedRequestRetention { Value = TimeSpan.FromHours(retentionHours) });
builder.Services.AddScoped<IReceivedRequestLog, EfReceivedRequestLog>();
builder.Services.AddScoped<IConfigPorter, EfConfigPorter>();

builder.Services.AddScoped<StubService>();
builder.Services.AddScoped<GroupService>();
builder.Services.AddScoped<TreeEditService>();
builder.Services.AddScoped<ConfigHistoryService>();
builder.Services.AddSingleton<IMockTrafficPolicy>(new PortMockTrafficPolicy(mockPort));

builder.Services.AddHealthChecks();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<PhoDbContext>().Database.EnsureCreated();
}

// Mock-serving surface: requests on the mock port are handled entirely by the mock middleware
// and never reach the Blazor UI.
var policy = app.Services.GetRequiredService<IMockTrafficPolicy>();
app.MapWhen(policy.IsMockTraffic, mockApp => mockApp.UseMiddleware<MockServingMiddleware>());

// Everything below runs for the admin surface only — the mock branch above has already terminated —
// so mounting the UI under a sub-path can never change what a stub matches.

// A reverse proxy may serve the UI under a sub-path (e.g. https://host/Pho/). Nothing here
// hardcodes a prefix: the proxy either announces the one it stripped via X-Forwarded-Prefix, or it
// is configured as Pho:PathBase. Either way it lands in Request.PathBase, which App.razor renders
// as the page's <base href> — and every link in the UI is relative to that. Forwarded headers are
// accepted from any address: Pho sits on a test network behind its proxy, and these headers only
// affect link generation. See docs/spec/08-architecture.md (Hosting behind a reverse proxy).
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
                       | ForwardedHeaders.XForwardedHost
                       | ForwardedHeaders.XForwardedPrefix
};
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

// Accept the prefix however it is written — "Pho", "/Pho", "/Pho/" — rather than throwing deep in
// the pipeline on a missing leading slash.
var configuredPathBase = app.Configuration["Pho:PathBase"]?.Trim().Trim('/');
var pathBase = string.IsNullOrEmpty(configuredPathBase) ? null : "/" + configuredPathBase;

if (pathBase is not null)
{
    // Two proxy styles, one setting: UsePathBase covers a proxy that forwards the prefix through
    // (it strips the prefix off the path), and the fallback covers one that strips it silently,
    // where the request arrives with no prefix to match on.
    app.UsePathBase(pathBase);
    app.Use((context, next) =>
    {
        if (!context.Request.PathBase.HasValue) context.Request.PathBase = pathBase;

        return next(context);
    });
}

// Admin UI (Blazor) on the admin port.
// Serve the static web assets (incl. _framework/blazor.web.js) from wwwroot; without this the
// admin UI renders once server-side but never boots interactivity. This relies on the publish
// output actually containing wwwroot/_framework — see the Dockerfile note about NOT using
// `dotnet publish --no-restore`, which otherwise emits an empty asset manifest and no wwwroot.
app.UseStaticFiles();

app.UseAntiforgery();

// Liveness endpoint for the Docker Compose healthcheck (admin port).
app.MapHealthChecks("/health");

// Full-set export download (F8).
app.MapGet("/export", async (IConfigPorter porter, HttpContext ctx) =>
{
    var json = await porter.ExportJsonAsync();
    ctx.Response.Headers.ContentDisposition = "attachment; filename=pho-mocks.json";
    return Results.Text(json, "application/json");
});

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

// Exposed so the integration tests can host the app via WebApplicationFactory<Program>.
public partial class Program;
