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

// Admin UI (Blazor) on the admin port.
// Serve the Blazor framework files and static web assets (incl. _framework/blazor.web.js);
// without this the admin UI renders once server-side but never boots interactivity.
// UseStaticFiles (middleware) is used rather than MapStaticAssets: in this two-port
// setup the endpoint-based MapStaticAssets returned empty (Content-Length: 0) bodies
// for _framework/blazor.web.js in a published build.
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
