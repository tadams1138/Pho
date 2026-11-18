using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pho.Test.Acceptance.Support;

/// <summary>
/// Arranges and inspects Pho's configuration through its own in-process services — the same ones
/// the admin UI calls — so a scenario's "Given" state is real configuration, recorded in history.
/// </summary>
public sealed class Configuration(PhoApp app)
{
    public static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public StubService Stubs => app.Get<StubService>();

    public GroupService Groups => app.Get<GroupService>();

    public ConfigHistoryService History => app.Get<ConfigHistoryService>();

    /// <summary>Where history stood once the scenario's "Given" configuration was in place.</summary>
    public int ArrangedSequence { get; private set; }

    /// <summary>Records arranged configuration as a change in history, as the UI would.</summary>
    public async Task Record(string summary)
    {
        await History.RecordAsync(summary);
        ArrangedSequence = await app.Get<IConfigHistoryStore>().GetCurrentSequenceAsync();
    }

    public async Task<Stub> Stub(string name)
    {
        var all = await Stubs.ListAsync();

        return all.SingleOrDefault(s => s.Name == name)
               ?? throw new InvalidOperationException($"No stored stub named '{name}'. Stored: {string.Join(", ", all.Select(s => s.Name))}");
    }

    public async Task<Stub?> StubOrNull(string name) => (await Stubs.ListAsync()).SingleOrDefault(s => s.Name == name);

    public async Task<Group> Group(string name)
    {
        var all = await Groups.ListAsync();

        return all.SingleOrDefault(g => g.Name == name)
               ?? throw new InvalidOperationException($"No group named '{name}'.");
    }

    public async Task<Group?> GroupOrNull(string name) => (await Groups.ListAsync()).SingleOrDefault(g => g.Name == name);

    /// <summary>Changes a stored stub and records the change, as saving it in the editor would.</summary>
    public async Task Change(string name, Func<Stub, Stub> change)
    {
        var stub = await Stubs.GetAsync((await Stub(name)).Id) ?? throw new InvalidOperationException(name);
        var changed = change(stub);
        await Stubs.UpdateAsync(changed);
        await Record($"Changed '{name}'");
    }

    /// <summary>The whole configuration — every stub and group — in a form that compares by value.</summary>
    public async Task<string> Snapshot()
    {
        var stubs = (await Stubs.ListAsync()).OrderBy(s => s.Id).ToList();
        var groups = (await Groups.ListAsync()).OrderBy(g => g.Id).ToList();

        return JsonSerializer.Serialize(new { stubs, groups }, Json);
    }
}
