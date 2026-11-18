Feature: F4 — Match requests and serve responses
  Pho answers every request sent to its mock-serving surface from the stubs that match it.
  Stubs have no priority: exactly one enabled match answers, none is "not found", and more than
  one is a configuration error.

  Scenario: A matching stub answers with its status, headers and body
    Given these stubs:
      | name    | method | path    | status | body       |
      | Created | POST   | /orders | 201    | {"id": 7}  |
    And the stub "Created" has a response header "Location" set to "/orders/7"
    When a client sends POST /orders
    Then the caller receives status 201
    And the response body is "{"id": 7}"
    And the response carries header "Location" with value "/orders/7"

  Scenario Outline: Path matching — <type> "<pattern>" against <path>
    Given these stubs:
      | name | path      | match  |
      | Stub | <pattern> | <type> |
    Then requests are answered like this:
      | method | path   | answer   |
      | GET    | <path> | <answer> |

    Examples:
      | type     | pattern            | path               | answer  |
      | EXACT    | /orders            | /orders            | Stub    |
      | EXACT    | /orders            | /orders/1          | no stub |
      | EXACT    | /orders            | /Orders            | no stub |
      | WILDCARD | /orders/*          | /orders/42         | Stub    |
      | WILDCARD | /orders/*          | /orders/42/items   | no stub |
      | WILDCARD | /orders/{id}/items | /orders/42/items   | Stub    |
      | WILDCARD | /orders/{id}/items | /orders/42/refunds | no stub |
      | REGEX    | ^/orders/\d+$      | /orders/42         | Stub    |
      | REGEX    | ^/orders/\d+$      | /orders/abc        | no stub |

  Scenario Outline: Method matching — a <method> stub against a <sent> request
    Given these stubs:
      | name | method   | path |
      | Stub | <method> | /m   |
    Then requests are answered like this:
      | method | path | answer   |
      | <sent> | /m   | <answer> |

    Examples:
      | method | sent   | answer  |
      | GET    | GET    | Stub    |
      | GET    | POST   | no stub |
      | DELETE | DELETE | Stub    |
      | ANY    | PATCH  | Stub    |
      | ANY    | GET    | Stub    |

  Scenario Outline: Query parameter, header and body rules — EQUALS, CONTAINS, REGEX, PRESENT and ABSENT
    Given these stubs:
      | name | method | path   |
      | Stub | POST   | /rules |
    And the stub "Stub" has <rule>
    When a client sends <request>
    Then the request is answered by <answer>

    Examples:
      | rule                                         | request                                                   | answer  |
      | a header rule "X-Tier" equals "gold"         | POST /rules with header "X-Tier" set to "gold"            | Stub    |
      | a header rule "X-Tier" equals "gold"         | POST /rules with header "X-Tier" set to "golden"          | no stub |
      | a header rule "X-Tier" contains "old"        | POST /rules with header "X-Tier" set to "golden"          | Stub    |
      | a header rule "X-Tier" contains "old"        | POST /rules with header "X-Tier" set to "silver"          | no stub |
      | a header rule "X-Tier" regex "^g.+d$"        | POST /rules with header "X-Tier" set to "gold"            | Stub    |
      | a header rule "X-Tier" regex "^g.+d$"        | POST /rules with header "X-Tier" set to "golden"          | no stub |
      | a header rule "X-Tier" present               | POST /rules with header "X-Tier" set to "anything"        | Stub    |
      | a header rule "X-Tier" present               | POST /rules                                               | no stub |
      | a header rule "X-Tier" absent                | POST /rules                                               | Stub    |
      | a header rule "X-Tier" absent                | POST /rules with header "X-Tier" set to "anything"        | no stub |
      | a query parameter rule "tier" equals "gold"  | POST /rules with query parameter "tier" set to "gold"     | Stub    |
      | a query parameter rule "tier" equals "gold"  | POST /rules with query parameter "tier" set to "golden"   | no stub |
      | a query parameter rule "tier" contains "old" | POST /rules with query parameter "tier" set to "golden"   | Stub    |
      | a query parameter rule "tier" contains "old" | POST /rules with query parameter "tier" set to "silver"   | no stub |
      | a query parameter rule "tier" regex "^\d+$"  | POST /rules with query parameter "tier" set to "42"       | Stub    |
      | a query parameter rule "tier" regex "^\d+$"  | POST /rules with query parameter "tier" set to "4x"       | no stub |
      | a query parameter rule "tier" present        | POST /rules with query parameter "tier" set to "anything" | Stub    |
      | a query parameter rule "tier" present        | POST /rules                                               | no stub |
      | a query parameter rule "tier" absent         | POST /rules                                               | Stub    |
      | a query parameter rule "tier" absent         | POST /rules with query parameter "tier" set to "anything" | no stub |
      | a body rule equals "{"a":1}"                 | POST /rules with body "{"a":1}"                           | Stub    |
      | a body rule equals "{"a":1}"                 | POST /rules with body "{"a":2}"                           | no stub |
      | a body rule contains "needle"                | POST /rules with body "hay needle hay"                    | Stub    |
      | a body rule contains "needle"                | POST /rules with body "haystack"                          | no stub |
      | a body rule regex "^id=\d+$"                 | POST /rules with body "id=12"                             | Stub    |
      | a body rule regex "^id=\d+$"                 | POST /rules with body "id=x"                              | no stub |
      | a body rule present                          | POST /rules with body "anything"                          | Stub    |
      | a body rule present                          | POST /rules                                               | no stub |
      | a body rule absent                           | POST /rules                                               | Stub    |
      | a body rule absent                           | POST /rules with body "anything"                          | no stub |

  Scenario: A request matches only when every criterion is satisfied
    Given these stubs:
      | name  | method | path   |
      | Strict| PUT    | /items |
    And the stub "Strict" has a query parameter rule "version" equals "2"
    And the stub "Strict" has a header rule "X-Client" equals "app"
    And the stub "Strict" has a body rule contains "item"
    Then requests are answered like this:
      | method | path             | headers       | body       | answer  |
      | PUT    | /items?version=2 | X-Client: app | new item   | Strict  |
      | POST   | /items?version=2 | X-Client: app | new item   | no stub |
      | PUT    | /other?version=2 | X-Client: app | new item   | no stub |
      | PUT    | /items?version=1 | X-Client: app | new item   | no stub |
      | PUT    | /items?version=2 | X-Client: web | new item   | no stub |
      | PUT    | /items?version=2 | X-Client: app | new thing  | no stub |

  Rule: Case sensitivity

    Scenario: Header names compare case-insensitively, always
      Given these stubs:
        | name | method | path  |
        | Json | POST   | /json |
      And the stub "Json" has a header rule "Content-Type" equals "application/json"
      When a client sends POST /json with header "content-type" set to "application/json"
      Then the request is answered by "Json"

    Scenario Outline: Values compare case-sensitively unless the rule says otherwise — <type>
      Given these stubs:
        | name  | path   |
        | Exact | /exact |
      And the stub "Exact" has a header rule "X-Mode" <type> "Live"
      Then requests are answered like this:
        | method | path   | headers      | answer  |
        | GET    | /exact | X-Mode: Live | Exact   |
        | GET    | /exact | X-Mode: live | no stub |
        | GET    | /exact | X-Mode: LIVE | no stub |

      Examples:
        | type     |
        | equals   |
        | contains |

    Scenario: A case-significant value such as a Basic credential is not accepted in another case
      Given these stubs:
        | name   | path    |
        | Secret | /secret |
      And the stub "Secret" has a header rule "Authorization" equals "Basic YWxpY2U6czNjcmV0"
      Then requests are answered like this:
        | method | path    | headers                              | answer  |
        | GET    | /secret | Authorization: Basic YWxpY2U6czNjcmV0 | Secret  |
        | GET    | /secret | Authorization: Basic ywXPy2U6CZNJCMV0 | no stub |

  Rule: More than one enabled match is an error, not a choice

    Background:
      Given these stubs:
        | name        | path      | match    |
        | Order one   | /orders/1 | EXACT    |
        | Any order   | /orders/* | WILDCARD |

    Scenario: An ambiguous match answers with a server error naming the stubs involved
      When a client sends GET /orders/1
      Then the caller receives a server error naming "Order one" and "Any order"
      And the response body is not the response of either stub

    Scenario: An ambiguous match is logged as an error naming the stubs and the request
      When a client sends GET /orders/1
      Then an error is logged naming "Order one", "Any order" and the request "GET /orders/1"

    Scenario: An ambiguous match is flagged in the received-requests view
      When a client sends GET /orders/1
      And I open the received-requests view
      Then the request "GET /orders/1" is flagged as matching 2 stubs

  Scenario: A request no enabled stub matches is answered with not found
    Given these stubs:
      | name   | path    |
      | Orders | /orders |
    When a client sends GET /customers
    Then the caller is told that no stub matched

  Scenario: Disabled stubs are never used
    Given these stubs:
      | name   | path    | enabled |
      | Orders | /orders | no      |
    When a client sends GET /orders
    Then the caller is told that no stub matched
