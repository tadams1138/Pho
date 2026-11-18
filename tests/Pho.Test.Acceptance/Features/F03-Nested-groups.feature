Feature: F3 — Organize stubs into nested groups
  Stubs are arranged in a tree of user-defined groups, purely for organisation.

  Rule: Creating, renaming and deleting groups

    Scenario: Create a group at the top level and inside another group
      Given I am on the stubs screen
      When I add a group "Payments"
      Then the stub tree shows "Payments"
      And "Payments" is shown at the top level
      When I add a group "Refunds" inside "Payments"
      Then "Refunds" is shown inside "Payments"

    Scenario: Delete an empty group
      Given these groups:
        | name     |
        | Payments |
        | Archive  |
      And I am on the stubs screen
      When I select "Archive"
      And I delete the selection
      And I confirm the deletion
      Then the stub tree does not show "Archive"
      And the stub tree shows "Payments"

  Rule: Groups nest to any depth, never inside themselves

    Scenario: Groups nest to any depth and are shown as a tree
      Given these groups:
        | name    | parent |
        | Level 1 |        |
        | Level 2 | Level 1 |
        | Level 3 | Level 2 |
        | Level 4 | Level 3 |
      And these stubs:
        | name | path  | group   |
        | Deep | /deep | Level 4 |
      And I am on the stubs screen
      When I expand all groups
      Then the stub tree shows exactly:
        | row     | level |
        | Level 1 | 0     |
        | Level 2 | 1     |
        | Level 3 | 2     |
        | Level 4 | 3     |
        | Deep    | 4     |

    Scenario: Move a group into another group
      Given these groups:
        | name   |
        | Shop   |
        | Basket |
      And I am on the stubs screen
      When I drag "Basket" onto "Shop"
      Then "Basket" is shown inside "Shop"

    Scenario Outline: A group cannot be moved into <where>
      Given these groups:
        | name    | parent  |
        | Outer   |         |
        | Middle  | Outer   |
        | Inner   | Middle  |
      And I am on the stubs screen
      And the whole tree is expanded
      When I drag "Outer" onto "<target>"
      Then the stub tree shows exactly:
        | row    | level |
        | Outer  | 0     |
        | Middle | 1     |
        | Inner  | 2     |
      And no change has been recorded

      Examples:
        | where                  | target |
        | itself                 | Outer  |
        | its own child          | Middle |
        | a deeper descendant    | Inner  |

  Rule: Stubs live in groups, or at the top level

    Scenario: A new stub is created in the group I am working in
      Given these groups:
        | name     |
        | Payments |
      And I am on the stubs screen
      When I select "Payments"
      And I start a new stub
      And I set the name to "Refund"
      And I set the path to "/refund"
      And I save the stub
      And I select "Payments"
      And I expand all groups
      Then "Refund" is shown inside "Payments"

    Scenario: Move a stub to another group, and out of every group
      Given these groups:
        | name     |
        | Payments |
        | Orders   |
      And these stubs:
        | name   | path    | group    |
        | Refund | /refund | Payments |
      And I am on the stubs screen
      And the whole tree is expanded
      When I drag "Refund" onto "Orders"
      Then "Refund" is shown inside "Orders"
      When I drag "Refund" to the top level
      Then "Refund" is shown at the top level

    Scenario: Ungrouped stubs appear at the top of the tree
      Given these groups:
        | name     |
        | Payments |
      And these stubs:
        | name   | path    |
        | Health | /health |
      When I open the stubs screen
      Then the stub tree shows exactly:
        | row      | level |
        | Payments | 0     |
        | Health   | 0     |

  Rule: Deleting a group deletes everything inside it

    Background:
      Given these groups:
        | name | parent |
        | Shop |        |
        | Cart | Shop   |
      And these stubs:
        | name    | path     | group |
        | Catalog | /catalog | Shop  |
        | Basket  | /basket  | Cart  |
        | Health  | /health  |       |
      And I am on the stubs screen

    Scenario: Deleting a non-empty group warns first and removes its whole subtree
      When I select "Shop"
      And I delete the selection
      Then I am asked to confirm deleting "2 groups and 2 stubs"
      And I am warned that a group is deleted with everything inside it
      When I confirm the deletion
      Then the stub tree shows exactly:
        | row    | level |
        | Health | 0     |
      And requests are answered like this:
        | method | path     | answer  |
        | GET    | /catalog | no stub |
        | GET    | /basket  | no stub |
        | GET    | /health  | Health  |

    Scenario: Cancelling the warning deletes nothing
      When I select "Shop"
      And I delete the selection
      And I cancel the deletion
      Then no delete confirmation is shown
      And the stub tree shows "Shop"
      And a client sending GET /basket receives "Basket"

    Scenario: A cascading delete is one undoable change
      When I select "Shop"
      And I delete the selection
      And I confirm the deletion
      And I undo
      And I expand all groups
      Then the stub tree shows exactly:
        | row     | level |
        | Shop    | 0     |
        | Cart    | 1     |
        | Basket  | 2     |
        | Catalog | 1     |
        | Health  | 0     |

  Rule: Rearranging by dragging

    Background:
      Given these groups:
        | name     |
        | Payments |
        | Orders   |
      And these stubs:
        | name    | path     | group    |
        | Refund  | /refund  | Payments |
        | Charge  | /charge  | Payments |
        | History | /history |          |
      And I am on the stubs screen
      And the whole tree is expanded

    Scenario: Dropping a stub on a group moves it there as one undoable change
      When I drag "History" onto "Orders"
      Then "History" is shown inside "Orders"
      When I undo
      Then "History" is shown at the top level

    Scenario: Dropping a group on a group nests it, contents and all
      When I drag "Payments" onto "Orders"
      Then the stub tree shows exactly:
        | row      | level |
        | Orders   | 0     |
        | Payments | 1     |
        | Charge   | 2     |
        | Refund   | 2     |
        | History  | 0     |

    Scenario: Dropping a row where it already lives changes nothing
      When I drag "Refund" onto "Payments"
      Then "Refund" is shown inside "Payments"
      And no change has been recorded

    Scenario: Dragging a selected row moves the whole selection
      When I select "Refund"
      And I ctrl-click "History"
      And I drag "History" onto "Orders"
      Then "Refund" is shown inside "Orders"
      And "History" is shown inside "Orders"

  Rule: Selecting many rows and deleting them together

    Background:
      Given these groups:
        | name | parent |
        | Shop |        |
        | Cart | Shop   |
      And these stubs:
        | name    | path     | group |
        | Catalog | /catalog | Shop  |
        | Basket  | /basket  | Cart  |
        | Alpha   | /alpha   |       |
        | Beta    | /beta    |       |
        | Gamma   | /gamma   |       |
      And I am on the stubs screen

    Scenario: Select rows by checkbox, ctrl-click and shift-click
      When I tick the checkbox of "Alpha"
      And I ctrl-click "Gamma"
      Then the selection is:
        | row   |
        | Alpha |
        | Gamma |
      When I select "Shop"
      And I shift-click "Beta"
      Then the selection is:
        | row   |
        | Shop  |
        | Alpha |
        | Beta  |

    Scenario: Deleting a selection says how much goes, then deletes it all as one change
      When I select "Shop"
      And I ctrl-click "Alpha"
      And I delete the selection
      Then I am asked to confirm deleting "2 groups and 3 stubs"
      When I confirm the deletion
      Then the stub tree shows exactly:
        | row   | level |
        | Beta  | 0     |
        | Gamma | 0     |
      When I undo
      Then the stub tree shows "Shop"
      And the stub tree shows "Alpha"

    Scenario: Rows inside a selected group are not counted or deleted twice
      Given the whole tree is expanded
      When I select "Shop"
      And I ctrl-click "Basket"
      And I ctrl-click "Catalog"
      And I delete the selection
      Then I am asked to confirm deleting "2 groups and 2 stubs"
      When I confirm the deletion
      Then the stub tree does not show "Shop"
      And the stub tree shows "Alpha"

    Scenario: Deleting is a toolbar action, not a button on every row
      Then no row carries a delete button of its own

  Rule: Expanding and collapsing in bulk

    Background:
      Given these groups:
        | name   | parent |
        | Shop   |        |
        | Cart   | Shop   |
        | Admin  |        |
      And these stubs:
        | name   | path    | group |
        | Basket | /basket | Cart  |
        | Users  | /users  | Admin |
      And I am on the stubs screen

    Scenario: The tree opens with every group collapsed
      Then the stub tree shows exactly:
        | row   | level |
        | Admin | 0     |
        | Shop  | 0     |

    Scenario: Expand all and collapse all act on the whole tree when nothing is selected
      When I expand all groups
      Then the stub tree shows exactly:
        | row    | level |
        | Admin  | 0     |
        | Users  | 1     |
        | Shop   | 0     |
        | Cart   | 1     |
        | Basket | 2     |
      When I collapse all groups
      Then the stub tree shows exactly:
        | row   | level |
        | Admin | 0     |
        | Shop  | 0     |

    Scenario: With groups selected, only they and the groups inside them open and close
      When I select "Shop"
      And I expand all groups
      Then the stub tree shows exactly:
        | row    | level |
        | Admin  | 0     |
        | Shop   | 0     |
        | Cart   | 1     |
        | Basket | 2     |
      When I collapse all groups
      Then the group "Shop" is collapsed
      And the stub tree does not show "Cart"

  Rule: Grouping never affects matching

    Scenario: Stubs answer the same wherever they sit in the tree
      Given these groups:
        | name  | parent |
        | Outer |        |
        | Inner | Outer  |
      And these stubs:
        | name   | path    | group |
        | Nested | /nested | Inner |
        | Loose  | /loose  |       |
      Then requests are answered like this:
        | method | path    | answer |
        | GET    | /nested | Nested |
        | GET    | /loose  | Loose  |
      When I open the stubs screen
      And I expand all groups
      And I drag "Nested" to the top level
      And I drag "Loose" onto "Inner"
      Then requests are answered like this:
        | method | path    | answer |
        | GET    | /nested | Nested |
        | GET    | /loose  | Loose  |
