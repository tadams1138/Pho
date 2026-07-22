using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Pho.Domain;

namespace Pho.Infrastructure;

/// <summary>
/// EF Core context backing Pho's persistence (SQLite). The nested value objects on a Stub
/// (its RequestMatcher and ResponseDefinition) are stored as JSON columns via value converters,
/// keeping the schema simple; matching runs in memory after load.
/// </summary>
public sealed class PhoDbContext : DbContext
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public PhoDbContext(DbContextOptions<PhoDbContext> options) : base(options)
    {
    }

    public DbSet<Stub> Stubs => Set<Stub>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<ConfigRevisionRecord> ConfigRevisions => Set<ConfigRevisionRecord>();
    public DbSet<HistoryState> HistoryState => Set<HistoryState>();
    public DbSet<ReceivedRequest> ReceivedRequests => Set<ReceivedRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var group = modelBuilder.Entity<Group>();
        group.HasKey(g => g.Id);
        group.Property(g => g.Name).IsRequired();

        var revision = modelBuilder.Entity<ConfigRevisionRecord>();
        revision.HasKey(r => r.Id);
        revision.HasIndex(r => r.Sequence).IsUnique();

        modelBuilder.Entity<HistoryState>().HasKey(h => h.Id);

        var stub = modelBuilder.Entity<Stub>();
        stub.HasKey(s => s.Id);
        stub.Property(s => s.Name).IsRequired();

        stub.Property(s => s.Request)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<RequestMatcher>(v, JsonOptions)!)
            .Metadata.SetValueComparer(RecordComparer<RequestMatcher>());

        stub.Property(s => s.Response)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<ResponseDefinition>(v, JsonOptions)!)
            .Metadata.SetValueComparer(RecordComparer<ResponseDefinition>());

        var received = modelBuilder.Entity<ReceivedRequest>();
        received.HasKey(r => r.Id);
        received.HasIndex(r => r.ReceivedAt);
        received.Property(r => r.Headers)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string?>>(v, JsonOptions)!)
            .Metadata.SetValueComparer(JsonComparer<IReadOnlyDictionary<string, string?>>());
        received.Property(r => r.MatchedStubIds)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<Guid>>(v, JsonOptions)!)
            .Metadata.SetValueComparer(JsonComparer<IReadOnlyList<Guid>>());
    }

    // JSON-value equality for converted collection members (these records are insert-only).
    private static ValueComparer<T> JsonComparer<T>() where T : class
        => new(
            (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
            v => v == null ? 0 : JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
            v => v);

    // The JSON-backed members are immutable records with value equality; compare by value,
    // and snapshot by identity since they are never mutated in place.
    private static ValueComparer<T> RecordComparer<T>() where T : class
        => new(
            (a, b) => Equals(a, b),
            v => v == null ? 0 : v.GetHashCode(),
            v => v);
}
