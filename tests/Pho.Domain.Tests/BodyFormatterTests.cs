using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class BodyFormatterTests
{
    [Fact]
    public void FormatJson_pretty_prints_valid_json()
    {
        var result = BodyFormatter.FormatJson("""{"a":1,"b":[2,3]}""");

        result.Ok.Should().BeTrue();
        result.Output.Should().Contain("\n");        // indented
        result.Output.Should().Contain("\"a\"");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void FormatJson_reports_invalid_json()
    {
        var result = BodyFormatter.FormatJson("{ not valid ]");

        result.Ok.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FormatXml_pretty_prints_valid_xml()
    {
        var result = BodyFormatter.FormatXml("<root><child>x</child></root>");

        result.Ok.Should().BeTrue();
        result.Output.Should().Contain("\n");
        result.Output.Should().Contain("<child>x</child>");
    }

    [Fact]
    public void FormatXml_reports_invalid_xml()
    {
        var result = BodyFormatter.FormatXml("<root><unclosed></root>");

        result.Ok.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Empty_input_is_not_valid()
    {
        BodyFormatter.FormatJson("   ").Ok.Should().BeFalse();
        BodyFormatter.FormatXml("").Ok.Should().BeFalse();
    }
}
