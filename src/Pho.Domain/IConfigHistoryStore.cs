using System.Threading.Tasks;

namespace Pho.Domain;

/// <summary>
/// Storage for configuration history: the current position, the ability to capture/restore the
/// whole configuration, and append/read/truncate revision snapshots by sequence number.
/// Revisions are numbered contiguously; sequence 0 is the empty baseline (never stored).
/// </summary>
public interface IConfigHistoryStore
{
    Task<int> GetCurrentSequenceAsync();
    Task SetCurrentSequenceAsync(int sequence);

    /// <summary>Snapshots the live configuration (all stubs and groups).</summary>
    Task<ConfigSnapshot> CaptureAsync();

    /// <summary>Replaces the live configuration with the given snapshot.</summary>
    Task RestoreAsync(ConfigSnapshot snapshot);

    Task AppendRevisionAsync(int sequence, string summary, ConfigSnapshot snapshot);
    Task TruncateAfterAsync(int sequence);

    /// <summary>The snapshot stored at the given sequence, or null if none (e.g. the baseline).</summary>
    Task<ConfigSnapshot?> GetRevisionSnapshotAsync(int sequence);
}
