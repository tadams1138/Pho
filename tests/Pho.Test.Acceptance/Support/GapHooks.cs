using Reqnroll;
using Reqnroll.UnitTestProvider;

namespace Pho.Test.Acceptance.Support;

/// <summary>
/// A scenario tagged <c>@gap</c> states behaviour the specification asks for but the product does
/// not (yet) have. It stays fully bound, but is reported as skipped with the gap's description as
/// the reason, so the gap shows up in every test run instead of hiding in a failing build.
/// </summary>
[Binding]
public sealed class GapHooks(ScenarioInfo scenario, IUnitTestRuntimeProvider runtime)
{
    /// <summary>Set to 1 to run gap scenarios anyway, e.g. to see whether a gap has been closed.</summary>
    public const string RunGapsVariable = "PHO_RUN_GAPS";

    [BeforeScenario("gap", Order = 0)]
    public void ReportGap()
    {
        if (Environment.GetEnvironmentVariable(RunGapsVariable) == "1") return;

        var reason = string.Join(' ', (scenario.Description ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        runtime.TestIgnore($"Specification gap: {reason}");
    }
}
