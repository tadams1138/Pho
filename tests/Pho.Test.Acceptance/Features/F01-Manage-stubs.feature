Feature: F1 — Manage stubs
  Test authors create and maintain stubs from the admin UI. A stub pairs a request matcher
  (method and path, plus optional query, header and body rules) with a canned response
  (status, headers and body). Stubs have no priority or ordering: two enabled stubs that
  match the same request are a configuration error (see F4), not resolved by ranking.

  Rule: Creating and validating stubs

    Scenario: Create a stub
      Given I am on the stubs screen
      When I start a new stub
      And I set the method to "POST"
      And I set the path to "/sessions"
      And I add a query parameter rule "client" equals "web"
      And I set the response status to 201
      And I set the response body to "session created"
      And I save the stub
      Then a stub named "POST /sessions" is stored
      And the stub tree shows "POST /sessions"
      And requests are answered like this:
        | method | path                 | answer         |
        | POST   | /sessions?client=web | POST /sessions |
        | POST   | /sessions            | no stub        |

    Scenario: A stub is named from its method and path when I give it no name
      Given I am on the stubs screen
      When I start a new stub
      And I set the method to "DELETE"
      And I set the path to "/carts/42"
      Then the name field suggests "DELETE /carts/42"
      When I save the stub
      Then a stub named "DELETE /carts/42" is stored

    Scenario: A stub without a path cannot be saved
      Given I am on the stubs screen
      When I start a new stub
      And I clear the path
      And I save the stub
      Then saving is refused with the message "Path is required."
      And no stub is stored

    @gap
    Scenario: A stub without a response status cannot be saved
      Clearing the response status field does not leave the stub without a status. The editor
      silently keeps the previous status (200), so the stub is saved instead of being refused.

      Given I am on the stubs screen
      When I start a new stub
      And I set the path to "/no-status"
      And I clear the response status
      And I save the stub
      Then saving is refused with a message about the response status
      And no stub is stored

  Rule: Listing, viewing, editing and deleting stubs

    Background:
      Given these stubs:
        | name     | method | path      | body          |
        | Orders   | GET    | /orders   | order list    |
        | Payments | POST   | /payments | payment taken |

    Scenario: Existing stubs are listed and open in the editor
      When I open the stubs screen
      Then the stub tree shows "Orders"
      And the stub tree shows "Payments"
      When I select "Orders"
      Then the editor shows the stub "Orders"
      And the editor's path is "/orders"

    Scenario: Edit a stub and save the change
      When I select "Orders"
      And I set the response body to "no orders yet"
      And I save the stub
      Then a client sending GET /orders receives "no orders yet"

    Scenario: Delete a stub
      When I select "Orders"
      And I delete the selection
      And I confirm the deletion
      Then the stub tree does not show "Orders"
      And the stub tree shows "Payments"
      And requests are answered like this:
        | method | path      | answer   |
        | GET    | /orders   | no stub  |
        | POST   | /payments | Payments |

  Rule: Header rules

    Scenario: A stub matches only requests whose headers satisfy every header rule
      Given I am on the stubs screen
      When I start a new stub
      And I set the name to "Secure"
      And I set the path to "/secure"
      And I add a header rule "X-Api-Key" equals "abc"
      And I add a header rule "X-Trace" present
      And I save the stub
      Then requests are answered like this:
        | method | path    | headers                    | answer  |
        | GET    | /secure | X-Api-Key: abc; X-Trace: 1 | Secure  |
        | GET    | /secure | x-api-key: abc; x-trace: 1 | Secure  |
        | GET    | /secure | X-Api-Key: abc             | no stub |
        | GET    | /secure | X-Api-Key: xyz; X-Trace: 1 | no stub |

    Scenario: A stub with no header rules is unconstrained by headers
      Given these stubs:
        | name  | method | path   |
        | Plain | GET    | /plain |
      Then requests are answered like this:
        | method | path   | headers                          | answer |
        | GET    | /plain |                                  | Plain  |
        | GET    | /plain | X-Anything: 1; Accept: text/html | Plain  |

  Rule: Ignore case is a per-rule choice for EQUALS and CONTAINS rules

    Scenario Outline: Ignore case makes a <type> <kind> rule compare its value case-insensitively
      Given I am on the stubs screen
      When I start a new stub
      And I set the name to "Typed"
      And I set the path to "/typed"
      And I add a <kind> rule "accept" <type> "application/json"
      And I turn on ignore case for the <kind> rule "accept"
      And I save the stub
      And a client sends GET /typed with <kind> "accept" set to "application/JSON"
      Then the request is answered by "Typed"

      Examples:
        | kind            | type     |
        | header          | equals   |
        | header          | contains |
        | query parameter | equals   |
        | query parameter | contains |

    Scenario Outline: Ignore case makes a <type> body rule compare the body case-insensitively
      Given I am on the stubs screen
      When I start a new stub
      And I set the name to "Body"
      And I set the method to "POST"
      And I set the path to "/body"
      And I add a body rule <type> "Hello World"
      And I turn on ignore case for the body rule
      And I save the stub
      And a client sends POST /body with body "HELLO WORLD"
      Then the request is answered by "Body"

      Examples:
        | type     |
        | equals   |
        | contains |

    Scenario: Ignore case applies to its own rule only
      Given these stubs:
        | name  | path   |
        | Mixed | /mixed |
      And the stub "Mixed" has a header rule "X-Relaxed" equals "abc" ignoring case
      And the stub "Mixed" has a header rule "X-Strict" equals "def"
      Then requests are answered like this:
        | method | path   | headers                    | answer  |
        | GET    | /mixed | X-Relaxed: ABC; X-Strict: def | Mixed   |
        | GET    | /mixed | X-Relaxed: abc; X-Strict: DEF | no stub |

    Scenario: Ignore case is off until someone turns it on
      Given I am on the stubs screen
      When I start a new stub
      And I set the name to "Strict"
      And I set the path to "/strict"
      And I add a header rule "Content-Type" equals "application/json"
      Then ignore case is off for the header rule "Content-Type"
      When I save the stub
      Then requests are answered like this:
        | method | path    | headers                          | answer  |
        | GET    | /strict | Content-Type: application/json   | Strict  |
        | GET    | /strict | Content-Type: application/JSON   | no stub |

    Scenario Outline: No ignore-case control is offered for a <type> rule
      Given I am on the stubs screen
      When I start a new stub
      And I add a header rule "X-Rule" <type>
      And I add a body rule <type>
      Then no ignore-case control is offered for the header rule "X-Rule"
      And no ignore-case control is offered for the body rule

      Examples:
        | type     | reason                                                                 |
        | regex    | A regular expression states its own case-insensitivity with (?i).      |
        | present  | A PRESENT rule compares no value.                                      |
        | absent   | An ABSENT rule compares no value.                                      |

  Rule: Response headers

    Scenario: A stub's response headers are sent with its status and body
      Given I am on the stubs screen
      When I start a new stub
      And I set the name to "With headers"
      And I set the path to "/with-headers"
      And I add a response header "Content-Type" with value "application/json"
      And I add a response header "X-Mock" with value "yes"
      And I set the response body to "{}"
      And I save the stub
      And a client sends GET /with-headers
      Then the request is answered by "With headers"
      And the response carries header "Content-Type" with value "application/json"
      And the response carries header "X-Mock" with value "yes"

    Scenario: A stub without response headers emits none
      Given these stubs:
        | name | path  |
        | Bare | /bare |
      When a client sends GET /bare
      Then the request is answered by "Bare"
      And the response carries no headers of its own

  Rule: Saving and unsaved changes

    Background:
      Given these stubs:
        | name     | method | path      | body          |
        | Orders   | GET    | /orders   | order list    |
        | Payments | POST   | /payments | payment taken |

    Scenario: Edits are only persisted when I save
      When I select "Orders"
      And I set the response body to "changed"
      Then the editor shows unsaved changes
      And a client sending GET /orders receives "order list"
      When I save the stub
      Then the editor shows the stub as saved
      And a client sending GET /orders receives "changed"

    Scenario: Selecting another row with unsaved changes warns first
      When I select "Orders"
      And I set the response body to "changed"
      And I select "Payments"
      Then I am warned about unsaved changes
      And the editor shows the stub "Orders"

    Scenario Outline: When warned about unsaved changes I can <choice>
      When I select "Orders"
      And I set the response body to "changed"
      And I select "Payments"
      And I choose to <choice>
      Then the editor shows the stub "<editor shows>"
      And a client sending GET /orders receives "<served body>"

      Examples:
        | choice  | editor shows | served body |
        | save    | Payments     | changed     |
        | discard | Payments     | order list  |
        | stay    | Orders       | order list  |

    Scenario: Navigating to another view with unsaved changes warns first
      When I select "Orders"
      And I set the response body to "changed"
      And I go to the received-requests view
      Then I am warned about unsaved changes
      And I am still on the stubs screen
      When I choose to discard
      Then I am on the received-requests view

    Scenario: Closing or reloading the page with unsaved changes warns first
      When I select "Orders"
      And I set the response body to "changed"
      Then the browser warns before the page is closed or reloaded
      When I save the stub
      Then the browser no longer warns before the page is closed or reloaded

    Scenario: Saving revalidates the draft
      When I select "Orders"
      And I set the path match type to "REGEX"
      And I set the path to "/orders/(unclosed"
      And I save the stub
      Then saving is refused with the message "Path is not a valid regular expression: /orders/(unclosed"
      And a client sending GET /orders receives "order list"

  Rule: Duplicating stubs

    Scenario: Duplicate a stub
      Given these groups:
        | name     |
        | Checkout |
      And these stubs:
        | name | method | path | status | body | group    |
        | Pay  | POST   | /pay | 202    | paid | Checkout |
      And the stub "Pay" has a header rule "X-Shop" equals "eu"
      And the stub "Pay" has a response header "X-Receipt" set to "r-1"
      And I am on the stubs screen
      When I expand all groups
      And I select "Pay"
      And I duplicate the stub
      Then "Copy of Pay" is shown inside "Checkout"
      And "Copy of Pay" has the same request matcher and response as "Pay"
      And "Copy of Pay" is a separate stub from "Pay"
      And "Copy of Pay" is disabled
      When I set the response body to "paid twice"
      And I save the stub
      Then a client sending POST /pay with header "X-Shop" set to "eu" receives "paid"
