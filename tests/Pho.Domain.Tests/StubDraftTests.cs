using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class StubDraftTests
{
    private static Stub Existing() => new()
    {
        Name = "login",
        Description = "the happy path",
        Enabled = false,
        Request = new RequestMatcher
        {
            Method = HttpMethodMatch.Post,
            Path = new PathMatcher(PathMatchType.Exact, "/login"),
            QueryParams = new List<ParamMatcher> { new("v", new MatchRule(MatchRuleType.Equals, "2")) },
            Headers = new List<ParamMatcher> { new("Authorization", new MatchRule(MatchRuleType.Present)) },
            Body = new MatchRule(MatchRuleType.Contains, "user")
        },
        Response = new ResponseDefinition
        {
            Status = 201,
            Headers = new List<HeaderValue> { new("Content-Type", "application/json") },
            Body = """{"ok":true}"""
        }
    };

    [Fact]
    public void Round_trips_a_stub_including_header_matching_and_response_headers()
    {
        var stub = Existing();

        var draft = StubDraft.From(stub);
        var rebuilt = draft.ToStub();

        rebuilt.Id.Should().Be(stub.Id);
        rebuilt.Name.Should().Be("login");
        rebuilt.Description.Should().Be("the happy path");
        rebuilt.Enabled.Should().BeFalse();
        // Structural comparison: the rebuilt matcher owns fresh rule lists, so record equality
        // (reference-based for its list members) is not what we mean by "the same matcher".
        rebuilt.Request.Should().BeEquivalentTo(stub.Request);
        rebuilt.Response.Should().BeEquivalentTo(stub.Response);
    }

    [Fact]
    public void A_request_header_rule_is_editable_and_applied_to_the_matcher()
    {
        var draft = StubDraft.ForNewStub();
        draft.Name = "needs-a-token";
        draft.RequestHeaders.Add(new ParamRuleDraft { Name = "X-Api-Key", Type = MatchRuleType.Equals, Value = "abc" });

        var request = draft.ToStub().Request;

        request.Headers.Should().ContainSingle()
            .Which.Should().Be(new ParamMatcher("X-Api-Key", new MatchRule(MatchRuleType.Equals, "abc")));
        request.Matches(new HttpRequestData("GET", "/",
            Headers: new Dictionary<string, string?> { ["x-api-key"] = "abc" })).Should().BeTrue();
    }

    [Fact]
    public void Present_and_absent_rules_carry_no_value()
    {
        var draft = StubDraft.ForNewStub();
        draft.RequestHeaders.Add(new ParamRuleDraft { Name = "X-Trace", Type = MatchRuleType.Present, Value = "ignored" });

        draft.ToStub().Request.Headers.Single().Rule.Value.Should().BeNull();
    }

    [Fact]
    public void Response_headers_are_emitted_on_the_response()
    {
        var draft = StubDraft.ForNewStub();
        draft.Name = "json";
        draft.ResponseHeaders.Add(new HeaderDraft { Name = "Content-Type", Value = "application/json" });

        draft.ToStub().Response.Headers.Should().ContainSingle()
            .Which.Should().Be(new HeaderValue("Content-Type", "application/json"));
    }

    [Fact]
    public void Blank_rows_are_dropped_and_names_trimmed()
    {
        var draft = StubDraft.ForNewStub();
        draft.RequestHeaders.Add(new ParamRuleDraft { Name = "  Accept  ", Value = "text/plain" });
        draft.RequestHeaders.Add(new ParamRuleDraft());
        draft.ResponseHeaders.Add(new HeaderDraft());

        var stub = draft.ToStub();

        stub.Request.Headers.Should().ContainSingle().Which.Name.Should().Be("Accept");
        stub.Response.Headers.Should().BeEmpty();
    }

    [Fact]
    public void A_clone_starts_out_equal_and_stops_being_equal_once_edited()
    {
        var draft = StubDraft.From(Existing());
        var baseline = draft.Clone();

        draft.ValueEquals(baseline).Should().BeTrue();

        draft.ResponseHeaders[0].Value = "text/plain";
        draft.ValueEquals(baseline).Should().BeFalse("an edited response header is an unsaved change");
    }

    [Fact]
    public void Adding_or_removing_a_rule_row_counts_as_a_change()
    {
        var draft = StubDraft.From(Existing());
        var baseline = draft.Clone();

        draft.RequestHeaders.RemoveAt(0);

        draft.ValueEquals(baseline).Should().BeFalse();
    }

    [Fact]
    public void Validation_requires_a_name_a_path_a_sane_status_and_named_headers()
    {
        var draft = StubDraft.ForNewStub();
        draft.Name = "  ";
        draft.Path = "";
        draft.Status = 99;
        draft.RequestHeaders.Add(new ParamRuleDraft { Name = "", Value = "x" });
        draft.ResponseHeaders.Add(new HeaderDraft { Name = "", Value = "x" });

        var errors = draft.Validate();

        errors.Should().HaveCount(5);
        errors.Should().Contain(e => e.Contains("Name"));
        errors.Should().Contain(e => e.Contains("Path"));
        errors.Should().Contain(e => e.Contains("status"));
    }

    [Fact]
    public void Validation_rejects_a_regex_that_does_not_compile()
    {
        var draft = StubDraft.ForNewStub();
        draft.Name = "bad-regex";
        draft.PathType = PathMatchType.Regex;
        draft.Path = "([";

        draft.Validate().Should().ContainSingle().Which.Should().Contain("regular expression");
    }

    [Fact]
    public void A_blank_name_is_filled_in_from_the_method_and_path_on_save()
    {
        var draft = StubDraft.ForNewStub();
        draft.Method = HttpMethodMatch.Post;
        draft.Path = "/sessions";

        draft.ApplyDefaultName();

        draft.Name.Should().Be("POST /sessions");
        draft.Validate().Should().BeEmpty();
    }

    [Fact]
    public void A_name_the_author_typed_survives_the_default()
    {
        var draft = StubDraft.ForNewStub();
        draft.Name = "login";
        draft.Path = "/sessions";

        draft.ApplyDefaultName();

        draft.Name.Should().Be("login");
    }

    [Fact]
    public void The_default_name_tracks_the_method_and_path_being_edited()
    {
        var draft = StubDraft.ForNewStub();
        draft.Method = HttpMethodMatch.Delete;
        draft.Path = "/users/7";

        draft.DefaultName.Should().Be("DELETE /users/7");
    }

    [Fact]
    public void A_valid_draft_reports_no_errors()
    {
        StubDraft.From(Existing()).Validate().Should().BeEmpty();
    }

    [Fact]
    public void A_new_draft_starts_in_the_group_it_was_created_from()
    {
        var groupId = Guid.NewGuid();

        var draft = StubDraft.ForNewStub(groupId);

        draft.Id.Should().BeNull();
        draft.GroupId.Should().Be(groupId);
        draft.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Applying_a_draft_updates_an_existing_stub_in_place()
    {
        var stub = Existing();
        var draft = StubDraft.From(stub);
        draft.Name = "renamed";
        draft.Status = 500;

        draft.ApplyTo(stub);

        stub.Name.Should().Be("renamed");
        stub.Response.Status.Should().Be(500);
    }
}
