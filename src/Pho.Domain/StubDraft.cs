using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pho.Domain;

/// <summary>
/// One editable query-parameter or header rule row in the stub editor.
/// A mutable record so the UI can bind to it while still comparing by value (dirty tracking).
/// </summary>
public sealed record ParamRuleDraft
{
    public string Name { get; set; } = "";
    public MatchRuleType Type { get; set; } = MatchRuleType.Equals;
    public string Value { get; set; } = "";

    public bool IsBlank => string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Value);

    /// <summary>PRESENT / ABSENT assert existence only; their value box is hidden and ignored.</summary>
    public static bool TakesValue(MatchRuleType type)
        => type is not (MatchRuleType.Present or MatchRuleType.Absent);

    public static ParamRuleDraft From(ParamMatcher matcher)
        => new() { Name = matcher.Name, Type = matcher.Rule.Type, Value = matcher.Rule.Value ?? "" };

    public ParamMatcher ToMatcher()
        => new(Name.Trim(), new MatchRule(Type, TakesValue(Type) ? Value : null));
}

/// <summary>One editable response-header row in the stub editor.</summary>
public sealed record HeaderDraft
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";

    public bool IsBlank => string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Value);

    public static HeaderDraft From(HeaderValue header) => new() { Name = header.Name, Value = header.Value };

    public HeaderValue ToHeader() => new(Name.Trim(), Value);
}

/// <summary>
/// The stub editor's working copy: everything the editor panel binds to, independent of the saved
/// stub. Holding the edit state here (rather than in the component) is what makes "is this dirty?"
/// and "is this valid?" ordinary testable logic — the panel keeps a saved baseline and compares
/// with <see cref="ValueEquals"/> to decide whether leaving needs a warning.
/// See docs/spec/05-screens-and-flows.md (stub editor).
/// </summary>
public sealed class StubDraft
{
    /// <summary>The stub being edited, or null for a stub that has never been saved.</summary>
    public Guid? Id { get; init; }

    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid? GroupId { get; set; }
    public bool Enabled { get; set; } = true;

    public HttpMethodMatch Method { get; set; } = HttpMethodMatch.Get;
    public PathMatchType PathType { get; set; } = PathMatchType.Exact;
    public string Path { get; set; } = "/";
    public List<ParamRuleDraft> QueryParams { get; init; } = new();
    public List<ParamRuleDraft> RequestHeaders { get; init; } = new();

    public bool MatchBody { get; set; }
    public MatchRuleType BodyMatchType { get; set; } = MatchRuleType.Equals;
    public string BodyMatchValue { get; set; } = "";

    public int Status { get; set; } = 200;
    public List<HeaderDraft> ResponseHeaders { get; init; } = new();
    public string Body { get; set; } = "";

    public static StubDraft ForNewStub(Guid? groupId = null) => new() { GroupId = groupId };

    /// <summary>The name a stub falls back to when the author leaves the box blank: "POST /sessions".</summary>
    public string DefaultName => StubLabel.ForRequest(Method, Path);

    /// <summary>
    /// Fills a blank name in from the request before saving, so authoring a stub never demands a
    /// name the method and path already state. See docs/spec/05-screens-and-flows.md (stub editor).
    /// </summary>
    public void ApplyDefaultName()
    {
        if (string.IsNullOrWhiteSpace(Name)) Name = DefaultName;
    }

    public static StubDraft From(Stub stub) => new()
    {
        Id = stub.Id,
        Name = stub.Name,
        Description = stub.Description ?? "",
        GroupId = stub.GroupId,
        Enabled = stub.Enabled,
        Method = stub.Request.Method,
        PathType = stub.Request.Path.Type,
        Path = stub.Request.Path.Value,
        QueryParams = stub.Request.QueryParams.Select(ParamRuleDraft.From).ToList(),
        RequestHeaders = stub.Request.Headers.Select(ParamRuleDraft.From).ToList(),
        MatchBody = stub.Request.Body is not null,
        BodyMatchType = stub.Request.Body?.Type ?? MatchRuleType.Equals,
        BodyMatchValue = stub.Request.Body?.Value ?? "",
        Status = stub.Response.Status,
        ResponseHeaders = stub.Response.Headers.Select(HeaderDraft.From).ToList(),
        Body = stub.Response.Body
    };

