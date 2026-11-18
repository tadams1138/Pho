using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Pho.Test.Acceptance.Support;
using Reqnroll;

namespace Pho.Test.Acceptance.Steps;

/// <summary>The received-requests view.</summary>
[Binding]
public sealed class ReceivedSteps(Screens ui, MockSteps mock)
{
    private readonly DateTime _scenarioStarted = DateTime.UtcNow;

    private IRenderedComponent<Pho.Web.Components.Pages.ReceivedRequests> View => ui.Received;

    /// <summary>The summary rows (not the expanded detail rows) of the list, top to bottom.</summary>
    private IReadOnlyList<IElement> Rows => View.FindAll("table tbody tr").Where(tr => tr.QuerySelector(".detail") is null).ToList();

    private static string Cell(IElement row, int index) => Screens.Text(row.QuerySelectorAll("td")[index]);

    private IElement Row(string request)
    {
        var parts = request.Split(' ', 2);
        return Rows.FirstOrDefault(r => Cell(r, 1) == parts[0] && Cell(r, 2) == parts[1])
               ?? throw new InvalidOperationException($"'{request}' is not listed.");
    }

    private static string Outcome(string shown)
        => shown == "matched" ? "one stub"
            : shown == "no match" ? "no stub"
            : shown.StartsWith("AMBIGUOUS") ? "several stubs"
            : shown;

    [When("I open the received-requests view")]
    public void Open() => ui.ReopenReceived();

    [Given(@"^(\d+) requests have been received, to /r01 up to /r(\d+) in that order$")]
    public async Task GivenRequests(int count, int last)
    {
        count.Should().Be(last);
        for (var i = 1; i <= count; i++) await mock.Send("GET", $"/r{i:00}");
    }

    [When("""^a client sends (\S+) (\S+) with body "(.*)" and these headers:$""")]
    public Task SendWithBodyAndHeaders(string method, string path, string body, DataTable headers)
        => mock.Send(method, path, headers.Rows.Select(r => (r["name"], r["value"])).ToList(), body);

    [Then("the received-requests view lists:")]
    public void Lists(DataTable table)
    {
        var rows = Rows;
        rows.Should().HaveCount(table.RowCount);
        for (var i = 0; i < table.RowCount; i++)
        {
            var expected = table.Rows[i];
            Cell(rows[i], 1).Should().Be(expected["method"], "row {0}", i + 1);
            Cell(rows[i], 2).Should().Be(expected["path"], "row {0}", i + 1);
            if (expected.TryGetValue("outcome", out var outcome)) Outcome(Cell(rows[i], 3)).Should().Be(outcome, "row {0}", i + 1);
            if (expected.TryGetValue("status", out var status)) Cell(rows[i], 4).Should().Be(status, "row {0}", i + 1);
        }
    }

    [Then(@"^the received-requests view lists the paths (.+)$")]
    public void ListsPaths(string paths)
    {
        var range = Regex.Match(paths, @"^/r(\d+) down to /r(\d+)$");
        var expected = range.Success
            ? Enumerable.Range(0, int.Parse(range.Groups[1].Value) - int.Parse(range.Groups[2].Value) + 1)
                .Select(i => $"/r{int.Parse(range.Groups[1].Value) - i:00}").ToList()
            : paths.Split(',', StringSplitOptions.TrimEntries).ToList();

        Rows.Select(r => Cell(r, 2)).Should().Equal(expected);
    }

    [Then("""^the request "([^"]*)" is flagged as matching (\d+) stubs$""")]
    public void FlaggedAmbiguous(string request, int count) => Cell(Row(request), 3).Should().Be($"AMBIGUOUS ({count} stubs)");

    [When("""^I filter by path "([^"]*)"$""")]
    public async Task FilterPath(string path)
    {
        await View.Find("input[placeholder='Path contains…']").InputAsync(new ChangeEventArgs { Value = path });
        await Screens.ButtonIn(View.Find(".toolbar"), "Filter").ClickAsync(new MouseEventArgs());
    }

    [When("""^I filter by method "([^"]*)"$""")]
    public async Task FilterMethod(string method)
    {
        await View.Find("input[placeholder='Method (e.g. GET)']").InputAsync(new ChangeEventArgs { Value = method });
        await Screens.ButtonIn(View.Find(".toolbar"), "Filter").ClickAsync(new MouseEventArgs());
    }

    [Then("I can choose to show 10, 20, 50 or 100 requests per page")]
    public void PageSizes()
        => View.Find(".toolbar select").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).Should().Equal("10", "20", "50", "100");

    [When(@"^I choose to show (\d+) requests per page$")]
    public Task ChoosePageSize(int size) => View.Find(".toolbar select").ChangeAsync(new ChangeEventArgs { Value = size.ToString() });

    [Then(@"^the view is on page (\d+) of (\d+)$")]
    public void OnPage(int page, int of) => View.Markup.Should().Contain($"page {page} of {of}");

    [When("^I go to the (next|previous) page$")]
    public Task GoToPage(string which)
        => Screens.ButtonIn(View.FindAll(".toolbar").Last(), which == "next" ? "Next" : "Previous").ClickAsync(new MouseEventArgs());

    [When("I clear the log")]
    public Task Clear() => Screens.ButtonIn(View.Find(".toolbar"), "Clear log").ClickAsync(new MouseEventArgs());

    [Then("the received-requests view is empty")]
    public void Empty()
    {
        Rows.Should().BeEmpty();
        View.Markup.Should().Contain("No received requests.");
    }

    [When("""^I open the details of "([^"]*)"$""")]
    public Task OpenDetails(string request) => Screens.ButtonIn(Row(request), "Details").ClickAsync(new MouseEventArgs());

    private string Details => Screens.Text(View.Find(".detail"));

    [Then("""^the details show the query "([^"]*)"$""")]
    public void DetailQuery(string query) => Screens.Text(View.Find(".detail code")).Should().Be(query);

    [Then("""^the details show the header "([^"]*)"$""")]
    public void DetailHeader(string header) => View.FindAll(".detail li").Select(Screens.Text).Should().Contain(header);

    [Then("""^the details show the body "(.*)"$""")]
    public void DetailBody(string body) => View.Find(".detail pre").TextContent.Should().Be(body);

    [Then("""^"([^"]*)" shows the time it was received$""")]
    public void ShowsTime(string request)
    {
        var shown = DateTime.ParseExact(Cell(Row(request), 0), "u", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        shown.Should().BeOnOrAfter(_scenarioStarted.AddSeconds(-1)).And.BeOnOrBefore(DateTime.UtcNow.AddSeconds(1));
    }

    [Then("""^"([^"]*)" shows the outcome "([^"]*)" and the status (\d+)$""")]
    public void ShowsOutcome(string request, string outcome, string status)
    {
        Outcome(Cell(Row(request), 3)).Should().Be(outcome);
        Cell(Row(request), 4).Should().Be(status);
    }

    [Then("""^"([^"]*)" names the stub "([^"]*)"$""")]
    public void NamesStub(string request, string stub) => RowAndDetail(request).Should().Contain(stub);

    [Then("""^"([^"]*)" names the stubs "([^"]*)" and "([^"]*)"$""")]
    public void NamesStubs(string request, string first, string second)
        => RowAndDetail(request).Should().Contain(first).And.Contain(second);

    private string RowAndDetail(string request)
    {
        var row = Row(request);
        Screens.ButtonIn(row, "Details").Click(new MouseEventArgs());
        row = Row(request);

        return Screens.Text(row) + " " + Screens.Text(row.NextElementSibling!);
    }
}
