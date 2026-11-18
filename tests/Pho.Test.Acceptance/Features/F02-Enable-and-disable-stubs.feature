Feature: F2 — Enable and disable stubs
  A stub can be switched off and on without being deleted or rewritten, so a test author can
  switch between alternative responses for the same endpoint.

  Scenario: A disabled stub is kept but no longer answers
    Given these stubs:
      | name   | path    |
      | Orders | /orders |
    And I am on the stubs screen
    When I select "Orders"
    And I disable the selection
    Then the stub "Orders" is disabled
    And the stub tree shows "Orders"
    And "Orders" is marked disabled
    And a client sending GET /orders is told that no stub matched

  Scenario: Requests a disabled stub would have answered fall through to the other stubs
    Given these stubs:
      | name     | path      | match    |
      | Specific | /orders/1 | EXACT    |
      | General  | /orders/* | WILDCARD |
    And I am on the stubs screen
    When I select "Specific"
    And I disable the selection
    And a client sends GET /orders/1
    Then the request is answered by "General"

  Scenario: An enabled stub answers again
    Given these stubs:
      | name   | path    | enabled |
      | Orders | /orders | no      |
    And I am on the stubs screen
    When I select "Orders"
    And I enable the selection
    Then the stub "Orders" is enabled
    And "Orders" is not marked disabled
    And a client sending GET /orders receives "Orders"

  Scenario: Switch an endpoint between alternative responses
    Given these stubs:
      | name    | path    | status | body        | enabled |
      | Healthy | /status | 200    | all good    | yes     |
      | Broken  | /status | 503    | maintenance | no      |
    And I am on the stubs screen
    When I select "Healthy"
    And I disable the selection
    And I select "Broken"
    And I enable the selection
    And a client sends GET /status
    Then the request is answered by "Broken"
    And the caller receives status 503
    And the response body is "maintenance"
    When I select "Broken"
    And I disable the selection
    And I select "Healthy"
    And I enable the selection
    And a client sends GET /status
    Then the request is answered by "Healthy"
    And the response body is "all good"

  Scenario: Enable or disable everything a selection covers, as one undoable change
    Given these groups:
      | name | parent |
      | Shop |        |
      | Cart | Shop   |
    And these stubs:
      | name     | path      | group |
      | Catalog  | /catalog  | Shop  |
      | Basket   | /basket   | Cart  |
      | Health   | /health   |       |
      | Untouched| /untouched|       |
    And I am on the stubs screen
    When I select "Shop"
    And I ctrl-click "Health"
    And I disable the selection
    Then the stub "Catalog" is disabled
    And the stub "Basket" is disabled
    And the stub "Health" is disabled
    And the stub "Untouched" is enabled
    When I undo
    Then the stub "Catalog" is enabled
    And the stub "Basket" is enabled
    And the stub "Health" is enabled

  Scenario: Enable and disable are offered only when they would change something
    Given these stubs:
      | name | path | enabled |
      | On   | /on  | yes     |
      | Off  | /off | no      |
    And I am on the stubs screen
    When I select "On"
    Then the enable action is not offered
    And the disable action is offered
    When I select "Off"
    Then the enable action is offered
    And the disable action is not offered
    When I ctrl-click "On"
    Then the enable action is offered
    And the disable action is offered