    public StubDraft Clone() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        GroupId = GroupId,
        Enabled = Enabled,
        Method = Method,
        PathType = PathType,
        Path = Path,
        QueryParams = QueryParams.Select(r => r with { }).ToList(),
        RequestHeaders = RequestHeaders.Select(r => r with { }).ToList(),
        MatchBody = MatchBody,
        BodyMatchType = BodyMatchType,
        BodyMatchValue = BodyMatchValue,
        Status = Status,
        ResponseHeaders = ResponseHeaders.Select(h => h with { }).ToList(),
        Body = Body
    };

    /// <summary>Field-by-field comparison, including the rule rows — the dirty check.</summary>
    public bool ValueEquals(StubDraft other)
        => Id == other.Id
           && Name == other.Name
           && Description == other.Description
           && GroupId == other.GroupId
           && Enabled == other.Enabled
           && Method == other.Method
           && PathType == other.PathType
           && Path == other.Path
           && QueryParams.SequenceEqual(other.QueryParams)
           && RequestHeaders.SequenceEqual(other.RequestHeaders)
           && MatchBody == other.MatchBody
           && (!MatchBody || (BodyMatchType == other.BodyMatchType && BodyMatchValue == other.BodyMatchValue))
           && Status == other.Status
           && ResponseHeaders.SequenceEqual(other.ResponseHeaders)
           && Body == other.Body;

    /// <summary>Blocking validation errors; empty means the draft can be saved.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Name is required.");
        if (string.IsNullOrWhiteSpace(Path)) errors.Add("Path is required.");
        else if (PathType == PathMatchType.Regex && !IsValidRegex(Path))
            errors.Add($"Path is not a valid regular expression: {Path}");

        if (Status is < 100 or > 599) errors.Add("Response status must be a valid HTTP status code (100-599).");

        foreach (var (rows, label) in new[] { (QueryParams, "Query parameter"), (RequestHeaders, "Header") })
        {
            foreach (var row in rows.Where(r => !r.IsBlank))
            {
                if (string.IsNullOrWhiteSpace(row.Name)) errors.Add($"{label} rules need a name.");
                else if (row.Type == MatchRuleType.Regex && !IsValidRegex(row.Value))
                    errors.Add($"{label} '{row.Name}' is not a valid regular expression.");
            }
        }

        if (MatchBody && BodyMatchType == MatchRuleType.Regex && !IsValidRegex(BodyMatchValue))
            errors.Add("The request body rule is not a valid regular expression.");

        if (ResponseHeaders.Any(h => !h.IsBlank && string.IsNullOrWhiteSpace(h.Name)))
            errors.Add("Response headers need a name.");

        return errors;
    }

    /// <summary>Builds a stub from the draft — the saved shape, with blank rule rows dropped.</summary>
    public Stub ToStub()
    {
        var stub = new Stub
        {
            Id = Id ?? Guid.NewGuid(),
            Request = ToRequest(),
            Response = ToResponse()
        };
        ApplyTo(stub);

        return stub;
    }

    /// <summary>Copies the draft over an existing stub, leaving its identity alone.</summary>
    public void ApplyTo(Stub stub)
    {
        stub.Name = Name.Trim();
        stub.Description = string.IsNullOrWhiteSpace(Description) ? null : Description;
        stub.GroupId = GroupId;
        stub.Enabled = Enabled;
        stub.Request = ToRequest();
        stub.Response = ToResponse();
    }

    private RequestMatcher ToRequest() => new()
    {
        Method = Method,
        Path = new PathMatcher(PathType, Path),
        QueryParams = QueryParams.Where(r => !r.IsBlank).Select(r => r.ToMatcher()).ToList(),
        Headers = RequestHeaders.Where(r => !r.IsBlank).Select(r => r.ToMatcher()).ToList(),
        Body = MatchBody
            ? new MatchRule(BodyMatchType, ParamRuleDraft.TakesValue(BodyMatchType) ? BodyMatchValue : null)
            : null
    };

    private ResponseDefinition ToResponse() => new()
    {
        Status = Status,
        Headers = ResponseHeaders.Where(h => !h.IsBlank).Select(h => h.ToHeader()).ToList(),
        Body = Body
    };

    private static bool IsValidRegex(string pattern)
    {
        try
        {
            _ = Regex.Match("", pattern);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
