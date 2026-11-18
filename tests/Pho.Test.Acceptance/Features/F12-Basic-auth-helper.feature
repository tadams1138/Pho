Feature: F12 — Basic auth credential helper
  Mocked services are often protected by HTTP Basic authentication, whose header value is a
  base64 blob. The stub editor encodes and decodes it, so nobody has to do that by hand.

  Rule: Building a credential rule

    Scenario: Build an Authorization rule from a user id and password
      Given these stubs:
        | name    | path     |
        | Account | /account |
      And the stub "Account" has a header rule "Authorization" equals "Bearer old-token"
      And I am on the stubs screen
      When I select "Account"
      And I enter the user id "alice" and the password "s3cret" in the basic auth helper
      And I apply the basic auth helper
      Then the stub has exactly one header rule for "Authorization"
      And that rule is a REGEX rule with the value "^(?i:Basic)\s+YWxpY2U6czNjcmV0$"
      When I save the stub
      Then requests are answered like this:
        | method | path     | headers                               | answer  |
        | GET    | /account | Authorization: Basic YWxpY2U6czNjcmV0 | Account |
        | GET    | /account | Authorization: Bearer old-token       | no stub |
        | GET    | /account |                                       | no stub |

    Scenario: The helper can add the rule to a stub that has no header rules yet
      Given I am on the stubs screen
      When I start a new stub
      And I enter the user id "alice" and the password "s3cret" in the basic auth helper
      And I apply the basic auth helper
      Then the stub has exactly one header rule for "Authorization"

  Rule: Matching a credential built by the helper

    Background:
      Given these stubs:
        | name    | path     |
        | Account | /account |
      And the stub "Account" has a Basic credential for "alice" with password "s3cret", built by the helper

    Scenario: The scheme word matches in any case, the credential only exactly
      Then requests are answered like this:
        | method | path     | headers                               | answer  |
        | GET    | /account | Authorization: Basic YWxpY2U6czNjcmV0 | Account |
        | GET    | /account | Authorization: BASIC YWxpY2U6czNjcmV0 | Account |
        | GET    | /account | Authorization: basic YWxpY2U6czNjcmV0 | Account |
        | GET    | /account | Authorization: Basic YWxpY2U6d3Jvbmc= | no stub |
        | GET    | /account | Authorization: Basic ywXPy2U6CZNJCMV0 | no stub |

    Scenario Outline: Only the real scheme, with nothing around it, is accepted — <case>
      When a client sends GET /account with header "Authorization" set to "<value>"
      Then the caller is told that no stub matched

      Examples:
        | case               | value                         |
        | misspelled scheme  | BASSIC YWxpY2U6czNjcmV0       |
        | truncated scheme   | Basi YWxpY2U6czNjcmV0         |
        | extended scheme    | Basicc YWxpY2U6czNjcmV0       |
        | another scheme     | Bearer YWxpY2U6czNjcmV0       |
        | leading junk       | xBasic YWxpY2U6czNjcmV0       |
        | trailing junk      | Basic YWxpY2U6czNjcmV0x       |

  Rule: The credential is matched literally

    Scenario: A credential whose encoding contains pattern characters still matches only itself
      Given these stubs:
        | name  | path   |
        | Vault | /vault |
      And the stub "Vault" has a Basic credential for "bob" with password "x>y>z", built by the helper
      # base64 of "bob:x>y>z" is Ym9iOng+eT56, and "+" means "one or more" in a pattern.
      Then the stub's Authorization rule contains the credential "Ym9iOng+eT56" as literal text
      And requests are answered like this:
        | method | path   | headers                            | answer  |
        | GET    | /vault | Authorization: Basic Ym9iOng+eT56  | Vault   |
        | GET    | /vault | Authorization: Basic Ym9iOnggeT56  | no stub |
        | GET    | /vault | Authorization: Basic Ym9iOngeT56   | no stub |

  Rule: Reading a credential back

    Scenario Outline: Hovering over a credential shows who it is for — <form>
      Given these stubs:
        | name    | path     |
        | Account | /account |
      And the stub "Account" has a header rule "Authorization" <rule>
      And I am on the stubs screen
      When I select "Account"
      And I hover over the value of the header rule "Authorization"
      Then I read "User ID: alice · Password: s3cret"
      And the basic auth helper shows the user id "alice" and the password "s3cret"

      Examples:
        | form                         | rule                                          |
        | a pattern the helper built   | regex "^(?i:Basic)\s+YWxpY2U6czNjcmV0$"       |
        | a plain value written by hand| equals "Basic YWxpY2U6czNjcmV0"               |
        | a plain value, other casing  | equals "basic YWxpY2U6czNjcmV0"               |

    Scenario: Re-applying the helper to an existing credential replaces it
      Given these stubs:
        | name    | path     |
        | Account | /account |
      And the stub "Account" has a header rule "Authorization" equals "Basic YWxpY2U6czNjcmV0"
      And I am on the stubs screen
      When I select "Account"
      And I enter the user id "alice" and the password "n3w-secret" in the basic auth helper
      And I apply the basic auth helper
      And I save the stub
      Then the stub has exactly one header rule for "Authorization"
      And requests are answered like this:
        | method | path     | headers                                     | answer  |
        | GET    | /account | Authorization: Basic YWxpY2U6bjN3LXNlY3JldA== | Account |
        | GET    | /account | Authorization: Basic YWxpY2U6czNjcmV0       | no stub |

    Scenario Outline: A value that is not a credential is left alone — <case>
      Given these stubs:
        | name    | path     |
        | Account | /account |
      And the stub "Account" has a header rule "Authorization" <rule>
      And I am on the stubs screen
      When I select "Account"
      And I hover over the value of the header rule "Authorization"
      Then no decoded reading is offered
      And the basic auth helper is empty

      Examples:
        | case                         | rule                                 |
        | another scheme               | equals "Bearer YWxpY2U6czNjcmV0"     |
        | a pattern of another shape   | regex "^Token\s+YWxpY2U6czNjcmV0$"   |
        | invalid base64               | equals "Basic !!not-base64!!"        |
        | decoded text without a colon | equals "Basic bm9jb2xvbg=="          |
