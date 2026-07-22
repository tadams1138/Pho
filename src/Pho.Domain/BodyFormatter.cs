using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Pho.Domain;

/// <summary>Outcome of a format/validate operation: success plus pretty output, or an error.</summary>
public sealed record FormatResult(bool Ok, string Output, string? Error);

/// <summary>
/// Formats and validates request/response bodies as JSON or XML for the stub editor (F9).
/// A successful result carries the pretty-printed output; a failure carries a parse error.
/// </summary>
public static class BodyFormatter
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    public static FormatResult FormatJson(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new FormatResult(false, input, "Empty content.");

        try
        {
            using var document = JsonDocument.Parse(input);
            var output = JsonSerializer.Serialize(document.RootElement, IndentedJson);
            return new FormatResult(true, output, null);
        }
        catch (JsonException ex)
        {
            return new FormatResult(false, input, ex.Message);
        }
    }

    public static FormatResult FormatXml(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new FormatResult(false, input, "Empty content.");

        try
        {
            var document = XDocument.Parse(input);
            return new FormatResult(true, document.ToString(), null);
        }
        catch (XmlException ex)
        {
            return new FormatResult(false, input, ex.Message);
        }
    }
}
