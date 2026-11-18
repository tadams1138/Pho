Feature: F7 — Export and import mock definitions (backup and restore)
  The complete set of mocks can be saved to a single file and restored from it later.

  Background:
    Given these groups:
      | name     | parent   |
      | Payments |          |
      | Refunds  | Payments |
    And these stubs:
      | name    | method | path            | match    | status | group    |
      | Charge  | POST   | /charge         | EXACT    | 201    | Payments |
      | Reverse | POST   | /refunds/{id}   | WILDCARD | 202    | Refunds  |
      | Health  | GET    | /health         | EXACT    | 200    |          |
    And the stub "Charge" has a header rule "Content-Type" equals "application/json" ignoring case
    And the stub "Charge" has a query parameter rule "currency" regex "^[A-Z]{3}$"
    And the stub "Charge" has a body rule contains "amount"
    And the stub "Charge" has a response header "Location" set to "/charges/1"
    And the stub "Health" is disabled

  Scenario: Export the complete set of mocks as one file
    Given a client has called GET /health
    When I export the configuration
    Then I receive a single JSON file
    And it contains every stub with its full request matcher and response
    And it contains the whole group tree
    And it contains no received requests and no configuration history

  Scenario: Import with replace-all clears the existing mocks and loads the file
    Given I have exported the configuration
    And the configuration has since changed:
      | change                                          |
      | the stub "Health" was deleted                   |
      | the response body of "Charge" became "changed"  |
      | a stub "Extra" answering GET /extra was added   |
    And I am on the stubs screen
    When I import the exported file, choosing replace-all
    Then the configuration is exactly the exported one
    And the stub tree does not show "Extra"

  Scenario: Import with merge adds and updates by identity, keeping everything else
    Given I have exported the configuration
    And the configuration has since changed:
      | change                                          |
      | the stub "Health" was deleted                   |
      | the response body of "Charge" became "changed"  |
      | a stub "Extra" answering GET /extra was added   |
    And I am on the stubs screen
    When I import the exported file, choosing merge
    Then requests are answered like this:
      | method | path                         | headers                        | body      | answer  |
      | POST   | /charge?currency=EUR         | Content-Type: application/json | amount=5  | Charge  |
      | GET    | /extra                       |                                |           | Extra   |
    And the response body of "Charge" is "Charge" again
    And the stub "Health" is back, and disabled

  Scenario: A replace-all round trip reproduces the configuration exactly
    Given I note the configuration
    And I have exported the configuration
    And the configuration has since changed:
      | change                                          |
      | everything was deleted                          |
    And I am on the stubs screen
    When I import the exported file, choosing replace-all
    Then the configuration is as I noted it
    And requests are answered like this:
      | method | path                 | headers                        | body      | answer  |
      | POST   | /charge?currency=EUR | Content-Type: APPLICATION/JSON | amount=5  | Charge  |
      | POST   | /charge?currency=eur | Content-Type: application/json | amount=5  | no stub |
      | POST   | /refunds/9           |                                |           | Reverse |
      | GET    | /health              |                                |           | no stub |

  Scenario: A file exported before a rule setting existed still imports, with that setting's default
    Given an export file written before rules had an ignore-case setting
    And I am on the stubs screen
    When I import that file, choosing replace-all
    Then the import succeeds
    And every rule has ignore case turned off
    And requests are answered like this:
      | method | path                 | headers                        | body      | answer  |
      | POST   | /charge?currency=EUR | Content-Type: application/json | amount=5  | Charge  |
      | POST   | /charge?currency=EUR | Content-Type: APPLICATION/JSON | amount=5  | no stub |

  Scenario Outline: An invalid file is rejected and changes nothing — <file>
    Given I note the configuration
    And I am on the stubs screen
    When I import <file>, choosing replace-all
    Then the import is rejected with an error
    And the configuration is as I noted it

    Examples:
      | file                                  |
      | a file that is not JSON               |
      | a JSON file that is not a Pho export  |
      | an export file with a stub missing its request |
