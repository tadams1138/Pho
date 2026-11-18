using Bunit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pho.Infrastructure;

namespace Pho.Test.Acceptance.Support;

/// <summary>
/// One running Pho per scenario: the real <c>Program</c> hosted in-process, on a private
/// in-memory SQLite database, with both of its surfaces reachable over HTTP and its UI
/// renderable through bUnit against the very same services and data.
/// </summary>
public sealed class PhoApp : IDisposable
{
    public const int DefaultAdminPort = 8931;
    public const int DefaultMockPort = 8932;

    private readonly string _databaseName = $"pho-acceptance-{Guid.NewGuid():N}";
    private SqliteConnection? _keepAlive;
    private WebApplicationFactory<Program>? _factory;
    private IServiceScope? _scope;
    private BunitContext? _ui;
    private HttpClient? _admin;
    private HttpClient? _mock;

    /// <summary>Configuration applied when the app starts (before first use).</summary>
    public Dictionary<string, string?> Settings { get; } = new();

    /// <summary>Overrides the database; by default each scenario gets its own in-memory one.</summary>
    public string? ConnectionString { get; set; }

    public LogCapture Logs { get; } = new();

    public bool IsStarted => _factory is not null;

    public int AdminPort => Settings.TryGetValue("Pho:AdminPort", out var v) && v is not null ? int.Parse(v) : DefaultAdminPort;

    public int MockPort => Settings.TryGetValue("Pho:MockPort", out var v) && v is not null ? int.Parse(v) : DefaultMockPort;

    private string EffectiveConnectionString
        => ConnectionString ?? $"Data Source=file:{_databaseName}?mode=memory&cache=shared";

    public WebApplicationFactory<Program> Factory => _factory ??= Start();

    /// <summary>A client of the admin surface — what a browser pointed at the admin UI talks to.</summary>
    public HttpClient Admin => _admin ??= ClientFor(AdminPort);

    /// <summary>A client of the mock-serving surface — what the system under test talks to.</summary>
    public HttpClient Mock => _mock ??= ClientFor(MockPort);

    public HttpClient ClientFor(int port)
        => Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"http://localhost:{port}"),
            AllowAutoRedirect = false
        });

    /// <summary>
    /// The app's services as one Blazor circuit sees them: a single scope shared by the UI and by
    /// the scenario's own arrangement, so both look at the same data.
    /// </summary>
    public IServiceProvider Services => (_scope ??= Factory.Services.CreateScope()).ServiceProvider;

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <summary>A bUnit renderer whose services fall back to the running app's.</summary>
    public BunitContext Ui => _ui ??= CreateUi();

    private WebApplicationFactory<Program> Start()
    {
        var connectionString = EffectiveConnectionString;

        // Program only creates the schema outside the Testing environment; keep the in-memory
        // database alive for the scenario and create it here.
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();
        using (var context = new PhoDbContext(new DbContextOptionsBuilder<PhoDbContext>().UseSqlite(_keepAlive).Options))
        {
            context.Database.EnsureCreated();
        }

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Pho", connectionString);
            foreach (var (key, value) in Settings) builder.UseSetting(key, value);
            builder.UseStaticWebAssets();
            builder.ConfigureLogging(logging => logging.AddProvider(Logs));
            builder.ConfigureServices(services =>
                services.AddTransient<IStartupFilter, HostPortAsLocalPort>());
        });
    }

    private BunitContext CreateUi()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFallbackServiceProvider(Services);

        return context;
    }

    private readonly List<Action> _cleanups = new();

    /// <summary>Runs <paramref name="cleanup"/> once the scenario is over.</summary>
    public void OnDispose(Action cleanup) => _cleanups.Add(cleanup);

    /// <summary>Stops Pho completely; the next use starts it again on the same database.</summary>
    public void Restart()
    {
        Stop();
        _ = Factory;
    }

    private void Stop()
    {
        _ui?.Dispose();
        _scope?.Dispose();
        _admin?.Dispose();
        _mock?.Dispose();
        _factory?.Dispose();
        _keepAlive?.Dispose();
        (_ui, _scope, _admin, _mock, _factory, _keepAlive) = (null, null, null, null, null, null);
    }

    public void Dispose()
    {
        Stop();
        foreach (var cleanup in _cleanups)
        {
            try
            {
                cleanup();
            }
            catch (IOException)
            {
                // A temporary file still in use is left for the OS to clean up.
            }
        }
    }
}

/// <summary>
/// The in-memory test server has no sockets, so every request arrives on "port 0". Kestrel
/// would report the port the request came in on; recreate that from the address the client
/// used, so Pho's own port-based routing between its two surfaces is what decides.
/// </summary>
internal sealed class HostPortAsLocalPort : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (context.Request.Host.Port is int port) context.Connection.LocalPort = port;

                return nextMiddleware(context);
            });
            next(app);
        };
}
