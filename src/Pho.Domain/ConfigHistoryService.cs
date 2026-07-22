using System.Threading.Tasks;

namespace Pho.Domain;

/// <summary>
/// Whole-configuration undo/redo (F6/F7). Every change records a new revision capturing the
/// entire configuration; undo/redo step a pointer through the revisions and restore state.
/// Making a new change after undo discards the forward (redo) revisions.
/// See docs/spec/03-domain-model.md (Configuration history and undo/redo).
/// </summary>
public sealed class ConfigHistoryService
{
    private readonly IConfigHistoryStore _store;

    public ConfigHistoryService(IConfigHistoryStore store)
    {
        _store = store;
    }

    /// <summary>Records the current configuration as a new revision after a change.</summary>
    public async Task RecordAsync(string summary)
    {
        var current = await _store.GetCurrentSequenceAsync();
        await _store.TruncateAfterAsync(current);

        var snapshot = await _store.CaptureAsync();
        var next = current + 1;
        await _store.AppendRevisionAsync(next, summary, snapshot);
        await _store.SetCurrentSequenceAsync(next);
    }

    public async Task<bool> CanUndoAsync()
        => await _store.GetCurrentSequenceAsync() > 0;

    public async Task<bool> CanRedoAsync()
    {
        var current = await _store.GetCurrentSequenceAsync();
        return await _store.GetRevisionSnapshotAsync(current + 1) is not null;
    }

    public async Task UndoAsync()
    {
        var current = await _store.GetCurrentSequenceAsync();
        if (current <= 0) return;

        var target = current - 1;
        // Sequence 0 is the empty baseline: no stored snapshot, restore an empty configuration.
        var snapshot = await _store.GetRevisionSnapshotAsync(target) ?? ConfigSnapshot.Empty;
        await _store.RestoreAsync(snapshot);
        await _store.SetCurrentSequenceAsync(target);
    }

    public async Task RedoAsync()
    {
        var current = await _store.GetCurrentSequenceAsync();
        var target = current + 1;

        var snapshot = await _store.GetRevisionSnapshotAsync(target);
        if (snapshot is null) return;

        await _store.RestoreAsync(snapshot);
        await _store.SetCurrentSequenceAsync(target);
    }
}
