Feature: F8 — Body formatting and validation (JSON and XML)
  The stub editor helps authors work with structured bodies, in both the request body rule and
  the response body. The help is advisory: a body may hold any content at all.

  Background:
    Given I am on the stubs screen
    And I start a new stub

  Scenario Outline: Format <format> in the <field> in place
    When I add a body rule equals
    And I type '<input>' into the <field>
    And I format the <field> as <format>
    Then the <field> reads:
      """
      <formatted>
      """

    Examples:
      | field              | format | input                          | formatted                                             |
      | response body      | JSON   | {"id":7,"tags":["a","b"]}      | {\n  "id": 7,\n  "tags": [\n    "a",\n    "b"\n  ]\n} |
      | request body value | JSON   | {"id":7,"tags":["a","b"]}      | {\n  "id": 7,\n  "tags": [\n    "a",\n    "b"\n  ]\n} |
      | response body      | XML    | <order><id>7</id><qty>2</qty></order> | <order>\n  <id>7</id>\n  <qty>2</qty>\n</order> |
      | request body value | XML    | <order><id>7</id><qty>2</qty></order> | <order>\n  <id>7</id>\n  <qty>2</qty>\n</order> |

  Scenario Outline: Validate well-formed <format> in the <field>
    When I add a body rule equals
    And I type '<input>' into the <field>
    And I validate the <field> as <format>
    Then the <field> is reported as well-formed

    Examples:
      | field              | format | input                         |
      | response body      | JSON   | {"id": 7}                     |
      | request body value | JSON   | [1, 2, 3]                     |
      | response body      | XML    | <order><id>7</id></order>     |
      | request body value | XML    | <ok/>                         |

  Scenario Outline: Validating malformed <format> shows the error and where it is
    When I add a body rule equals
    And I type '<input>' into the <field>
    And I validate the <field> as <format>
    Then the <field> is reported as malformed, with the error's location

    Examples:
      | field              | format | input                         |
      | response body      | JSON   | {"id": 7,,}                   |
      | request body value | JSON   | {"id": }                      |
      | response body      | XML    | <order><id>7</order>          |
      | request body value | XML    | <a><b></a>                    |

  Scenario: Formatting malformed content leaves it untouched
    When I type '{"id": 7,,}' into the response body
    And I format the response body as JSON
    Then the response body reads:
      """
      {"id": 7,,}
      """
    And the response body is reported as malformed, with the error's location

  Scenario: Bodies that are neither JSON nor XML can still be saved
    When I set the name to "Broken on purpose"
    And I set the path to "/broken"
    And I type '{"truncated": ' into the response body
    And I validate the response body as JSON
    And I save the stub
    Then a stub named "Broken on purpose" is stored
    And a client sending GET /broken receives "{"truncated": "
