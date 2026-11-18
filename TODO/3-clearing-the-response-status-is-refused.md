# 3 — Refuse a stub whose response status was cleared

## Summary
Clearing the response status field in the stub editor must refuse the save, instead of silently keeping the previous status.

## Motivation
A stub always has a response status, so an author who clears the field expects to be told it is missing, not to find 200 saved behind their back.

## Behavior
- Clearing the Status field (or entering something that is not a number) makes saving fail with a message about the response status; nothing is stored.
- A number outside 100-599 is already refused ("Response status must be a valid HTTP status code (100-599).").

## Spec text removed (restore on implementation)
These requirements were removed from `SPEC.md` (and the scenario from the feature file) because
the code did not implement them. Affected IDs: F1.5 (and L-16 message list).

**SPEC.md, F1.5 (Validation)** currently reads (after the gap was removed) as the list item
"the status is outside 100–599 — ... (a status field that is cleared or does not hold a number keeps its previous value, so it never counts as missing)". On implementation, drop that parenthetical and state that a missing status is refused. The requirement text that was removed from F1.5 context is the P-1 / F1.5 rule that a stub must have a status in 100-599 (P-1: "A response definition has a **status code** (an integer from 100 to 599; default 200)", unchanged); the removed behavior was only the implicit "cleared status is refused" expectation, so no whole requirement ID was deleted. The parenthetical to remove is exactly:

> (a status field that is cleared or does not hold a number keeps its previous value, so it never counts as missing)

## Gherkin scenario removed
From `tests/Pho.Test.Acceptance/Features/F01-Manage-stubs.feature` (it was tagged `@gap`):

```gherkin
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
```

## What the code does today
- `src/Pho.Web/Components/StubEditorPanel.razor` (~line 151, ~207): the Status input is `type="number"` and its `@oninput` calls `ParseStatus(e.Value, Draft.Status)`, which falls back to the previous status when `int.TryParse` fails (blank text), so the draft keeps 200.
- `src/Pho.Domain/StubDraft.cs` (~line 207): validation only checks `Status is < 100 or > 599`; `Status` is a non-nullable `int`, so "missing" cannot be represented.

## Acceptance criteria
- The behavior above works in the app.
- Restore the requirement in `SPEC.md` (text above; replace the interim wording).
- Restore the scenario in `tests/Pho.Test.Acceptance/Features/F01-Manage-stubs.feature` (drop the `@gap` tag; add or adapt step definitions as needed).
- `dotnet build` is warning-free and `dotnet test` passes.

## Technical notes
- Make the draft able to represent an empty status (e.g. `int?`), or validate in the editor, so a cleared field yields a validation error such as "Response status is required." in the same inline list as the other messages.
- Keep the 100-599 check.

## Out of scope
- Changes unrelated to the behavior above.
