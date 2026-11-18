# Pho — Product Specification

This document is the single, technology-neutral specification of what Pho does. It describes
behavior only: it is complete enough to rebuild Pho on any technology, and it deliberately says
nothing about how Pho is built, hosted, or stored. Requirements carry numbered identifiers
(for example **M-4**) so that tests and later changes can refer to them.

Contents:

1. [What Pho is](#1-what-pho-is)
2. [Scope](#2-scope)
3. [Users, roles and permissions](#3-users-roles-and-permissions)
4. [Product decisions](#4-product-decisions)
5. [Concepts](#5-concepts)
6. [Serving mocks: matching and resolution](#6-serving-mocks-matching-and-resolution)
7. [Features](#7-features)
8. [Screens, layout and navigation](#8-screens-layout-and-navigation)
9. [The two surfaces and hosting behind a reverse proxy](#9-the-two-surfaces-and-hosting-behind-a-reverse-proxy)
10. [Configuration settings](#10-configuration-settings)
11. [Quality requirements](#11-quality-requirements)
12. [Glossary](#12-glossary)

---

## 1. What Pho is

Pho is a **mock web service**: a standing server that imitates real web services so that
software can be tested against a controllable stand-in instead of a live dependency. It is
comparable to WireMock, Mockoon and MockServer, with a browser-based authoring experience.

A test author defines fake endpoints — **stubs** — in a web-based admin interface. Each stub is
a rule: "when a request looks like *this*, respond with *that*". The system under test sends its
ordinary HTTP requests to Pho's mock-serving surface and receives the configured responses. Pho
records every request it receives there, so the author can see exactly what was called.

**The problem it solves.** Code that depends on external web services is slow, flaky and hard
to control in tests: the real service may be unavailable, rate-limited or non-deterministic, and
it rarely produces on demand the error conditions a test needs. Pho replaces that dependency with
responses the tester fully controls, and lets the tester switch between alternative behaviors of
the same endpoint without editing test code.

## 2. Scope

### 2.1 In scope for the first version

- Stub management through the web admin interface: create, view, edit, duplicate, delete.
- Organizing stubs into user-defined groups, nested to any depth, shown as a tree.
- Request matching by method, path, query parameters, headers and body.
- Responses with a configured status code, headers and body.
- Enabling and disabling stubs without deleting them.
- Recording received requests and inspecting them in full (human-observed verification).
- A whole-configuration history with global, ordered undo and redo.
- Body helpers (JSON and XML format and validate) and advisory spell-check hints.
- A helper for HTTP Basic authentication credentials.
- Light and dark themes.
- Export of the whole mock configuration to a file, and import from such a file.
- Persistent storage that survives restarts, and configurable retention.
- A single shared instance: one global set of stubs for everyone.

### 2.2 Not in scope (candidates for later versions)

- A programmatic configuration or verification interface for tests (creating or resetting
  stubs, or asserting on received requests, from code). Authoring and verification happen in the
  admin interface only.
- Loading stub definitions from files automatically at start-up ("configuration as code"),
  distinct from manual export and import.
- Generating stubs from an API description document (for example OpenAPI).
- Stateful scenarios (a different response on the Nth call, state machines).
- Artificial latency and fault injection (delays, dropped connections).
- Per-session or namespaced isolation for parallel test suites.
- Authentication on the admin interface (see section 3).
- Response templating that echoes request data into the response.
- Per-stub history views or reverting a single stub in isolation (see 5.8).
- A configurable no-match response.

## 3. Users, roles and permissions

**Personas.**

- **Test author** (developer or QA engineer) — the only interactive user. Creates and manages
  stubs in the admin interface and inspects received requests to verify behavior.
- **System under test** — a non-human client: the application or test process whose HTTP
  requests Pho answers. It never authenticates and never configures anything; it simply sends
  requests to the mock-serving surface and receives responses.

**Roles and permissions.**

- **U-1** There is exactly one role. Anyone who can reach the admin interface has every
  capability: manage stubs and groups, undo and redo, view and clear received requests, export
  and import.
- **U-2** The admin interface is **anonymous**: no sign-in and no access control. Pho is
  expected to run on a trusted network (a developer machine or an internal test network), as
  most mock servers are.
- **U-3** The mock-serving surface is **always unauthenticated**, because the system under
  test calls it as though it were the real service. Any future access control applies to the
  admin interface only, never to served mocks.
- **U-4** The design must not preclude placing an authentication layer (a directory-service
  sign-in is the expected candidate) in front of the admin interface in a later version.

## 4. Product decisions

These decisions are settled and shape the rest of the specification.

- **D-1 Verification is human-observed only.** Received requests are inspected by a person in
  the admin interface. There is no programmatic query interface and none is planned; automated
  tests do not assert on received requests from code.
- **D-2 No public authoring interface.** Stubs, groups, history and backups are managed only
  through the admin interface. The only public, machine-facing surface is the mock-serving
  surface.
- **D-3 Stubs have no priority or ordering.** A request is expected to match at most one
  enabled stub. When several enabled stubs match one request, that is a configuration error that
  Pho reports; it never silently picks a winner (see 6.2).
- **D-4 History is kept for the whole configuration**, not per stub. Every change produces a
  new revision of the entire configuration, which is what makes multi-item changes — above all
  a cascading group delete — undoable in one step.
- **D-5 Two separate surfaces.** The admin interface and the mock-serving surface are reached
  at different network addresses, so that a stub for any path can never collide with the admin
  interface's own pages (see section 9).
- **D-6 A single shared instance.** There is one global configuration. Everyone who uses an
  instance sees and edits the same stubs, history and received-request log.
- **D-7 Fidelity over leniency in matching.** Values compare case-sensitively unless a rule
  explicitly opts out, because a mock looser than the service it imitates turns a passing test
  into a production failure (see 5.5).
- **D-8 Plain, self-contained visual design.** The admin interface is specified by behavior,
  not appearance; it uses no third-party visual component library, and its colors come from a
  small set of named color roles so that a theme is just a redefinition of those roles (see 8.7).

## 5. Concepts

### 5.1 The configuration

The **configuration** is the complete set of stubs plus the complete group tree. It is what
history versions, what undo and redo restore, and what export and import carry. Received
requests are **not** part of the configuration.

### 5.2 Groups

- **G-1** A group is an organizational folder with a **name** (required, non-blank) and an
  optional **parent group**. A group without a parent is a top-level group.
- **G-2** Groups form a tree nested to any depth. A group may contain stubs, child groups,
  both, or nothing.
- **G-3** A group can never become its own ancestor: placing a group inside itself or inside
  any of its own descendants is refused.
- **G-4** Groups have **no effect on matching**. They exist purely to organize the admin
  interface.
- **G-5** Deleting a group is **cascading**: it removes the group, every descendant group, and
  every stub contained anywhere in that subtree. The whole cascade is a single change in
  history and is undone in one step.
- **G-6** Group names need not be unique.

### 5.3 Stubs

A stub is one mocking rule: a set of **matching rules** paired with a **response definition**.

- **S-1** Every stub has a stable unique identity that never changes once created and is
  preserved by export, import, undo and redo.
- **S-2** A stub has a **name** (always present once saved — see F1.4), an optional
  **description**, an optional containing **group** (none means it sits at the top level of the
  tree), an **enabled** flag, its matching rules, and its response definition.
- **S-3** A stub is **enabled** by default. A disabled stub is kept, shown and editable, but is
  ignored entirely during matching.

### 5.4 Matching rules

A request matches a stub only when **every** criterion the stub specifies is satisfied.
Criteria the stub does not specify do not constrain the match.

**Method.**

- **R-1** The method criterion is one of: ANY, GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS.
- **R-2** ANY matches every request method, including methods not in that list.
- **R-3** Any other choice matches only requests with that method, compared without regard to
  letter case.

**Path.** The path criterion is required and has a **path match type** and a value. It is
compared with the request path only — never the query string.

- **R-4** **Exact**: the request path equals the value, character for character, case-sensitive.
- **R-5** **Wildcard**: the value is a path template in which each `*` or `{name}` placeholder
  stands for one or more characters within a single path segment (any characters except `/`).
  Everything else in the template is literal. The template must match the whole path, end to
  end. Examples: `/users/*` and `/users/{id}` match `/users/123` and `/users/abc`, but not
  `/users` or `/users/123/orders`; `/users/{id}/orders` matches `/users/7/orders`.
- **R-6** **Regular expression**: the request path matches if the regular expression finds a
  match within it. The expression is not implicitly anchored; an author who needs the whole
  path to match writes the start and end anchors (for example `^/users/\d+$`).

**Query parameters, headers and body.** A stub may have any number of query-parameter rules
and header rules, and at most one body rule.

- **R-7** A query-parameter rule names a parameter and carries a **value rule**. Query
  parameter names compare **exactly** (case-sensitive). When a parameter occurs several times,
  its values are joined with commas into one value before comparison.
- **R-8** A header rule names a header and carries a value rule. Header **names** always
  compare **without regard to case** (`Content-Type` and `content-type` are the same header),
  and this is not configurable. When a header occurs several times, its values are joined with
  commas into one value before comparison.
- **R-9** A body rule is a value rule applied to the raw request body as text. A request that
  carries no body is treated as having no value at all (so PRESENT fails and ABSENT succeeds).
- **R-10** A stub with no rules of a kind is unconstrained by that kind.

**Value rules.** Each value rule has a type:

- **R-11** **EQUALS** — the value is present and equals the rule's value.
- **R-12** **CONTAINS** — the value is present and contains the rule's value as a substring.
- **R-13** **REGEX** — the value is present and the rule's regular expression finds a match
  within it (not implicitly anchored, as in R-6).
- **R-14** **PRESENT** — the parameter, header or body exists, whatever its content (an empty
  value counts as present). The rule has no value of its own.
- **R-15** **ABSENT** — the parameter, header or body does not exist. The rule has no value of
  its own.
- **R-16** A missing parameter, header or body never satisfies EQUALS, CONTAINS or REGEX.

### 5.5 Case sensitivity — names versus values

Names and values are separate decisions and are answered differently.

- **C-1** **Header names** are always compared without regard to case (HTTP field names carry
  no case meaning). Not configurable.
- **C-2** **Values are compared case-sensitively by default.** Header values are not
  case-insensitive in general, and some carry case-significant payloads: a Basic
  authentication credential is base64, whose alphabet is case-significant, so folding case would
  let a stub accept a credential it was written to reject.
- **C-3** **Ignore case** is a per-rule option that makes an EQUALS or CONTAINS rule compare its
  value without regard to case (for example a rule for `application/json` then matches a request
  sending `application/JSON`). It is **off by default** and affects only the rule it is set on.
- **C-4** Ignore case does not apply to REGEX rules — a regular expression states its own case
  insensitivity inline (with `(?i)`, optionally scoped to part of the pattern) and two competing
  ways of saying it would be a defect. It is meaningless for PRESENT and ABSENT, which compare no
  value.
- **C-5** An authentication scheme word (such as `Basic` or `Bearer`) is case-insensitive while
  the credential after it is not. Pho does **not** special-case the authorization header to
  encode that split; instead the Basic auth helper (F12) writes a REGEX rule whose scoped inline
  option folds the case of the scheme word alone. That one value can need case-insensitivity in
  one half and case-sensitivity in the other is precisely why ignore case is a per-rule option
  that REGEX rules do not use.

### 5.6 Response definitions

- **P-1** A response definition has a **status code** (an integer from 100 to 599; default 200),
  any number of **response headers** (name and value pairs, in order), and a **body** (text,
  possibly empty).
- **P-2** A request served by a stub receives exactly that status, those headers and that
  body. A stub with no response headers emits none of its own.
- **P-3** A response header named Content-Length (in any letter case) is not emitted as
  written; the length of the response is always determined from the actual body, so a stale
  length can never corrupt a response.
- **P-4** There are no delay, fault or templating options in this version.

### 5.7 Received requests

- **Q-1** A received request is an immutable record of one request that arrived on the
  mock-serving surface. **Every** such request is recorded, whether it matched one stub, none,
  or several. Requests to the admin interface are never recorded.
- **Q-2** The capture is verbatim: the record holds the time received, the method, the path,
  the raw query string, every header with its value, and the complete, unmodified body.
- **Q-3** The record also holds the **match outcome** — matched one stub, matched none, or
  ambiguous (matched several) — the identity of the matched stub (for one match) or of **every**
  matched stub (for an ambiguous match), and the status code actually returned.
- **Q-4** Received requests are kept for a limited time (see 10 and 11.2) and can be cleared
  on demand. Undo, redo, export and import never touch them.

### 5.8 Configuration history

- **H-1** Every change to the configuration produces a new **configuration revision**: creating,
  saving, duplicating, deleting, enabling, disabling or moving stubs; creating, renaming,
  moving or deleting groups; and importing a file. Each revision records a sequence position,
  the time it was made, a human-readable **summary** of the change (for example "Deleted
  1 group and 3 stubs"), and the **complete configuration** after the change.
- **H-2** One user action is one revision, however many items it touches: a cascading group
  delete, a multi-row delete, a multi-row move, a bulk enable or disable, and an import are each
  a single revision.
- **H-3** History is linear. A **current position** marks the revision the live configuration
  corresponds to. Before any change has ever been made, the position is an implicit empty
  baseline (no stubs, no groups).
- **H-4** **Undo** restores the configuration of the revision before the current one and moves
  the position back; undoing the very first change restores the empty baseline. **Redo** restores
  the next revision and moves the position forward.
- **H-5** Because every change is just a revision, every change is undoable and redoable in the
  same way, with no special handling per kind of change. In particular, undoing a cascading
  group delete restores the entire removed subtree — groups and stubs with their full
  definitions and original identities — in one step.
- **H-6** Making a new change while not at the newest revision discards every revision after
  the current position; those changes can no longer be redone.
- **H-7** Restoring a revision reproduces it exactly, including stub identities, so a
  restored stub is the same stub, not a copy.
- **H-8** Revisions older than the configuration-history retention window (see 10) are removed
  automatically, and undo cannot reach past the oldest remaining revision.
- **H-9** There is no per-stub history and no way to revert one stub in isolation; only the
  whole-configuration undo and redo exist.
- **H-10** History is separate from the received-request log: it records configuration, not
  traffic.

## 6. Serving mocks: matching and resolution

### 6.1 What the mock-serving surface accepts

- **M-1** The mock-serving surface accepts any request, with any method and any path. Every
  request it receives is mock traffic; it has no paths of its own.
- **M-2** It always evaluates the current saved configuration: a change saved in the admin
  interface (including enabling, disabling, undo, redo and import) affects the very next
  request.
- **M-3** It never requires or checks authentication.

### 6.2 Resolution

For each request:

- **M-4** Only **enabled** stubs are considered.
- **M-5** The stubs whose matching rules all match the request are selected.
- **M-6** **Exactly one match** — Pho returns that stub's status, headers and body.
- **M-7** **No match** — Pho returns status **404** with a short plain-text body stating that no
  stub matched the request ("No stub matched this request.").
- **M-8** **More than one match — an ambiguous match, which is an error.** Pho does not choose
  between them (there is no priority). It:
  - returns status **500** with a plain-text body that says the request matched multiple stubs,
    names every matched stub by name and identity, and tells the author to disable or narrow
    all but one, because stubs have no priority;
  - writes an error to the application log naming every matched stub (name and identity)
    together with the request's method and path; and
  - flags the request as an ambiguous match in the received-requests view, identifying the
    stubs it matched (see F5 and 8.5).
- **M-9** Every request is recorded as a received request with its outcome and the status
  returned (see 5.7), regardless of outcome.
- **M-10** Grouping has no influence on any of the above.

## 7. Features

### F1 — Manage stubs

- **F1.1 Create.** The author starts a new stub, fills in its matching rules and response, and
  saves it. The saved stub appears in the tree and is selected.
- **F1.2 New stub defaults.** A new stub starts enabled, matching method GET with an exact path
  of `/`, with no query, header or body rules, responding with status 200, no headers and an
  empty body. It is placed in the group of the row that was last activated in the tree (a group
  row means that group; a stub row means that stub's group), or at the top level if none.
- **F1.3 View and edit.** Selecting exactly one stub opens it in the editor, where every part of
  its definition can be changed and saved. Each save is one undoable change.
- **F1.4 Default name.** A stub saved with a blank name is named after what it matches — the
  method in capitals, a space, and the path (for example "POST /sessions"); with a blank path the
  name is just the method. While the name box is blank it shows that default as a hint, and the
  hint follows the method and path as they are edited. A name the author typed is kept as
  typed (surrounding spaces removed).
- **F1.5 Validation.** Saving is refused, nothing is stored, and every applicable message is
  shown inline, when:
  - the path is blank — "Path is required.";
  - the path match type is regular expression and the path does not compile — "Path is not a
    valid regular expression: ‹path›";
  - the status is outside 100–599 — "Response status must be a valid HTTP status code
    (100-599).";
  - a query-parameter or header rule row has a value but no name — "Query parameter rules need
    a name." / "Header rules need a name.";
  - a query-parameter or header REGEX rule does not compile — "Query parameter ‹name› is not a
    valid regular expression." / "Header ‹name› is not a valid regular expression.";
  - the body rule is REGEX and does not compile — "The request body rule is not a valid regular
    expression.";
  - a response header row has a value but no name — "Response headers need a name.";
  - the name is blank after the default has been applied — "Name is required." (in practice
    unreachable, since the default always supplies one).
- **F1.6 Tidying on save.** Rule rows and response-header rows whose name and value are both
  blank are dropped. Rule and header names are stored with surrounding spaces removed. A blank
  description is stored as no description. A PRESENT or ABSENT rule keeps no value, and a rule
  type that cannot use ignore case never stores it as on, even if the box was ticked while the
  row had another type.
- **F1.7 Match on request headers.** The author adds header rules (name plus EQUALS, CONTAINS,
  REGEX, PRESENT or ABSENT); the stub then matches only requests whose headers satisfy every
  rule, with header names compared without regard to case.
- **F1.8 Ignore case per rule.** Each EQUALS or CONTAINS rule — query parameter, header or body
  — offers an ignore-case option, off by default, as described in 5.5. REGEX, PRESENT and
  ABSENT rules offer no such option.
- **F1.9 Emit response headers.** The author adds response-header rows (name and value); a
  request served by the stub receives them alongside the status and body.
- **F1.10 Unsaved changes.** Edits are held as a draft until saved. The editor shows whether
  there are unsaved changes, and offers **revert** to return to the last saved state. Adding or
  removing a rule row, or changing any field or option, counts as a change; changing something
  and changing it back does not.
- **F1.11 Leaving with unsaved changes.** Any action that would replace or discard the draft —
  selecting a different row in the tree such that a different stub (or none) would be shown,
  starting a new stub, closing the editor, duplicating, undo, redo, import, or navigating to
  another screen — first asks the author to **save and continue**, **discard changes** and
  continue, or **cancel** and stay. If saving fails validation, the author stays with the
  draft and the messages. Closing or reloading the browser page with unsaved changes triggers
  the browser's own leave-page warning.
- **F1.12 Duplicate.** A saved stub can be duplicated. The copy has a new identity, the same
  description, group, matching rules and response, the name "Copy of ‹original name›", and is
  **disabled**, so it does not immediately collide with its source (two enabled stubs with
  identical rules would be an ambiguous match). The copy is selected and opened in the editor and
  is edited independently of the original. Duplicating is one undoable change.
- **F1.13 Delete.** Stubs are deleted from the tree's selection (see F3.6); the editor itself has
  no delete action.

### F2 — Enable and disable stubs

- **F2.1** Disabling a stub keeps it but excludes it from matching; requests it would have
  matched fall through to other stubs or to the no-match response.
- **F2.2** Enabling a stub makes it take part in matching again.
- **F2.3 Toggle between responses.** With two stubs for the same endpoint returning different
  responses, one enabled and one disabled, flipping which one is enabled switches the endpoint's
  response without editing either.
- **F2.4 One stub.** The editor has an enabled switch, saved with the rest of the stub.
- **F2.5 Many at once.** With a selection of tree rows (stubs, groups or both), the toolbar's
  **enable** and **disable** actions flip every stub the selection covers — including every
  stub nested anywhere inside a selected group — as **one** undoable change.
- **F2.6** Each action shows how many stubs it would change and is offered only while that
  number is above zero (nothing to enable when every covered stub is already enabled).
- **F2.7** An open, unedited editor showing an affected stub is refreshed to the new state.

### F3 — Organize stubs into nested groups

- **F3.1 Create.** The author names a new group and chooses where it goes — the top level or
  inside any existing group (the chooser lists every group, indented to show nesting, and
  defaults to the group of the last activated row). A blank name does nothing; the name is
  stored with surrounding spaces removed. The parent group is expanded so the new group is
  visible.
- **F3.2 Rename.** A group can be renamed; the rename is one undoable change.
- **F3.3 Nesting.** Groups nest to any depth and the tree shows the hierarchy. Any operation
  that would make a group its own ancestor is refused.
- **F3.4 Placement of stubs.** A stub lives in one group or at the top level; stubs at the top
  level appear at the root of the tree. Stubs are moved between groups by dragging (F3.5); the
  editor has no group field.
- **F3.5 Rearrange by dragging.** Dragging a stub or group onto a group row moves it into that
  group; dropping onto a stub row means "into the group holding that stub"; dropping onto the
  top-level drop area moves it to the root.
  - Dropping a group into itself or any of its descendants is refused, as is a drop where the
    row already lives, and dropping a row onto itself does nothing.
  - If the dragged row is part of the current selection, the whole selection moves; otherwise
    only the dragged row moves.
  - A moving group carries its contents. Selected groups nested under another selected group
    are not moved separately, and selected stubs inside a moving group stay in their group.
  - Rows that cannot legally move, or would not change place, are skipped while the rest move.
    If nothing moves, nothing is recorded.
  - All the moves of one drop are **one** undoable change, and the target group is expanded so
    the moved rows stay visible.
- **F3.6 Select many rows and delete them together.** Rows are selected with the row checkbox,
  ctrl/cmd-click (add or remove one row), shift-click (a range) or the keyboard (F11), and
  removed with **delete selected**. Before deleting, Pho states how much will go — counting
  every group and stub inside selected groups, for example "Delete 2 groups and 1 stub?" —
  warns that a group is deleted with everything inside it, notes that this can be undone, and
  asks for confirmation. On confirmation everything is deleted as **one** undoable change. Rows
  already covered by a selected group are not deleted twice. Individual rows carry no delete
  button.
- **F3.7 Cascade.** Deleting a non-empty group deletes all its descendant groups and every
  stub inside them (G-5); undo restores all of it in one step (H-5).
- **F3.8 Expand and collapse in bulk.** The tree opens with every group collapsed. **Expand
  all** and **collapse all** act on every group when nothing is selected, and only on the
  selected groups and the groups nested under them when something is selected (a selection of
  stubs only affects no groups).
- **F3.9 Grouping does not affect matching** (G-4).

### F4 — Match requests and serve responses

- **F4.1** An enabled stub whose rules match an incoming request answers it with its status,
  headers and body (M-6).
- **F4.2** Path matching supports exact, wildcard and regular-expression types (R-4–R-6); query
  parameters, headers and body support EQUALS, CONTAINS, REGEX, PRESENT and ABSENT (R-11–R-15);
  a request matches only when every specified criterion is satisfied.
- **F4.3 Case sensitivity.** A header rule naming `Content-Type` applies to a request header
  spelled `content-type`. An EQUALS or CONTAINS rule without ignore case does not match the
  same value in different case. Both as in 5.5.
- **F4.4 Ambiguous match is an error** — 500, logged, flagged (M-8).
- **F4.5 No match** — 404 (M-7).
- **F4.6 Disabled stubs are ignored** (M-4).

### F5 — Verify received requests

- **F5.1 Recording.** Every request to the mock-serving surface is recorded (Q-1–Q-3), including
  those that match no stub (whose caller receives 404) and ambiguous ones (500).
- **F5.2 Order.** The received-requests view always lists requests newest first by time
  received, across all pages and under any filter. Requests received at the same instant
  still have a stable order.
- **F5.3 Filter by method.** A method filter limits the list to requests with exactly that
  method.
- **F5.4 Filter by path.** A path filter limits the list to requests whose path **contains** the
  entered text (substring, not only exact). The two filters combine. Surrounding spaces in
  filter text are ignored, and a blank filter means no filter. Applying a filter returns to
  the first page.
- **F5.5 Pagination.** The list is paged with a choice of **10, 20, 50 or 100** requests per page
  (20 by default). The view shows the total number of matching requests and the current page out
  of the total pages, with previous and next navigation offered only where there is a page to
  go to. Changing the page size returns to the first page.
- **F5.6 Summary per request.** Each listed request shows the time received (in UTC), method,
  path, outcome — the matched stub, "no match", or an ambiguous-match flag with the number of
  stubs matched — and the status returned.
- **F5.7 Full detail.** Opening a request shows it exactly as received: the query string (or an
  indication there was none), every header with its value, and the complete body (or an
  indication it was empty), together with the time received, the outcome and the status
  returned. For an ambiguous match the detail identifies every stub that matched, so
  overlapping stubs can be found and fixed.
- **F5.8 Clear.** The author can clear the log; the view is then empty. Clearing is not part of
  configuration history and cannot be undone.
- **F5.9** Verification is human-observed only (D-1).

### F6 — Undo and redo configuration changes

- **F6.1 Undo** reverses the most recent change not yet undone and returns the configuration to
  its prior state (H-4).
- **F6.2 Redo** reapplies the most recently undone change, provided no new change has been made
  since (H-4, H-6).
- **F6.3 Cascade.** Undoing a cascading group delete restores the whole deleted subtree in one
  step (H-5).
- **F6.4 New change clears redo** (H-6).
- **F6.5 Bounded by retention** (H-8).
- **F6.6 Availability.** Undo is offered only while there is something to undo, and redo only
  while there is something to redo.
- **F6.7** Undo and redo are global: they act on the shared configuration, whoever made the
  change (D-6).
- **F6.8** After undo or redo the tree shows the restored configuration; rows that no longer
  exist drop out of the selection, and the editor follows the remaining selection.

### F7 — Export and import (backup and restore)

- **F7.1 Export** produces a single downloadable file containing the **complete configuration**:
  every stub with all of its matching rules, options and response details, its enabled state,
  identity and group, and the full group tree. Received requests and configuration history are
  not included.
- **F7.2 Import** reads such a file and applies it in one of two modes chosen beforehand:
  - **replace all** — the existing configuration is cleared and replaced by the file's;
  - **merge** — groups and stubs in the file are added, or, where one with the same identity
    already exists, updated to the file's version; everything not in the file is left as it is.
- **F7.3 Round trip.** Exporting and later importing the same file with replace all reproduces
  the exported configuration exactly — every stub with all its detail, and the entire group
  hierarchy, with the same identities.
- **F7.4 Older files.** A file exported by an earlier version that lacks a later rule option
  imports successfully, each missing option taking its default — an absent ignore-case option
  imports as off, so the configuration matches exactly as it did when exported.
- **F7.5 Invalid files.** A file that is not well-formed, or is not a Pho export, is rejected
  with a clear error ("Import failed: …", stating that the file is not valid or not a valid Pho
  export), and the existing configuration is left unchanged.
- **F7.6 Limits.** Import accepts files up to 10 MB.
- **F7.7 History.** A successful import is one undoable change.
- **F7.8 After import** the selection and editor are cleared and the tree is shown fully
  collapsed. An import attempted with unsaved editor changes asks first (F1.11).

### F8 — Body formatting and validation (JSON and XML)

Both the request-body rule value and the response body offer these helpers.

- **F8.1 Format.** Format-as-JSON and format-as-XML pretty-print the content in place with
  indentation, and report success. Formatting counts as an edit of the draft.
- **F8.2 Validate.** Validate-as-JSON and validate-as-XML report whether the content is
  well-formed, and on failure show the parser's error, including the location where available.
  Validation does not change the content.
- **F8.3** If content cannot be parsed, format leaves it unchanged and shows the error. Empty or
  blank content is reported as not valid ("Empty content.").
- **F8.4 Advisory only.** Bodies may hold any text — plain text, or a deliberately malformed
  payload for a test. Formatting and validation are conveniences and never prevent saving.
- **F8.5** The result message clears as soon as the content is edited.

### F9 — Spell-check hints

- **F9.1** Free-text fields — stub name, description, the request-body rule value and the
  response body — indicate possible misspellings as the author types.
- **F9.2** Hints are advisory: they never block saving and never alter the stored value
  (a flagged word may be an intentional identifier, code or domain term).

### F10 — Light and dark theme

- **F10.1** By default the interface follows the system's or browser's light or dark
  preference.
- **F10.2** The author can choose light or dark explicitly, or return to following the system.
- **F10.3** An explicit choice persists across visits in that browser until changed, and is in
  effect from the first moment a page is shown, so the page never flashes the wrong theme.

### F11 — Navigate the stub tree from the keyboard

- **F11.1 Focus.** The tree can take keyboard focus; while it has focus, the navigation keys
  act on the tree and do not scroll the page.
- **F11.2 Move.** Down and up move the active row to the next or previous visible row, in
  display order; with no active row, down goes to the first row and up to the last. Home and
  End go to the first and last row. The active row becomes the selection (holding shift
  extends a range instead) and is scrolled into view.
- **F11.3 Expand and collapse.** On a collapsed group that has contents, right expands it; on
  an expanded group, right moves to its first child. On an expanded group that has contents,
  left collapses it; otherwise left moves to the row's parent group.
- **F11.4 Select.** Space adds the active row to, or removes it from, the selection, as
  ctrl/cmd-click does.
- **F11.5 Delete.** The delete key raises the same confirmation as **delete selected**.
- **F11.6 Editor follows selection.** Whenever exactly one stub is selected, by any means, the
  editor shows it; selecting a group, several rows, or nothing shows no editor. Moving off a
  stub with unsaved changes asks first (F1.11).

### F12 — Basic auth credential helper

Mocked services are often protected by HTTP Basic authentication, whose header value is a base64
encoding of "user id:password". The editor encodes and decodes this so the author never does it
by hand.

- **F12.1 Build.** Beside the header rules the author enters a user id and a password and
  applies them. Pho sets a header rule named Authorization, of type **REGEX**, whose value is
  `^(?i:Basic)\s+‹credential›$`, where ‹credential› is the base64 of the UTF-8 text
  "user id:password". It replaces any Authorization rule already on the stub (found by name,
  regardless of case) rather than adding a second one, and the rule's ignore-case option is off.
  The apply action is offered once either box has content; an empty password is allowed. The
  boxes show their text unmasked (these are test credentials).
- **F12.2 Scheme case only.** The stub then matches a request whose Authorization header is
  `Basic`, `BASIC` or `basic` (any letter case), followed by one or more spaces, followed by
  exactly that credential. The case-insensitive part covers the scheme word alone; the
  credential stays case-sensitive, so the right scheme with a different credential does not
  match.
- **F12.3 Only the real scheme.** A misspelled scheme (`BASSIC`, `Basi`, `Basicc`), a different
  scheme (`Bearer`), or any extra text before or after the value does not match, because the
  pattern spells the scheme out and is anchored at both ends.
- **F12.4 Escaping.** The credential is escaped before being placed in the pattern, so
  characters that are special in regular expressions (base64 produces `+`, for example) are
  matched literally.
- **F12.5 Read back.** Hovering over a rule value that holds a credential shows the decoded
  "User ID: ‹id› · Password: ‹password›" (an empty password reads "(empty)"). This works for a
  pattern the helper built and for a plain value of the form "Basic ‹base64›" (scheme in any
  case, surrounding spaces tolerated) typed by hand or imported from an older configuration.
- **F12.6 Prefill.** When a stub is opened, the helper's boxes are filled from the credential
  its Authorization rule already holds (in either form), and the helper indicates that the stub
  is matching a Basic credential, so an existing credential can be read and edited rather than
  retyped.
- **F12.7 Splitting.** The decoded text is split at its **first** colon: a password may contain
  colons, a user id may not. Non-ASCII user ids and passwords round-trip.
- **F12.8 Leave anything else alone.** A value that is not a credential — another scheme's
  value, a regular expression of any shape other than exactly the one the helper builds,
  invalid base64, base64 without a scheme, or decoded text with no colon — offers no decoded
  reading and is treated as an ordinary value.

## 8. Screens, layout and navigation

### 8.1 Overall

- **L-1** The admin interface is used in a web browser and behaves as a single interactive
  application: actions take effect without full page reloads.
- **L-2** A header shows the product name ("Pho — Mock Web Service"), navigation between the
  two screens — **Stubs** (the starting screen) and **Received requests** — and the theme
  control (light, dark, follow system).

### 8.2 Stubs screen — toolbars

From top to bottom:

- **L-3 Backup toolbar.** Export, the import mode choice (replace all, the default, or merge)
  and the import file picker sit at the very top of the screen, deliberately above and apart
  from the authoring controls, so they do not interrupt the authoring flow. Import errors are
  shown here.
- **L-4 Authoring toolbar.** New stub; a new-group name box with its "in" chooser (top level
  or any group) and an add-group action; delete selected (showing how many rows are selected,
  offered only with a selection); enable and disable (each showing how many stubs it would
  change, F2.6); undo and redo (F6.6).
- **L-5 Prompts.** The delete confirmation (F3.6) and the unsaved-changes prompt (F1.11) appear
  as bars above the workspace.

### 8.3 Stubs screen — the tree (left pane)

- **L-6** The tree shows groups as expandable and collapsible nodes nested to any depth, with
  stubs under their group and top-level stubs at the root. At every level, groups come first,
  then stubs; groups are ordered by name and stubs by the label they show, both
  alphabetically without regard to case.
- **L-7** The tree opens fully collapsed (F3.8), and is collapsed again after an import.
- **L-8 One row, one line.** A row never wraps. The tree scrolls horizontally for long labels
  and vertically within its own bounded height, so the editor beside it stays on screen however
  long the tree grows. Rows are indented by depth.
- **L-9 Rows.** Each row has a selection checkbox. A group row has an expand/collapse control
  (shown inert for an empty group), a folder marker and the group's name. A stub row shows
  **either** its name **or**, when it has none, the method and path it matches (for example
  "GET /users/1") — never both. A disabled stub is struck through and tagged "disabled". Rows
  carry no other buttons: delete, enable and disable are toolbar actions over the selection.
- **L-10 Selection.** A plain click selects just that row (and makes it the range anchor);
  ctrl/cmd-click or the checkbox adds or removes one row; shift-click selects the range between
  the anchor and the clicked row, replacing the previous selection (with no anchor it acts as a
  plain click). Groups and stubs can be selected together. After any change, rows that no
  longer exist drop out of the selection.
- **L-11 Drag and drop** as in F3.5, with the hovered drop target highlighted and a top-level
  drop area ("Drop here to move to the top level") beneath the rows.
- **L-12** Expand all and collapse all sit above the tree (F3.8), with a short hint explaining
  the click, keyboard and drag interactions.
- **L-13 States.** Loading; **empty** — no groups or stubs yet, with a prompt to create the
  first one; populated.

### 8.4 Stubs screen — the editor (main pane)

- **L-14** The editor is a panel beside the tree, not a separate page. It shows exactly when one
  stub is selected (or a new stub is being created). With several rows selected the pane says
  how many and suggests deleting them together, dragging them into a group, or selecting one
  stub; with nothing selected it invites selecting or creating a stub.
- **L-15 Header.** The stub's name ("New stub" for an unsaved one), a status — unsaved changes,
  not saved yet, or saved — and the actions **save** (offered for a new stub or when there are
  unsaved changes), **revert** (offered when there are unsaved changes), **duplicate** (saved
  stubs only) and **close**.
- **L-16** Validation messages appear as a list at the top of the panel and clear on the next
  edit.
- **L-17 General fields.** Name (with the default-name hint, F1.4), description, and the
  enabled switch. There is no priority control and no group control.
- **L-18 Request.** Method; path with its match type on the same line, the type as a control to
  the right of the path box.
- **L-19 Collapsible rule sections.** Query-parameter rules, header rules and response headers
  are each a collapsible section whose heading states how many rows it holds (or "none"). Each
  section starts open if the stub has rows of that kind and closed if it has none, so an
  ordinary stub is a short form.
- **L-20 Rule rows.** Each query-parameter or header rule row is a single line: name, rule type,
  value (replaced by a note that the rule checks existence only for PRESENT and ABSENT), a
  compact ignore-case checkbox shown only for EQUALS and CONTAINS (switching the type shows or
  hides it), and a remove action. An empty section says that any value matches. Each section
  has an add-rule action. The header-rules section notes that a stub matches only when every
  header rule matches and that header names compare without regard to case.
- **L-21 Basic auth helper.** Inside the header-rules section, beneath the rules (F12).
- **L-22 Body rule.** A "match on the request body" switch; when on, the rule type, the
  ignore-case option for EQUALS and CONTAINS, and — for types that take a value — a body editor
  with the format and validate helpers (F8).
- **L-23 Response.** Status; the response-headers section (name and value rows, with an
  add action and a note when empty that only the status and body are returned); the response
  body editor with the format and validate helpers.
- **L-24 Flow.** Select a stub → edit → save (stored, tree refreshes, stub stays selected) or
  revert; leaving with unsaved changes prompts (F1.11).

### 8.5 Received-requests screen

- **L-25 Controls.** A method filter box, a path-contains filter box, an apply-filter action, the
  page-size choice, and **clear log**.
- **L-26 List.** As in F5.2–F5.6, with each row offering a details toggle that expands the full
  request detail (F5.7) beneath the row; one request's detail is open at a time.
- **L-27 Ambiguous matches are flagged** prominently as errors, with the number of stubs matched
  and, in the detail, which stubs they are.
- **L-28 States.** Loading; empty ("No received requests."); populated.

### 8.6 Export and import flow

- **L-29** Export downloads the configuration file immediately (F7.1).
- **L-30** Import: choose the mode, then choose a file. On success the tree refreshes (collapsed)
  and the import is undoable; on failure an error is shown in the backup toolbar and nothing
  changes. Unsaved editor changes prompt first.

### 8.7 Visual design and theming

- **L-31** Screens are specified by behavior, not visual design; the points here fix only what
  behavior depends on.
- **L-32** Colors come from a single set of named color roles — background, foreground, border,
  muted text, accent and error — defined once; the light and dark themes are two definitions of
  that set rather than per-element rules.
- **L-33** The theme follows the system preference unless an explicit choice is stored; the
  stored choice overrides the preference and is applied before the first paint; returning to
  "follow system" forgets the stored choice.

## 9. The two surfaces and hosting behind a reverse proxy

### 9.1 Two surfaces

- **N-1** Pho has two separate surfaces: the **admin interface** (for people, in a browser) and
  the **mock-serving surface** (for the system under test).
- **N-2** Each surface is reached at its own network address, and each address is
  independently configurable, so a stub for any path — including paths that look like admin
  pages — can never collide with the admin interface.
- **N-3** Every request arriving at the mock-serving surface is mock traffic (M-1). Nothing
  arriving at the admin interface is ever treated as mock traffic or recorded as a received
  request.

### 9.2 Admin interface behind a reverse proxy

- **N-4** The admin interface works both when served at the root of a host and when a reverse
  proxy mounts it under a **sub-path** (for example `https://host/Pho/`).
- **N-5** Nothing in Pho hardcodes a prefix. Every link, resource reference and download the
  admin interface emits is relative to the page's base location, which is the sub-path when
  there is one and the root otherwise. Direct access without a proxy is unaffected.
- **N-6** Pho learns the sub-path either from the proxy announcing on each request the prefix it
  removed (Pho also honors the original scheme and host announced alongside it), or from a
  configuration setting — for proxies that remove the prefix without announcing it, and for
  proxies that pass the prefix through unchanged.
- **N-7** The configured sub-path is accepted with or without leading and trailing slashes
  ("Pho/admin", "/Pho/admin" and "/Pho/admin/" are equivalent).
- **N-8** Proxy-announced information is accepted from any source, since Pho runs on a trusted
  test network and the information affects only how the admin interface builds its links.
- **N-9** A sub-path applies to the admin interface only. It never changes what the
  mock-serving surface receives or what a stub matches.

## 10. Configuration settings

An operator can configure the following, each with a sensible default so that Pho runs with no
configuration at all:

| Setting | Default | Effect |
|---|---|---|
| Admin interface address | a fixed well-known default | Where the admin interface is reached. |
| Mock-serving surface address | a different fixed well-known default | Where the system under test sends requests. |
| Admin sub-path | none (served at the host root) | The prefix under which a reverse proxy serves the admin interface (N-6). |
| Storage location | a local default | Where the persistent configuration, history and log are kept. |
| Received-request retention | 1 day | Received requests older than this are removed automatically. |
| Configuration-history retention | 1 year | Revisions older than this are removed automatically; bounds undo depth. |

## 11. Quality requirements

### 11.1 Persistence

- **Y-1** Stubs, groups, configuration history (including the current undo position) and
  received requests are kept in persistent storage and survive restarts.
- **Y-2** On first start Pho creates empty storage by itself; no manual set-up step is
  needed.
- **Y-3** Stored data survives stopping, restarting, upgrading and redeploying Pho, unless the
  operator explicitly deletes it.
- **Y-4** Backup of the mock configuration is handled within the product by export and import
  (F7), independently of any storage-level backup.

### 11.2 Retention

- **Y-5** The received-request log and the configuration history are bounded by their retention
  settings rather than growing without limit.
- **Y-6** Pruning is automatic and age-based. Received requests past the window are removed no
  later than the next time a request is recorded; revisions past the window are removed
  automatically and undo depth is limited to what remains.

### 11.3 Concurrency and consistency

- **Y-7** One instance is shared by everyone (D-6). Several test authors and several systems
  under test may use it at the same time.
- **Y-8** Concurrent edits and concurrent mock traffic must not corrupt stored state. The
  mock-serving surface always reflects the currently stored, enabled stubs (M-2).
- **Y-9** History is shared: an undo or redo by one author acts on the latest change made by
  anyone.

### 11.4 Performance

- **Y-10** The mock-serving surface sits on the hot path of other teams' test suites; its
  response latency must be low and predictable at typical stub counts. No numeric target is
  set for this version.

### 11.5 Security

- **Y-11** The mock-serving surface is unauthenticated by design (U-3).
- **Y-12** The admin interface is anonymous in this version and assumes a trusted network
  (U-2); a later version may add a sign-in in front of it (U-4).

### 11.6 Observability

- **Y-13** Pho writes an application log sufficient to diagnose matching problems and errors;
  every ambiguous match is logged as an error naming the matched stubs and the request (M-8).
- **Y-14** No metrics surface is provided in this version.

## 12. Glossary

- **Stub** — one mocking rule: matching rules paired with a response definition. Pho's central
  concept. Also called a "mock" or "mapping" elsewhere.
- **Mock** — informal synonym for a stub, and the general name for what Pho does.
- **Matching rules** — the part of a stub that says which requests it applies to: method, path,
  query-parameter rules, header rules and an optional body rule.
- **Path match type** — how the path is compared: exact, wildcard or regular expression.
- **Value rule** — how one value is compared: EQUALS, CONTAINS, REGEX, PRESENT or ABSENT.
- **Ignore case** — a per-rule option making an EQUALS or CONTAINS rule compare its value
  without regard to case. Off by default. Unrelated to header names, which always compare
  without regard to case.
- **Response definition** — the part of a stub that says what Pho returns: status, headers,
  body.
- **Enabled / disabled** — a stub's on/off state; disabled stubs are kept but ignored while
  matching.
- **Group** — an organizational folder for stubs and other groups; groups nest into a tree.
- **Cascade delete** — deleting a group removes everything nested under it, as one undoable
  change.
- **Configuration** — all stubs plus the whole group tree.
- **Configuration revision** — an immutable record of the whole configuration after one change,
  with a summary and a time.
- **Undo / redo** — stepping backward and forward through configuration revisions.
- **Ambiguous match** — the error condition in which more than one enabled stub matches a
  request; answered with 500, logged, and flagged.
- **No match** — no enabled stub matches; answered with 404.
- **Received request** — the verbatim record of one request that arrived on the mock-serving
  surface, with its outcome and returned status.
- **Verification (spying)** — inspecting received requests to confirm the system under test
  made the expected calls.
- **Mock-serving surface** — what the system under test calls; behaves like the real service and
  is unauthenticated.
- **Admin interface** — the browser-based interface in which test authors manage stubs, groups,
  history, backups and received requests.
- **System under test** — the software being tested, whose requests Pho answers.
- **Export / import** — writing the whole configuration to a file, and restoring it from one.
- **Single shared instance** — one global configuration per running Pho, with no per-session
  isolation.
