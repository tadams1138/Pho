Feature: F9 — Spell-check hints
  Free text in the stub editor is checked for possible misspellings, as a hint only.

  Scenario: The editor's free-text fields offer spell-check hints
    Given I am on the stubs screen
    When I start a new stub
    And I add a body rule equals
    Then these fields offer spell-check hints:
      | field              |
      | name               |
      | description        |
      | request body value |
      | response body      |

  Scenario: A flagged word never blocks saving and is stored exactly as typed
    Given I am on the stubs screen
    When I start a new stub
    And I set the name to "Recieve ordrs"
    And I set the description to "Teh PSP callbak for acme-pay"
    And I set the path to "/callbak"
    And I type 'Paymnt acepted' into the response body
    And I save the stub
    Then a stub named "Recieve ordrs" is stored
    And its description is "Teh PSP callbak for acme-pay"
    And a client sending GET /callbak receives "Paymnt acepted"
