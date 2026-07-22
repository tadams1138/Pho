using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class ConfigHistoryServiceTests
{
    // A fake store whose "live config" is a list of names the test mutates before recording.
    private sealed class FakeStore : IConfigHistoryStore
    {
        public List<string> Live { get; private set; } = new();
        private int _current;
        private readonly Dictionary<int, List<string>> _revisions = new();

        public Task<int> GetCurrentSequenceAsync() => Task.FromResult(_current);
        public Task SetCurrentSequenceAsync(int sequence) { _current = sequence; return Task.CompletedTask; }

        public Task<ConfigSnapshot> CaptureAsync()
            => Task.FromResult(Snapshot(Live));

        public Task RestoreAsync(ConfigSnapshot snapshot)
        {
            Live = snapshot.Stubs.Select(s => s.Name).ToList();
            return Task.CompletedTask;
        }

        public Task AppendRevisionAsync(int sequence, string summary, ConfigSnapshot snapshot)
        {
            _revisions[sequence] = snapshot.Stubs.Select(s => s.Name).ToList();
            return Task.CompletedTask;
        }

        public Task TruncateAfterAsync(int sequence)
        {
            foreach (var key in _revisions.Keys.Where(k => k > sequence).ToList())
                _revisions.Remove(key);
            return Task.CompletedTask;
        }

        public Task<ConfigSnapshot?> GetRevisionSnapshotAsync(int sequence)
            => Task.FromResult(_revisions.TryGetValue(sequence, out var names) ? Snapshot(names) : null);

        private static ConfigSnapshot Snapshot(IEnumerable<string> names)
            => new(names.Select(n => new Stub
            {
                Name = n,
                Request = new RequestMatcher { Path = new PathMatcher(PathMatchType.Exact, "/" + n) },
                Response = new ResponseDefinition()
            }).ToList(), new List<Group>());
    }

    private static async Task ChangeAsync(FakeStore store, ConfigHistoryService history, string addName)
    {
        store.Live.Add(addName);
        await history.RecordAsync($"add {addName}");
    }

    [Fact]
    public async Task Undo_restores_the_previous_configuration()
    {
        var store = new FakeStore();
        var history = new ConfigHistoryService(store);
        await ChangeAsync(store, history, "A");
        await ChangeAsync(store, history, "B");

        await history.UndoAsync();

        store.Live.Should().ContainSingle().Which.Should().Be("A");
    }

    [Fact]
    public async Task Undo_to_the_baseline_yields_an_empty_configuration()
    {
        var store = new FakeStore();
        var history = new ConfigHistoryService(store);
        await ChangeAsync(store, history, "A");

        await history.UndoAsync();

        store.Live.Should().BeEmpty();
        (await history.CanUndoAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Redo_reapplies_an_undone_change()
    {
        var store = new FakeStore();
        var history = new ConfigHistoryService(store);
        await ChangeAsync(store, history, "A");
        await ChangeAsync(store, history, "B");
        await history.UndoAsync();

        (await history.CanRedoAsync()).Should().BeTrue();
        await history.RedoAsync();

        store.Live.Should().BeEquivalentTo("A", "B");
    }

    [Fact]
    public async Task A_new_change_after_undo_discards_the_redo_stack()
    {
        var store = new FakeStore();
        var history = new ConfigHistoryService(store);
        await ChangeAsync(store, history, "A");
        await ChangeAsync(store, history, "B");
        await history.UndoAsync();          // back to {A}

        await ChangeAsync(store, history, "C"); // new change

        store.Live.Should().BeEquivalentTo("A", "C");
        (await history.CanRedoAsync()).Should().BeFalse();
    }
}
