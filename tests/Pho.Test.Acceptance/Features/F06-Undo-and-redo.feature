Feature: F6 — Undo and redo configuration changes
  One global undo/redo steps backwards and forwards through the history of the whole mock
  configuration, so any change can be reversed and reapplied, in order.

  Background:
    Given these groups:
      | name     | parent   |
      | Payments |          |
      | Refunds  | Payments |
      | Archive  |          |
    And these stubs:
      | name    | path     | group    |
      | Charge  | /charge  | Payments |
      | Reverse | /reverse | Refunds  |
      | Health  | /health  |          |
    And I am on the stubs screen
    And the whole tree is expanded

  Scenario Outline: Undo reverses the most recent change — <change>
    Given I note the configuration
    When I <change>
    Then the configuration has changed
    When I undo
    Then the configuration is as I noted it

    Examples:
      | change                                                  |
      | create a stub "Ping" answering GET /ping                |
      | change the response body of "Health" to "still alive"   |
      | delete the stub "Health"                                |
      | disable the stub "Health"                               |
      | move the stub "Health" into "Archive"                   |
      | add a group "Disputes" inside "Payments"                |
      | move the group "Refunds" to the top level               |
      | delete the group "Archive"                              |

  Scenario: Undo steps back through several changes in order
    Given I note the configuration
    When I disable the stub "Health"
    And I delete the group "Archive"
    And I undo
    Then the stub tree shows "Archive"
    And the stub "Health" is disabled
    When I undo
    Then the configuration is as I noted it

  Scenario: Redo reapplies an undone change
    When I delete the stub "Health"
    And I note the configuration
    And I undo
    Then the stub tree shows "Health"
    When I redo
    Then the configuration is as I noted it
    And the stub tree does not show "Health"

  Scenario: Undoing a cascading group delete restores the whole subtree in one step
    Given the stub "Reverse" has a header rule "X-Reason" equals "fraud"
    And I note the configuration
    When I delete the group "Payments"
    Then the stub tree does not show "Charge"
    When I undo
    Then the configuration is as I noted it
    And requests are answered like this:
      | method | path     | headers         | answer  |
      | GET    | /charge  |                 | Charge  |
      | GET    | /reverse | X-Reason: fraud | Reverse |

  Scenario: A new change after undoing discards what could have been redone
    When I delete the stub "Health"
    And I undo
    Then redo is offered
    When I disable the stub "Charge"
    Then redo is not offered
