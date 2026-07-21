using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Pho.Domain;

namespace Pho.Web;

/// <summary>
/// Terminal middleware for the mock-serving surface: turns the inbound HttpContext into a
/// domain request, resolves it against the stubs, and writes the resulting response.
/// (Admin-UI port branching is added with the Blazor slice; today the whole app is the mock port.)
/// </summary>
public sealed class MockServingMiddleware
{
    // Middleware requires this ctor even though the request is terminal here.
    public MockServingMiddleware(RequestDelegate next)
    {
    }

    public async Task Invoke(HttpContext context, IStubStore store)
    {
        var request = await ReadRequestAsync(context.Request);
        var handling = MockServer.Handle(store.GetAll(), request);
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
