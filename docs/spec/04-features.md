# 4. Features

Capabilities are written as acceptance criteria in Given/When/Then form. These are the source for BDD/TDD: each criterion should become at least one automated test.

---

## F1 — Manage stubs

Test authors create and maintain stubs through the web UI.

- **Create a stub**
  - Given I am on the stub editor
  - When I define a request matcher (method + path, plus optional query/header/body rules) and a response (status, headers, body) and save
  - Then the stub is persisted and appears in the stub list
- **Validation**
  - Given I try to save a stub without a path matcher (or without a response status)
  - Then saving is rejected with a clear validation message and nothing is persisted
- **List / view / edit / delete**
  - Given existing stubs
  - Then I can see them in a list, open one to view or edit its details, save changes, and delete a stub
  - _(Stubs have no priority or ordering. Two enabled stubs that match the same request are a configuration error, handled in F4 — not resolved by ranking.)_
- **Match on request headers**
  - Given the stub editor
  - When I add one or more header rules (name + EQUALS / CONTAINS / REGEX / PRESENT / ABSENT)
  - Then the stub matches only requests whose headers satisfy every rule, with header **names compared case-insensitively**
  - And a stub with no header rules is unconstrained by headers
- **Emit response headers**
  - Given the stub editor
  - When I add response header rows (name + value)
  - Then a request served by that stub receives those headers alongside the status and body; a stub with no header rows emits none
- **Save and unsaved changes**
  - Given I have edited a stub in the editor
  - Then the editor shows that there are unsaved changes, and nothing is persisted until I **save**
  - And when I try to leave — selecting another row in the tree, navigating to another view, or closing/reloading the page — I am warned first and can save, discard, or stay
  - And saving revalidates the draft; validation failures are shown and nothing is persisted
- **Duplicate a stub**
  - Given an existing stub
  - When I duplicate it
  - Then a new stub is created as a copy of its full definition — request matcher and response — in the same group, with a distinct id and a name marking it a copy (e.g. "Copy of …"), so similar requests/responses can be built without redefining from scratch
  - And the duplicate is created **disabled** by default, so it does not immediately collide with its source (an identical matcher on two enabled stubs would be an ambiguous-match error, F4); the user adjusts it and then enables it
  - And I can then edit the duplicate independently of the original

## F2 — Enable / disable stubs

A stub can be toggled on or off without being deleted or rewritten, so a user can switch between alternative responses for the same endpoint.

- **Disable**
  - Given an enabled stub
  - When I disable it
  - Then it is retained but excluded from matching, and requests it would have matched fall through to other stubs (or the no-match default)
- **Enable**
  - Given a disabled stub
  - When I enable it
  - Then it participates in matching again
- **Toggle between responses**
  - Given two stubs for the same endpoint returning different responses, one enabled and one disabled
  - When I flip which one is enabled
  - Then the endpoint's response switches accordingly, with no edits to either response body

## F3 — Organize stubs into nested groups

Stubs are arranged in a tree of user-defined groups for organizational purposes.

- **Create / rename / delete groups**
  - Given the stub tree
  - When I create a group (optionally inside another group), rename it, or delete it
  - Then the tree updates accordingly
- **Nesting**
  - Given a group
  - When I create or move a group into it
  - Then groups nest to any depth, and the UI shows the hierarchy as a tree view
  - And an operation that would make a group its own ancestor (a cycle) is rejected
- **Assign stubs to groups**
  - Given a stub and a group
  - When I place the stub in that group (or move it to another group, or leave it ungrouped)
  - Then the stub appears under that group in the tree; ungrouped stubs appear at the tree root
- **Delete behavior (cascade)**
  - Given a non-empty group
  - When I delete it
  - Then the group and everything nested under it — all descendant groups and all contained stubs — are deleted
  - And the UI warns that the delete is cascading and asks for confirmation first (it is destructive)
  - And the whole action is recorded as a single undoable change, so it can be undone in one step (F7)
- **Rearrange by dragging**
  - Given the stub tree
  - When I drag a stub or a group onto a group (or onto the top-level drop area)
  - Then it moves there — a stub changes group, a group is nested under the target — and the move is one undoable change
  - And a drop that would nest a group inside itself or one of its own descendants is refused, as is a drop where the row already lives
  - And when the dragged row is part of the current selection, the whole selection moves; a group carries its contents with it
