using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class BasicAuthTests
{
    // The credential "valid userName:valid password", as it appears in real mock definitions.
    private const string Encoded = "Basic dmFsaWQgdXNlck5hbWU6dmFsaWQgcGFzc3dvcmQ=";

    [Fact]
    public void Encodes_a_user_id_and_password_as_a_basic_credential()
    {
        BasicAuth.Encode("valid userName", "valid password").Should().Be(Encoded);
    }

    [Fact]
    public void Decodes_a_basic_credential_back_to_its_parts()
    {
        var credentials = BasicAuth.Decode(Encoded);

        credentials.Should().NotBeNull();
        credentials!.UserId.Should().Be("valid userName");
        credentials.Password.Should().Be("valid password");
    }

    [Fact]
    public void Round_trips_a_password_containing_a_colon()
    {
        var credentials = BasicAuth.Decode(BasicAuth.Encode("user", "pa:ss:word"));

        credentials!.UserId.Should().Be("user");
        credentials.Password.Should().Be("pa:ss:word", "only the first colon separates the two");
    }

    [Fact]
    public void Round_trips_non_ascii_credentials()
    {
        var credentials = BasicAuth.Decode(BasicAuth.Encode("üser", "pässword"));

        credentials!.UserId.Should().Be("üser");
        credentials.Password.Should().Be("pässword");
    }

    [Fact]
    public void Encodes_an_empty_password()
    {
        BasicAuth.Decode(BasicAuth.Encode("user", "")).Should().Be(new BasicCredentials("user", ""));
    }

    [Fact]
    public void The_scheme_is_matched_case_insensitively_and_extra_spacing_is_tolerated()
    {
        BasicAuth.Decode("  basic   dXNlcjpwYXNz  ").Should().Be(new BasicCredentials("user", "pass"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Bearer dXNlcjpwYXNz")]                 // another scheme
    [InlineData("Basic")]                               // no credential at all
    [InlineData("Basic not-base-64!!")]                 // undecodable
    [InlineData("Basic bm8tY29sb24taGVyZQ==")]          // decodes to "no-colon-here"
    [InlineData("dXNlcjpwYXNz")]                        // base64, but no scheme
    public void Anything_that_is_not_a_basic_credential_does_not_decode(string? headerValue)
    {
        BasicAuth.Decode(headerValue).Should().BeNull();
        BasicAuth.Describe(headerValue).Should().BeNull();
    }

    [Fact]
    public void Describes_a_credential_for_a_tooltip()
    {
        BasicAuth.Describe(Encoded).Should().Be("User ID: valid userName · Password: valid password");
    }

    [Fact]
    public void Describes_an_empty_password_rather_than_showing_nothing()
    {
        BasicAuth.Describe(BasicAuth.Encode("user", "")).Should().Be("User ID: user · Password: (empty)");
    }
}
