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
    public void A_rule_row_carries_its_ignore_case_setting_onto_the_matcher()
    {
        // Arrange
        var draft = StubDraft.ForNewStub();
        draft.Name = "lenient";
        draft.RequestHeaders.Add(new ParamRuleDraft
        {
            Name = "Content-Type",
            Type = MatchRuleType.Equals,
            Value = "application/json",
            IgnoreCase = true
        });

        // Act
        var request = draft.ToStub().Request;

        // Assert
        request.Headers.Single().Rule.IgnoreCase.Should().BeTrue();
        request.Matches(new HttpRequestData("GET", "/",
            Headers: new Dictionary<string, string?> { ["content-type"] = "application/JSON" })).Should().BeTrue();
    }

    [Fact]
    public void A_body_rule_carries_its_ignore_case_setting_too()
    {
        // Arrange
        var draft = StubDraft.ForNewStub();
        draft.Name = "lenient-body";
        draft.MatchBody = true;
        draft.BodyMatchType = MatchRuleType.Contains;
        draft.BodyMatchValue = "orderId";
        draft.BodyIgnoreCase = true;

        // Act
        var body = draft.ToStub().Request.Body;

        // Assert
        body!.IgnoreCase.Should().BeTrue();
        body.Matches("{\"ORDERID\":7}").Should().BeTrue();
    }

    [Fact]
    public void Toggling_ignore_case_on_the_body_rule_makes_the_draft_dirty()
    {
        // Arrange
        var draft = StubDraft.ForNewStub();
        draft.MatchBody = true;
        draft.BodyMatchValue = "orderId";
        var baseline = draft.Clone();

        // Act
        draft.BodyIgnoreCase = true;

        // Assert
        draft.ValueEquals(baseline).Should().BeFalse();
    }

    [Fact]
    public void A_rule_row_ignores_case_only_where_the_rule_type_can_use_it()
    {
        // Arrange / Act / Assert — the editor shows the control only for these two
        ParamRuleDraft.TakesIgnoreCase(MatchRuleType.Equals).Should().BeTrue();
        ParamRuleDraft.TakesIgnoreCase(MatchRuleType.Contains).Should().BeTrue();
        ParamRuleDraft.TakesIgnoreCase(MatchRuleType.Regex).Should().BeFalse("a pattern says (?i) itself");
        ParamRuleDraft.TakesIgnoreCase(MatchRuleType.Present).Should().BeFalse();
        ParamRuleDraft.TakesIgnoreCase(MatchRuleType.Absent).Should().BeFalse();
    }

    [Fact]
    public void A_row_switched_to_a_type_that_cannot_use_it_does_not_smuggle_the_flag_through()
    {
        // Arrange — the author ticked ignore case, then changed the row to REGEX
        var draft = StubDraft.ForNewStub();
        draft.Name = "switched";
        draft.RequestHeaders.Add(new ParamRuleDraft
        {
            Name = "X-Trace",
            Type = MatchRuleType.Regex,
            Value = "^abc$",
            IgnoreCase = true
        });

        // Act
        var rule = draft.ToStub().Request.Headers.Single().Rule;

        // Assert
        rule.IgnoreCase.Should().BeFalse("the saved stub carries only what its rule type can use");
    }

    [Fact]
    public void Toggling_ignore_case_makes_the_draft_dirty()
    {
        // Arrange
        var draft = StubDraft.ForNewStub();
        draft.RequestHeaders.Add(new ParamRuleDraft { Name = "Accept", Value = "text/plain" });
        var baseline = draft.Clone();

        // Act
        draft.RequestHeaders[0].IgnoreCase = true;

        // Assert
        draft.ValueEquals(baseline).Should().BeFalse("an unsaved change must be noticed");
    }

    [Fact]
    public void A_saved_stub_round_trips_its_ignore_case_setting_back_into_the_editor()
    {
        // Arrange
        var draft = StubDraft.ForNewStub();
        draft.Name = "round-trip";
        draft.RequestHeaders.Add(new ParamRuleDraft
        {
            Name = "Accept",
            Type = MatchRuleType.Contains,
            Value = "json",
            IgnoreCase = true
        });

        // Act
        var reopened = StubDraft.From(draft.ToStub());

        // Assert
        reopened.RequestHeaders.Single().IgnoreCase.Should().BeTrue();
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
    public void Applying_basic_auth_adds_an_authorization_header_rule()
    {
        // Arrange
        var draft = StubDraft.ForNewStub();

        // Act
        draft.SetBasicAuth("user", "pass");

        // Assert
        var rule = draft.RequestHeaders.Should().ContainSingle().Subject;
        rule.Name.Should().Be("Authorization");
        rule.Type.Should().Be(MatchRuleType.Regex, "only a pattern can fold the scheme's case alone");
        rule.Value.Should().Be(@"^(?i:Basic)\s+dXNlcjpwYXNz$");
    }

    [Fact]
    public void Applying_basic_auth_replaces_an_authorization_rule_already_present()
    {
        // Arrange
        var draft = StubDraft.ForNewStub();
        draft.RequestHeaders.Add(new ParamRuleDraft { Name = "Accept", Value = "application/json" });
        draft.RequestHeaders.Add(new ParamRuleDraft { Name = "authorization", Value = "Basic old" });

        // Act
        draft.SetBasicAuth("user", "pass");

        // Assert
        draft.RequestHeaders.Should().HaveCount(2, "the header is replaced, not duplicated");
        draft.RequestHeaders.Single(r => r.Name == "Accept").Value.Should().Be("application/json");
        draft.RequestHeaders.Single(r => r.Name.Equals("authorization", StringComparison.OrdinalIgnoreCase))
            .Value.Should().Be(@"^(?i:Basic)\s+dXNlcjpwYXNz$");
    }

    [Fact]
    public void The_basic_auth_helper_reads_back_the_credential_already_on_the_draft()
    {
        var draft = StubDraft.ForNewStub();
        draft.SetBasicAuth("user", "pass");

        draft.CurrentBasicAuth().Should().Be(new BasicCredentials("user", "pass"));
    }

    [Fact]
    public void The_basic_auth_helper_reads_back_a_credential_authored_before_patterns_existed()
    {
        // Arrange — a stub written by hand, or imported from an older configuration
        var draft = StubDraft.ForNewStub();
        draft.RequestHeaders.Add(new ParamRuleDraft
        {
            Name = "Authorization",
            Type = MatchRuleType.Equals,
            Value = "Basic dXNlcjpwYXNz"
        });

        // Act
        var credentials = draft.CurrentBasicAuth();

        // Assert
        credentials.Should().Be(new BasicCredentials("user", "pass"), "the plain form is still editable");
    }

    [Fact]
    public void There_is_no_credential_to_read_back_without_a_basic_authorization_rule()
    {
        var draft = StubDraft.ForNewStub();
        draft.RequestHeaders.Add(new ParamRuleDraft { Name = "Authorization", Value = "Bearer token" });

        draft.CurrentBasicAuth().Should().BeNull();
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