- **Select many rows and delete them together**
  - Given the stub tree
  - When I select several rows — by row checkbox, ctrl/cmd-click to add one, or shift-click for a range — and choose **Delete selected**
  - Then I am first told how much will be removed (counting everything nested inside selected groups), and on confirmation all of it is deleted as one undoable change
  - And rows already covered by a selected group are not deleted twice
  - _(Deleting is a single toolbar action over the selection; individual rows carry no delete button.)_
- **Grouping does not affect matching**
  - Given stubs in any groups
  - Then request matching (F4) behaves identically regardless of grouping

## F4 — Match requests and serve responses

Pho answers requests sent to its mock-serving surface.

- **Match and respond**
  - Given an enabled stub whose matcher matches an incoming request
  - When that request arrives
  - Then Pho returns the stub's configured status, headers, and body
- **Matcher rules**
  - Path matching supports EXACT, WILDCARD (`*` / `{param}`), and REGEX
  - Query params, headers, and body support EQUALS, CONTAINS, REGEX, PRESENT, and ABSENT rules
  - A request matches only when all specified criteria are satisfied
- **Ambiguous match is an error (no priority)**
  - Given more than one enabled stub matches one request
  - Then Pho does **not** pick a winner (there is no priority/ordering); it returns **HTTP 500** with a body identifying the conflict and the stubs involved
  - And it **logs an error naming which stubs matched** (ids/names) together with the request
  - And it surfaces the conflict in the UI where feasible (e.g. flags the request in the received-requests view as an ambiguous match and/or warns on the involved stubs)
- **No match**
  - Given no enabled stub matches an incoming request
  - Then Pho returns HTTP 404 with a body indicating no stub matched
- **Disabled stubs ignored**
  - Given a stub that would match but is disabled
  - Then it is not used

## F5 — Verify received requests (spying)

Pho records traffic so testers can confirm what was called.

- **Recording**
  - Given any request to the mock-serving surface — i.e. any inbound request that is not one of Pho's own authoring/UI endpoints — whether or not it matches a stub
  - Then a ReceivedRequest is recorded with method, path, query, headers, body, timestamp, the match outcome (one / none / several), and the response status
  - And a request that matches no stub is still recorded, and the caller simply receives **HTTP 404**
- **Inspection**
  - Given recorded requests
  - When I open the received-requests view in the UI
  - Then I see them **always in descending order by time received** (most recent at the top, oldest at the bottom) and can filter by method and path
- **Pagination**
  - Given more requests than fit on one page
  - When I view the log
  - Then results are paginated with a selectable page size of **10, 20, 50, or 100** requests per page, and I can navigate between pages
  - And the descending (most-recent-first) order and any active filters apply across all pages
- **Filter by URL path**
  - Given many requests recorded in a short span
  - When I enter a path filter
  - Then the list narrows to requests whose path matches, using **partial/substring** matching (not just exact) so a busy log can be reduced to the paths of interest; combinable with the method filter
- **Full request detail**
  - Given a recorded request
  - When I open it
  - Then I can observe the **full** request exactly as Pho received it — method, path, query string, **all headers, and the complete body** — along with the **time it was received**, the matched stub (or "no match"), and the response status served
- **Clear**
  - Given recorded requests
  - When I clear the log
  - Then the received-requests view is empty

Verification is **human-observed in the UI only, by design** — there is no programmatic query endpoint and none is planned (see [`06-interfaces.md`](06-interfaces.md)).

## F6 — Mock definition history (view / revert)

