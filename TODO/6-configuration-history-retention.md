# 6 — Prune configuration history after a retention window

## Summary
Configuration revisions older than a retention window (default one year) are removed automatically, and undo cannot reach past the oldest remaining revision.

## Motivation
Received requests are already bounded by retention; configuration history grows without limit, each revision being a full JSON snapshot.

## Behavior
- A setting for configuration-history retention, default 1 year.
- Revisions older than the window are pruned automatically; undo stops at the oldest remaining revision.
- Received-request retention is unchanged.

## Spec text removed (restore on implementation)
These requirements were removed from `SPEC.md` (and the scenario from the feature file) because
the code did not implement them. Affected IDs: H-8, F6.5, Y-5, Y-6, section 2 and 10.

**SPEC.md H-8 (current text replaced; restore the original):**

> - **H-8** Revisions older than the configuration-history retention window (see 10) are removed automatically, and undo cannot reach past the oldest remaining revision.

**SPEC.md F6.5 (restore):**

> - **F6.5 Bounded by retention** (H-8).

**SPEC.md section 10 settings table row (removed; restore):**

> | Configuration-history retention | 1 year | Revisions older than this are removed automatically; bounds undo depth. |

**SPEC.md Y-5 (restore):**

> - **Y-5** The received-request log and the configuration history are bounded by their retention settings rather than growing without limit.

**SPEC.md Y-6 (restore the tail):** "...no later than the next time a request is recorded; revisions past the window are removed automatically and undo depth is limited to what remains."

**SPEC.md section 2 feature summary:** "Persistent storage that survives restarts, and configurable retention." (currently "...retention of received requests.")

## Gherkin scenario removed
From `tests/Pho.Test.Acceptance/Features/F06-Undo-and-redo.feature` (it was tagged `@gap`):

```gherkin
  @gap
  Scenario: Undo cannot reach past the history retention window
    Configuration history is never pruned: there is no history retention window (one year by
    default), so undo can always reach back to the very first change.

    Given a stub "Ancient" answering GET /ancient was created more than a year ago
    And a stub "Recent" answering GET /recent was created today
    When I undo as far as possible
    Then the stub tree does not show "Recent"
    And the stub tree shows "Ancient"
```

## What the code does today
- `src/Pho.Infrastructure/EfConfigHistoryStore.cs`: `AppendRevisionAsync` stores `ConfigRevisionRecord` (`CreatedAt`) and `TruncateAfterAsync` drops redo revisions; nothing removes old revisions.
- Received requests do prune: `src/Pho.Infrastructure/EfReceivedRequestLog.cs` with `ReceivedRequestRetention`, configured in `src/Pho.Web/Program.cs` via `Pho:ReceivedRequestRetentionHours`.
- `ConfigHistoryService` (`src/Pho.Domain`) assumes sequence 0 is the implicit empty baseline; after pruning, undo must stop at the oldest remaining revision (pruning the oldest revisions means the baseline is no longer reachable).

## Acceptance criteria
- The behavior above works in the app.
- Restore the requirement in `SPEC.md` (text above; replace the interim wording).
- Restore the scenario in `tests/Pho.Test.Acceptance/Features/F06-Undo-and-redo.feature` (drop the `@gap` tag; add or adapt step definitions as needed).
- `dotnet build` is warning-free and `dotnet test` passes.

## Technical notes
- Add a configuration key (AGENTS.md currently says none exists) and document it in AGENTS.md.
- Decide how pruning interacts with the current position and the oldest revision's predecessor.

## Out of scope
- Changes unrelated to the behavior above.
