# 5 — Name the matched stubs in the received-requests view

## Summary
The received-requests view should name the stub that answered a request and, for an ambiguous request, every stub it matched.

## Motivation
Today the view says only that a request matched. Authors verifying calls, and fixing overlapping stubs, need to know which stub(s) were involved.

## Behavior
- A request that matched one stub shows that stub's name in the summary or detail.
- An ambiguous request's detail lists every matched stub by name, so overlapping stubs can be found and fixed.
- The stubs are identified by identity; a stub deleted since then is shown in a sensible way (e.g. its id).

## Spec text removed (restore on implementation)
These requirements were removed from `SPEC.md` (and the scenario from the feature file) because
the code did not implement them. Affected IDs: F5.6, F5.7, L-27, M-8.

**SPEC.md F5.6 (changed wording; restore):**

> - **F5.6 Summary per request.** Each listed request shows the time received (in UTC), method, path, outcome — the matched stub, "no match", or an ambiguous-match flag with the number of stubs matched — and the status returned.

**SPEC.md F5.7 (last sentence removed; restore):**

> For an ambiguous match the detail identifies every stub that matched, so overlapping stubs can be found and fixed.

(currently replaced by "The view does not name the stub that answered a request, nor the stubs an ambiguous request matched (the log and the 500 response body do, M-8).")

**SPEC.md L-27 (restore):**

> - **L-27 Ambiguous matches are flagged** prominently as errors, with the number of stubs matched and, in the detail, which stubs they are.

**SPEC.md M-8, third bullet (restore):** "flags the request as an ambiguous match in the received-requests view, identifying the stubs it matched (see F5 and 8.5)." (currently "...with the number of stubs it matched ...").

## Gherkin scenario removed
From `tests/Pho.Test.Acceptance/Features/F05-Received-requests.feature` (it was tagged `@gap`):

```gherkin
  @gap
  Scenario: The details name the stub that answered
    The received-requests view says whether a request matched, but not which stub answered it
    (it shows "matched" without naming the stub), nor which stubs an ambiguous request matched.

    When a client sends POST /orders
    And a client sends GET /items/1
    And I open the received-requests view
    Then "POST /orders" names the stub "Create"
    And "GET /items/1" names the stubs "Order one" and "Any item"
```

## What the code does today
- `src/Pho.Web/Components/Pages/ReceivedRequests.razor` `Describe(...)` (~line 134) renders only "matched", "no match" or "AMBIGUOUS (n stubs)"; the detail does not list stubs.
- The data is already recorded: `ReceivedRequest.MatchedStubIds` (Q-3) is stored; stub names are not, so resolve them from the current stubs (or store the name at record time).
- The application log (`src/Pho.Web/MockServingMiddleware.cs`) and the 500 body (`src/Pho.Domain/MockServer.cs`) already name the stubs.

## Acceptance criteria
- The behavior above works in the app.
- Restore the requirement in `SPEC.md` (text above; replace the interim wording).
- Restore the scenario in `tests/Pho.Test.Acceptance/Features/F05-Received-requests.feature` (drop the `@gap` tag; add or adapt step definitions as needed).
- `dotnet build` is warning-free and `dotnet test` passes.

## Technical notes
- Decide whether to snapshot the stub name in the record or look it up (and what to show for deleted stubs).

## Out of scope
- Changes unrelated to the behavior above.