Pho versions the whole configuration ([`03-domain-model.md`](03-domain-model.md#configuration-history-and-undoredo)); a single mock's history is a derived view of that timeline, so edits to a mock can be reviewed and reverted.

- **View a mock's history**
  - Given a stub that has been edited over time
  - When I open its history
  - Then I see, in reverse-chronological order, the configuration revisions in which that stub's definition changed, and can inspect the definition captured in each
- **Revert a mock**
  - Given an earlier version of a stub's definition
  - When I revert the stub to it
  - Then a new configuration revision is recorded in which only that stub's definition is restored (other mocks and the group tree are unchanged), and the revert can itself be undone (F7)

## F7 — Undo / redo configuration changes

A global undo/redo lets a user reverse and reapply any change to the mock configuration, in order — by stepping through the configuration history ([`03-domain-model.md`](03-domain-model.md#configuration-history-and-undoredo)).

- **Undo a change**
  - Given I have made one or more configuration changes (create/edit/delete/toggle/move a stub, or create/rename/move/delete a group)
  - When I undo
  - Then the most recent not-yet-undone change is reversed and the configuration returns to its prior state
- **Redo**
  - Given I have undone a change and made no new change since
  - When I redo
  - Then that change is reapplied
- **Undo a cascade group deletion**
  - Given I deleted a non-empty group (F3), removing its subgroups and stubs
  - When I undo
  - Then the entire deleted subtree — groups and stubs with their definitions — is restored in one step
- **New change clears the redo stack**
  - Given I have undone a change
  - When I make a new change instead of redoing
  - Then the previously undone changes can no longer be redone
- **Bounded by retention**
  - Given configuration revisions older than the history retention window (1 year default) have been pruned
  - Then undo cannot reach past what remains

## F8 — Export and import mock definitions (backup / restore)

- **Export the full set**
  - Given the current configuration
  - When I export
  - Then I receive a single JSON file containing the **complete set of mocks** — every stub definition (matchers and responses) **and the full group tree** they belong to — so nothing is omitted; ReceivedRequest logs and configuration history are not included
- **Import**
  - Given a previously exported JSON file
  - When I import it and choose a mode
  - Then the mocks are restored: **replace-all** (clear existing, load the file) or **merge** (add/update by id)
- **Round-trip fidelity**
  - Given I export the full set and later re-import that same file with **replace-all**
  - Then the resulting configuration matches the exported one exactly — every stub (with all matcher and response detail) and the entire group hierarchy are reproduced faithfully
- **Validation**
  - Given a malformed or schema-invalid file
  - When I import it
  - Then the import is rejected with a clear error and existing mocks are left unchanged

## F9 — Body formatting and validation (JSON / XML)

The stub editor helps authors work with structured bodies on both the **request body matcher** and the **response body** fields.

- **Format**
  - Given a body field containing JSON (or XML)
  - When I click the JSON (or XML) **format** button
  - Then the content is pretty-printed/indented in place
- **Validate**
  - Given a body field
  - When I click the JSON (or XML) **validate** button
  - Then the UI indicates whether the content is well-formed, and on failure shows the error (with location where available)
- **Advisory, not blocking**
  - Given a body that is not JSON or XML (e.g. plain text or a deliberately malformed payload for a test)
  - Then formatting/validation is a convenience only and does **not** prevent saving the stub — bodies may be any content

## F10 — Spell-check hints

- **Highlight possible typos**
  - Given free-text input in the editor (e.g. stub `name`, `description`, and body text)
  - When I type
  - Then the UI indicates possible misspellings
- **Advisory, not blocking**
  - Given a flagged word (which may be an intentional identifier, code, or domain term)
  - Then the hint is advisory only and never blocks saving or alters the stored value

## F11 — Light / dark theme

- **Follow system by default**
  - Given the UI loads and the OS/browser exposes a color-scheme preference
  - Then the UI matches the system setting (light or dark) automatically
- **Manual override**
  - Given I choose a theme (light or dark) explicitly
  - Then the UI uses my choice, and it persists across visits until I change it or revert to "follow system"

## F12 — Navigate the stub tree from the keyboard

The tree is operable without a mouse; the editor follows the keyboard.

- **Move through rows**
  - Given focus is on the stub tree
  - When I press ↑ / ↓ (or Home / End)
  - Then the active row moves through the tree in the order it is displayed, scrolling into view, and becomes the selection
- **Collapse and expand**
  - Given the active row is a group
  - When I press ← or →
  - Then the group collapses or expands; on an already-expanded group → moves into its first child, and on a collapsed or non-group row ← moves to its parent group
- **Select and delete**
  - Given the active row
  - When I press space
  - Then that row is added to or removed from the selection (as ctrl/cmd-click does), and Delete raises the same confirmation as **Delete selected**
- **Editing follows selection**
  - Given exactly one stub is selected, however it was selected
  - Then the stub editor shows that stub; selecting a group, several rows, or nothing shows no editor
  - And moving off a stub with unsaved changes warns first (F1)
