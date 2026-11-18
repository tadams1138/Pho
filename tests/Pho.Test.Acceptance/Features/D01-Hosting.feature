Feature: D1 — Hosting decisions
  Decisions about how Pho is run that the product specification deliberately leaves out. They are
  verified here without saying how they are implemented: Pho runs as a long-lived service whose
  health an orchestrator can check, it serves its admin UI and its mock-serving surface as two
  separately addressed surfaces, it can sit behind a reverse proxy under a sub-path, and it keeps
  its mock definitions across restarts.

  Scenario: The service reports when it is healthy
    Given Pho is running
    When an orchestrator asks the admin surface whether Pho is healthy
    Then Pho reports that it is healthy

  Scenario: The admin UI and the mock surface listen on their own default addresses
    Given Pho has been started with its default settings
    Then the admin UI is served at its default address
    And the mock surface answers at its own default address
    And the service reports that it is healthy at the admin address

  Scenario: Every request to the mock surface is mock traffic, even where the admin surface has a page
    Given a stub for every path the admin surface serves
    Then each of those paths is answered by its stub on the mock surface
    And the admin surface still serves its own pages at those paths

  Scenario: The mock surface never answers from the admin surface's pages
    Given no stubs exist
    Then none of the paths the admin surface serves is answered on the mock surface

  Scenario: Each surface's address can be configured
    Given Pho is configured with its own addresses for the admin UI and for mock traffic
    And these stubs:
      | name | path  |
      | Ping | /ping |
    Then GET /ping is answered by "Ping" at the configured mock address
    And the admin UI is served at the configured admin address
    And nothing is mocked at the configured admin address

  Rule: Running behind a reverse proxy under a sub-path

    Scenario: A proxy that announces the sub-path it removed
      Given Pho is running
      When the admin UI is requested through a proxy that serves it under "/tools/pho"
      Then every link on the page leads back through "/tools/pho/"

    Scenario: A sub-path configured for a proxy that removes it silently
      Given Pho is configured to be served under the sub-path "tools/pho"
      When the admin UI is requested through a proxy that silently removes "/tools/pho"
      Then every link on the page leads back through "/tools/pho/"

    Scenario: Served directly, the admin UI's links lead to the host root
      Given Pho is running
      When the admin UI is requested directly
      Then every link on the page leads back through "/"

    Scenario: A sub-path never changes what a stub matches
      Given Pho is configured to be served under the sub-path "tools/pho"
      And these stubs:
        | name   | path              |
        | Plain  | /orders           |
        | Prefix | /tools/pho/orders |
      Then requests are answered like this:
        | method | path              | answer |
        | GET    | /orders           | Plain  |
        | GET    | /tools/pho/orders | Prefix |

  Scenario: Mock definitions survive a restart
    Given Pho keeps its data in a database file
    And these stubs:
      | name    | path     |
      | Durable | /durable |
    When Pho is restarted
    Then a client sending GET /durable receives "Durable"

  Scenario: Received requests are kept for a day by default
    Given a request was received more than a day ago
    When a client sends GET /fresh
    And I open the received-requests view
    Then the received-requests view lists:
      | method | path   |
      | GET    | /fresh |
