using Pho.Domain;
using Pho.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IStubStore, InMemoryStubStore>();

var app = builder.Build();

// The mock-serving surface: every request is treated as mock traffic.
app.UseMiddleware<MockServingMiddleware>();

app.Run();

// Exposed so the integration tests can host the app via WebApplicationFactory<Program>.
public partial class Program;
