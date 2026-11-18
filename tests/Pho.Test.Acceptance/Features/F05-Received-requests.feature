Feature: F5 — Verify received requests
  Pho records every request that reaches its mock-serving surface, so a tester can confirm what
  the system under test called. Verification is done by a person looking at the received-requests
  view; there is no programmatic way to query the log, by design.

  Background:
    Given these stubs:
      | name        | method | path      | match    | status |
      | Orders      | GET    | /orders   | EXACT    | 200    |
      | Create      | POST   | /orders   | EXACT    | 201    |
      | Order one   | GET    | /items/1  | EXACT    | 200    |
      | Any item    | GET    | /items/*  | WILDCARD | 200    |

  Scenario: Every request is recorded with its outcome and the status it was answered with
    When a client sends GET /orders
    And a client sends GET /unknown
    And a client sends GET /items/1
    And I open the received-requests view
    Then the received-requests view lists:
      | method | path     | outcome       | status |
      | GET    | /items/1 | several stubs | 500    |
      | GET    | /unknown | no stub       | 404    |
      | GET    | /orders  | one stub      | 200    |

  Scenario: A request that matches no stub is still recorded, and the caller gets not found
    When a client sends DELETE /nothing/here
    Then the caller is told that no stub matched
    When I open the received-requests view
    Then the received-requests view lists:
      | method | path          | outcome | status |
      | DELETE | /nothing/here | no stub | 404    |

  Scenario: The full request can be inspected exactly as received
    When a client sends POST /orders?express=yes&gift=no with body "{"sku": "A-1", "qty": 2}" and these headers:
      | name          | value       |
      | X-Customer    | c-42        |
      | Authorization | Bearer t0k3n |
    And I open the received-requests view
    And I open the details of "POST /orders"
    Then the details show the query "?express=yes&gift=no"
    And the details show the header "X-Customer: c-42"
    And the details show the header "Authorization: Bearer t0k3n"
    And the details show the body "{"sku": "A-1", "qty": 2}"
    And "POST /orders" shows the time it was received
    And "POST /orders" shows the outcome "one stub" and the status 201

  @gap
  Scenario: The details name the stub that answered
    The received-requests view says whether a request matched, but not which stub answered it
    (it shows "matched" without naming the stub), nor which stubs an ambiguous request matched.

    When a client sends POST /orders
    And a client sends GET /items/1
    And I open the received-requests view
    Then "POST /orders" names the stub "Create"
    And "GET /items/1" names the stubs "Order one" and "Any item"

  Scenario: Requests are always listed newest first
    When a client sends GET /first
    And a client sends GET /second
    And a client sends GET /third
    And I open the received-requests view
    Then the received-requests view lists:
      | method | path    |
      | GET    | /third  |
      | GET    | /second |
      | GET    | /first  |

  Scenario: Filter by method and by part of the path, together
    When a client sends GET /orders
    And a client sends POST /orders
    And a client sends GET /orders/archive
    And a client sends GET /customers
    And I open the received-requests view
    And I filter by path "orders"
    Then the received-requests view lists:
      | method | path            |
      | GET    | /orders/archive |
      | POST   | /orders         |
      | GET    | /orders         |
    When I filter by method "GET"
    Then the received-requests view lists:
      | method | path            |
      | GET    | /orders/archive |
      | GET    | /orders         |

  Scenario: Page sizes of 10, 20, 50 or 100
    When I open the received-requests view
    Then I can choose to show 10, 20, 50 or 100 requests per page

  Scenario: Page through a long log, newest first across every page
    Given 25 requests have been received, to /r01 up to /r25 in that order
    When I open the received-requests view
    And I choose to show 10 requests per page
    Then the view is on page 1 of 3
    And the received-requests view lists the paths /r25 down to /r16
    When I go to the next page
    Then the view is on page 2 of 3
    And the received-requests view lists the paths /r15 down to /r06
    When I go to the next page
    Then the received-requests view lists the paths /r05 down to /r01
    When I go to the previous page
    Then the view is on page 2 of 3

  Scenario: Filters apply across all pages
    Given 25 requests have been received, to /r01 up to /r25 in that order
    When I open the received-requests view
    And I choose to show 10 requests per page
    And I filter by path "1"
    Then the view is on page 1 of 2
    And the received-requests view lists the paths /r21, /r19, /r18, /r17, /r16, /r15, /r14, /r13, /r12, /r11
    When I go to the next page
    Then the view is on page 2 of 2
    And the received-requests view lists the paths /r10, /r01

  Scenario: Clear the log
    When a client sends GET /orders
    And a client sends GET /unknown
    And I open the received-requests view
    And I clear the log
    Then the received-requests view is empty
