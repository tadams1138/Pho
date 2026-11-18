namespace Pho.Test.Acceptance.Support;

/// <summary>The most recent exchange a client had with Pho.</summary>
public sealed class LastExchange
{
    public HttpResponseMessage? Response { get; set; }

    public string Body { get; set; } = "";

    public HttpResponseMessage RequireResponse()
        => Response ?? throw new InvalidOperationException("No request has been sent yet.");
}

public static class MockRequests
{
    /// <summary>"Name: value; Name2: value2" as written in feature tables.</summary>
    public static IEnumerable<(string Name, string Value)> ParseHeaders(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;

        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = part.IndexOf(':');
            yield return (part[..colon].Trim(), part[(colon + 1)..].Trim());
        }
    }

    public static HttpRequestMessage Build(string method, string path, IEnumerable<(string Name, string Value)>? headers = null, string? body = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null) request.Content = new StringContent(body);

        foreach (var (name, value) in headers ?? [])
        {
            if (!request.Headers.TryAddWithoutValidation(name, value))
            {
                request.Content ??= new StringContent("");
                request.Content.Headers.Remove(name);
                request.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return request;
    }
}
