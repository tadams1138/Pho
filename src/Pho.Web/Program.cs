using Microsoft.EntityFrameworkCore;
using Pho.Domain;
using Pho.Infrastructure;
using Pho.Web;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Pho") ?? "Data Source=pho.db";
builder.Services.AddDbContext<PhoDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<IStubStore, EfStubStore>();

var app = builder.Build();

// Create the SQLite schema on startup. Skipped under the "Testing" environment, where
// integration tests supply their own store.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<PhoDbContext>().Database.EnsureCreated();
}

// The mock-serving surface: every request is treated as mock traffic.
app.UseMiddleware<MockServingMiddleware>();

app.Run();

// Exposed so the integration tests can host the app via WebApplicationFactory<Program>.
public partial class Program;
