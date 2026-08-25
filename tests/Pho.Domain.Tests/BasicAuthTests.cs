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
    public void Builds_a_pattern_that_spells_the_scheme_out_and_anchors_both_ends()
    {
        // Arrange / Act
        var pattern = BasicAuth.EncodePattern("user", "pass");

        // Assert
        pattern.Should().Be(@"^(?i:Basic)\s+dXNlcjpwYXNz$");
    }

    [Theory]
    [InlineData("Basic dXNlcjpwYXNz", true)]
    [InlineData("BASIC dXNlcjpwYXNz", true)]            // schemes are case-insensitive tokens
    [InlineData("basic dXNlcjpwYXNz", true)]
    [InlineData("Basic  dXNlcjpwYXNz", true)]           // more than one space is still valid
    [InlineData("BASSIC dXNlcjpwYXNz", false)]          // a misspelling is not a scheme
    [InlineData("Basi dXNlcjpwYXNz", false)]
    [InlineData("Basicc dXNlcjpwYXNz", false)]
    [InlineData("Bearer dXNlcjpwYXNz", false)]          // a different scheme entirely
    [InlineData("Basic DXNLCJPWQXN6", false)]           // base64 is case-significant: wrong credential
    [InlineData("xBasic dXNlcjpwYXNz", false)]          // anchored, so leading junk fails
    [InlineData("Basic dXNlcjpwYXNz extra", false)]     // and trailing junk too
    public void The_pattern_folds_the_schemes_case_and_nothing_else(string headerValue, bool expected)
    {
        // Arrange
        var rule = new MatchRule(MatchRuleType.Regex, BasicAuth.EncodePattern("user", "pass"));

        // Act
        var matched = rule.Matches(headerValue);

        // Assert
        matched.Should().Be(expected);
    }

    [Fact]
    public void A_credential_containing_a_regex_metacharacter_is_escaped_into_the_pattern()
    {
        // Arrange — base64 emits '+', which unescaped would quantify the preceding character
        var credential = BasicAuth.Encode("user", "~aa");
        credential.Should().Be("Basic dXNlcjp+YWE=", "this is the case the escaping exists for");

        // Act
        var rule = new MatchRule(MatchRuleType.Regex, BasicAuth.EncodePattern("user", "~aa"));

        // Assert
        rule.Matches("Basic dXNlcjp+YWE=").Should().BeTrue("the credential is matched as literal text");
        rule.Matches("Basic dXNlcjpppYWE=").Should().BeFalse("'p+' must not become a quantifier");
    }

    [Fact]
    public void Decodes_a_pattern_the_helper_built()
    {
        // Arrange
        var pattern = BasicAuth.EncodePattern("valid userName", "valid password");

        // Act
        var credentials = BasicAuth.Decode(pattern);

        // Assert
        credentials.Should().Be(new BasicCredentials("valid userName", "valid password"));
    }

    [Fact]
    public void Decodes_a_pattern_whose_credential_was_escaped()
    {
        // Arrange
        var pattern = BasicAuth.EncodePattern("user", "~aa");

        // Act
        var credentials = BasicAuth.Decode(pattern);

        // Assert — the escaping is reversed, not carried into the decoded text
        credentials.Should().Be(new BasicCredentials("user", "~aa"));
    }

    [Theory]
    [InlineData(@"^(?i:Bearer)\s+dXNlcjpwYXNz$")]        // right shape, wrong scheme
    [InlineData(@"^(?i:Basic)\s+not-base-64!!$")]        // undecodable credential
    [InlineData(@"^(?i:Basic)\s+dXNlcjpwYXNz")]          // unanchored: not what the helper builds
    [InlineData(@"\s+dXNlcjpwYXNz$")]
    [InlineData(@"^Basic\s+dXNlcjpwYXNz$")]              // no case folding: not the helper's shape
    public void A_regex_that_is_not_the_helpers_shape_does_not_decode(string headerValue)
    {
        // Act / Assert
        BasicAuth.Decode(headerValue).Should().BeNull();
        BasicAuth.Describe(headerValue).Should().BeNull();
    }

    [Fact]
    public void Describes_a_credential_for_a_tooltip()
    {
        BasicAuth.Describe(Encoded).Should().Be("User ID: valid userName · Password: valid password");
    }

    [Fact]
    public void Describes_a_credential_held_as_a_pattern()
    {
        BasicAuth.Describe(BasicAuth.EncodePattern("user", "pass"))
            .Should().Be("User ID: user · Password: pass");
    }

    [Fact]
    public void Describes_an_empty_password_rather_than_showing_nothing()
    {
        BasicAuth.Describe(BasicAuth.Encode("user", "")).Should().Be("User ID: user · Password: (empty)");
    }
}
