using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Pho.Domain;

namespace Pho.Web;

/// <summary>
/// Terminal middleware for the mock-serving surface: turns the inbound HttpContext into a
/// domain request, resolves it against the stubs, records the received request (F5), logs
/// ambiguous matches, and writes the response.
/// </summary>
public sealed class MockServingMiddleware
{
    public MockServingMiddleware(RequestDelegate next)
    {
    }

    public async Task Invoke(
        HttpContext context,
        IStubStore store,
        IReceivedRequestLog log,
        ILogger<MockServingMiddleware> logger)
    {
        var request = await ReadRequestAsync(context.Request);
        var handling = MockServer.Handle(store.GetAll(), request);

        await log.RecordAsync(new ReceivedRequest
        {
            ReceivedAt = DateTime.UtcNow,
            Method = request.Method,
            Path = request.Path,
            Query = context.Request.QueryString.Value ?? string.Empty,
            Headers = request.HeadersOrEmpty,
            Body = request.Body ?? string.Empty,
            Outcome = handling.Match.Outcome,
            MatchedStubIds = handling.Match.MatchedStubs.Select(s => s.Id).ToList(),
            ResponseStatus = handling.Response.Status
        });

        if (handling.Match.Outcome == MatchOutcome.Ambiguous)
        {
            var stubs = string.Join(", ", handling.Match.MatchedStubs.Select(s => $"'{s.Name}' ({s.Id})"));
            logger.LogError("Ambiguous mock match for {Method} {Path}: matched stubs {Stubs}",
                request.Method, request.Path, stubs);
        }

        await WriteResponseAsync(context.Response, handling.Response);
    }

    private static async Task<HttpRequestData> ReadRequestAsync(HttpRequest request)
    {
        string? body = null;
        if (request.ContentLength is > 0 || request.Headers.ContainsKey("Transfer-Encoding"))
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
        }

        var query = request.Query.ToDictionary(kv => kv.Key, kv => (string?)kv.Value.ToString());
        var headers = request.Headers.ToDictionary(kv => kv.Key, kv => (string?)kv.Value.ToString());

        return new HttpRequestData(request.Method, request.Path.Value ?? "/", query, headers, body);
    }

    private static async Task WriteResponseAsync(HttpResponse response, MockResponse mock)
    {
        response.StatusCode = mock.Status;
        foreach (var header in mock.Headers)
        {
            if (string.Equals(header.Name, "Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
            response.Headers[header.Name] = header.Value;
        }

        await response.WriteAsync(mock.Body);
    }
}
