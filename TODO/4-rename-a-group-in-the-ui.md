# 4 — Rename a group from the stubs screen

## Summary
The stubs screen needs a way to rename a group; renaming is one undoable change.

## Motivation
The service layer already supports renaming and the history spec lists group rename as a revision-producing change, but authors cannot do it in the UI and have to delete and recreate groups.

## Behavior
- The author can rename a group from the stub tree; the new name is stored with surrounding spaces removed; a blank name is refused or ignored like group creation.
- The rename is one configuration revision ("Renamed group ...") and can be undone and redone.

## Spec text removed (restore on implementation)
These requirements were removed from `SPEC.md` (and the scenario from the feature file) because
the code did not implement them. Affected IDs: F3.2, H-1, F6.

**SPEC.md F3.2 (removed in full):**

> - **F3.2 Rename.** A group can be renamed; the rename is one undoable change.

**SPEC.md H-1 (word removed):** the list of revision-producing changes read "...enabling, disabling or moving stubs; creating, renaming, moving or deleting groups; and importing a file." Restore "renaming" after "creating,".

## Gherkin scenario removed
From `tests/Pho.Test.Acceptance/Features/F03-Nested-groups.feature` (it was tagged `@gap`):

```gherkin
    @gap
    Scenario: Rename a group
      The stubs screen offers no way to rename a group, although renaming is a supported change
      (and one that undo is expected to reverse, F6).

      Given these groups:
        | name     |
        | Payments |
      And I am on the stubs screen
      When I rename the group "Payments" to "Billing"
      Then the stub tree shows "Billing"
      And the stub tree does not show "Payments"
```

## What the code does today
- `src/Pho.Domain/GroupService.cs` `RenameAsync(Guid id, string name)` exists and works.
- `src/Pho.Web/Components/GroupTreeView.razor` and `src/Pho.Web/Components/Pages/Stubs.razor` offer no rename action; `TreeEditService` / the UI never call `RenameAsync`.
- `ConfigHistoryService.RecordAsync(summary)` must be called once per action, as for other tree edits (see AGENTS.md).

## Acceptance criteria
- The behavior above works in the app.
- Restore the requirement in `SPEC.md` (text above; replace the interim wording).
- Restore the scenario in `tests/Pho.Test.Acceptance/Features/F03-Nested-groups.feature` (drop the `@gap` tag; add or adapt step definitions as needed).
- `dotnet build` is warning-free and `dotnet test` passes.

## Technical notes
- Add a rename action to the group row (inline edit or small form) calling `GroupService.RenameAsync` then `ConfigHistoryService.RecordAsync`.
- Add an undo scenario for rename.

## Out of scope
- Changes unrelated to the behavior above.
