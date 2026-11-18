Feature: F10 — Light and dark theme
  The admin UI follows the system's colour-scheme preference, unless the user picks a theme,
  which is then remembered across visits until changed.

  Scenario Outline: The UI follows a <system> system preference by default
    Given my system prefers a <system> colour scheme
    When I open the admin UI
    Then the admin UI is shown in the <system> theme

    Examples:
      | system |
      | light  |
      | dark   |

  Scenario Outline: A chosen <choice> theme overrides the system and is remembered
    Given my system prefers a <system> colour scheme
    When I open the admin UI
    And I choose the <choice> theme
    Then the admin UI is shown in the <choice> theme
    When I open the admin UI again later
    Then the admin UI is shown in the <choice> theme

    Examples:
      | system | choice |
      | light  | dark   |
      | dark   | light  |

  Scenario: Going back to following the system
    Given my system prefers a light colour scheme
    And I open the admin UI
    And I choose the dark theme
    When I choose to follow the system theme
    Then the admin UI is shown in the light theme
    When my system switches to a dark colour scheme
    And I open the admin UI again later
    Then the admin UI is shown in the dark theme
