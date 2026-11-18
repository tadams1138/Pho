using Pho.Test.Acceptance.Support;
using Reqnroll;

namespace Pho.Test.Acceptance.Steps;

/// <summary>Givens that put configuration in place, recorded in history like any user change.</summary>
[Binding]
public sealed class ArrangeSteps(Configuration config)
{
    [Given("these groups:")]
    public async Task GivenTheseGroups(DataTable table)
    {
        foreach (var row in table.Rows)
        {
            var parent = row.TryGetValue("parent", out var p) && !string.IsNullOrWhiteSpace(p) ? (await config.Group(p)).Id : (Guid?)null;
            await config.Groups.CreateAsync(row["name"], parent);
        }

        await config.Record("Arranged groups");
    }

    [Given("these stubs:")]
    public async Task GivenTheseStubs(DataTable table)
    {
        foreach (var row in table.Rows)
        {
            string Cell(string column, string fallback)
                => row.TryGetValue(column, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;

            var name = row["name"];
            var group = Cell("group", "");
            await config.Stubs.CreateAsync(new Stub
            {
                Name = name,
                Enabled = Cell("enabled", "yes") == "yes",
                GroupId = group == "" ? null : (await config.Group(group)).Id,
                Request = new RequestMatcher
                {
                    Method = Enum.Parse<HttpMethodMatch>(Cell("method", "GET"), ignoreCase: true),
                    Path = new PathMatcher(Enum.Parse<PathMatchType>(Cell("match", "EXACT"), ignoreCase: true), Cell("path", "/" + name.ToLowerInvariant().Replace(' ', '-')))
                },
                Response = new ResponseDefinition
                {
                    Status = int.Parse(Cell("status", "200")),
                    // By default a stub answers with its own name, so it is easy to see which one answered.
                    Body = row.TryGetValue("body", out var body) ? body : name
                }
            });
        }

        await config.Record("Arranged stubs");
    }

    [Given("""^the stub "([^"]*)" has a (header|query parameter) rule "([^"]*)" (equals|contains|regex|present|absent)((?: ".*")?)((?: ignoring case)?)$""")]
    public Task GivenARule(string stub, string kind, string name, string type, string value, string ignoreCase)
        => config.Change(stub, s =>
        {
            var rule = new ParamMatcher(name, Rule(type, value, ignoreCase));
            s.Request = kind == "header"
                ? s.Request with { Headers = s.Request.Headers.Append(rule).ToList() }
                : s.Request with { QueryParams = s.Request.QueryParams.Append(rule).ToList() };
            return s;
        });

    [Given("""^the stub "([^"]*)" has a body rule (equals|contains|regex|present|absent)((?: ".*")?)((?: ignoring case)?)$""")]
    public Task GivenABodyRule(string stub, string type, string value, string ignoreCase)
        => config.Change(stub, s =>
        {
            s.Request = s.Request with { Body = Rule(type, value, ignoreCase) };
            return s;
        });

    [Given("""^the stub "([^"]*)" has a response header "([^"]*)" set to "([^"]*)"$""")]
    public Task GivenAResponseHeader(string stub, string name, string value)
        => config.Change(stub, s =>
        {
            s.Response = s.Response with { Headers = s.Response.Headers.Append(new HeaderValue(name, value)).ToList() };
            return s;
        });

    [Given("""^the stub "([^"]*)" is disabled$""")]
    public Task GivenDisabled(string stub)
        => config.Change(stub, s =>
        {
            s.Enabled = false;
            return s;
        });

    public static MatchRuleType RuleType(string type) => Enum.Parse<MatchRuleType>(type, ignoreCase: true);

    private static MatchRule Rule(string type, string value, string ignoreCase)
        => new(RuleType(type), OptionalQuoted(value), ignoreCase.Length > 0);

    /// <summary>An optional <c> "value"</c> captured from a step, without its quotes; null when absent.</summary>
    public static string? OptionalQuoted(string captured)
        => captured.Trim().Length == 0 ? null : Unescape(captured.Trim()[1..^1]);

    public static string Unescape(string value) => value.Replace("\\\"", "\"");
}
