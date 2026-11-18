Feature: F11 — Navigate the stub tree from the keyboard
  The stub tree can be operated without a mouse, and the editor follows the keyboard.

  Background:
    Given these groups:
      | name  | parent |
      | Admin |        |
      | Shop  |        |
      | Cart  | Shop   |
    And these stubs:
      | name     | path      | group |
      | Users    | /users    | Admin |
      | Basket   | /basket   | Cart  |
      | Checkout | /checkout | Shop  |
      | Health   | /health   |       |
    And I am on the stubs screen
    And the whole tree is expanded

  # The expanded tree, in display order:
  #   Admin > Users, Shop > Cart > Basket, Shop > Checkout, Health

  Scenario: Arrow keys move through the rows in display order, selecting each and scrolling it into view
    When I focus the tree and press "Down"
    Then the active row is "Admin"
    And the selection is:
      | row   |
      | Admin |
    When I press "Down" 3 times
    Then the active row is "Cart"
    And the selection is:
      | row  |
      | Cart |
    And "Cart" has been scrolled into view
    When I press "Up"
    Then the active row is "Shop"

  Scenario: Home and End jump to the first and last rows
    When I focus the tree and press "End"
    Then the active row is "Health"
    When I press "Home"
    Then the active row is "Admin"

  Scenario: Left and right collapse and expand the active group
    Given the active row is "Shop"
    When I press "Left"
    Then the group "Shop" is collapsed
    And the stub tree does not show "Checkout"
    When I press "Right"
    Then the group "Shop" is expanded
    And the stub tree shows "Checkout"

  Scenario: Right on an expanded group moves into its first child
    Given the active row is "Shop"
    When I press "Right"
    Then the active row is "Cart"

  Scenario Outline: Left on a stub moves to its parent group — <row>
    Given the active row is "<row>"
    When I press "Left"
    Then the active row is "<parent>"

    Examples:
      | row      | parent |
      | Checkout | Shop   |
      | Basket   | Cart   |
      | Users    | Admin  |

  Scenario: Left on a collapsed group moves to its parent group
    Given the active row is "Cart"
    When I press "Left"
    Then the group "Cart" is collapsed
    And the active row is "Cart"
    When I press "Left"
    Then the active row is "Shop"

  Scenario: Space takes the active row out of the selection, or adds it back
    Given the active row is "Users"
    When I ctrl-click "Health"
    And I press "Space"
    Then the selection is:
      | row   |
      | Users |
    When I press "Space"
    Then the selection is:
      | row    |
      | Users  |
      | Health |

  Scenario: Delete raises the same confirmation as deleting the selection
    Given the active row is "Health"
    When I press "Delete"
    Then I am asked to confirm deleting "1 stub"
    When I confirm the deletion
    Then the stub tree does not show "Health"

  Rule: The editor follows the selection

    Scenario: Exactly one selected stub is shown in the editor, however it was selected
      When I focus the tree and press "End"
      Then the editor shows the stub "Health"
      When I select "Users"
      Then the editor shows the stub "Users"

    Scenario Outline: Selecting <selection> shows no editor
      When I <action>
      Then no editor is shown

      Examples:
        | selection    | action                                  |
        | a group      | select "Shop"                           |
        | several rows | select "Users" and ctrl-click "Health"  |
        | nothing      | select "Users" and ctrl-click "Users"   |

    Scenario: Moving off a stub with unsaved changes by keyboard warns first
      Given the active row is "Checkout"
      When I set the response body to "changed"
      And I press "Down"
      Then I am warned about unsaved changes
      And the editor shows the stub "Checkout"
      When I choose to discard
      Then the active row is "Health"
      And the editor shows the stub "Health"
