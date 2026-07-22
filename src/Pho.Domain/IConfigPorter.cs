using System.Threading.Tasks;

namespace Pho.Domain;

/// <summary>How an imported configuration is applied.</summary>
public enum ImportMode
{
    /// <summary>Clear all existing stubs and groups, then load the file.</summary>
    ReplaceAll,

    /// <summary>Add or update stubs and groups by id, leaving others in place.</summary>
    Merge
}

/// <summary>
/// Exports the full set of mocks (all stubs + the group tree) to JSON and imports them back
/// for backup/restore (F8). Received-request logs and history are not included.
/// </summary>
public interface IConfigPorter
{
    Task<string> ExportJsonAsync();

    /// <summary>Imports a previously exported JSON document. Throws on invalid input.</summary>
    Task ImportJsonAsync(string json, ImportMode mode);
}
