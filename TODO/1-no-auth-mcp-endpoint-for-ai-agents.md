# 1 — No-auth MCP endpoint for AI agents

## Summary
Expose an MCP (Model Context Protocol) server endpoint, with no authentication, through
which AI agents can read and edit everything a human can do in Pho's admin UI.

## Motivation
The user wants agents (Claude Code, other MCP clients) to be able to drive Pho directly —
set up stubs for a test run, inspect what the system under test sent, tidy up afterwards —
without a human clicking through the web UI.

## Behavior
- An MCP client can connect to Pho with no credentials and discover a set of tools (and,
  where it fits, resources) covering **all of Pho's features** — parity with the admin UI:
  - **Stubs** — list, get, create, update, delete, duplicate (copy into the same group,
    created disabled), enable/disable.
  - **Groups** — list as a tree, create, rename, cascade-delete, move; assign a stub to a
    group.
  - **Configuration history** — list the revisions in which a stub changed, inspect a
    revision, revert a stub to an earlier version.
  - **Undo / redo** — undo, redo, report whether each is available.
  - **Received requests** — list (newest first, filter by method and URL-path substring,
    paginated), get full detail (method, path, query, every header, full body, time
    received), clear.
  - **Export / import** — export the full definition set; import with replace-all or merge,
    with the same validation the UI applies.
  - **Helpers with agent value** — e.g. body format/validate (JSON/XML) and the Basic auth
    credential helper, if useful to an agent. Purely visual UI features (theme, keyboard
    tree navigation, spell-check hints) have no MCP equivalent.
- Edits made through MCP behave exactly like edits made in the UI: same validation, same
  errors, and each change is a configuration revision, so it appears in history and can be
  undone/redone from either the UI or MCP.
- Errors are returned as MCP tool errors with a clear message (e.g. validation failure,
  unknown stub/group id), never as a crash.
- Requests to the MCP endpoint are **not** recorded as received requests and never
  collide with stub paths.

## Acceptance criteria
- An MCP client connecting without credentials can list the tools and call each one.
- Every operation listed above is reachable via MCP (a parity checklist against the
  admin UI's operations).
- A stub created via MCP is served on the mock surface and visible in the admin UI;
  undoing it in the UI removes it, and vice versa.
- An agent can read back a received request in full detail via MCP.
- Invalid input (e.g. malformed import, unknown id) produces an MCP tool error, and leaves
  the configuration unchanged.
- The MCP endpoint is served on the admin surface (and under the configured sub-path when
  behind a reverse proxy), not on the mock surface.
- MCP traffic is not logged as received requests.
- An agent can read the stub tree, a stub, a received request and the history as MCP
  resources.
- A change made through MCP appears in history marked as made via MCP, with the agent's
  client name/version and, when supplied, the self-reported "on behalf of" value; a change
  made in the UI is marked as made in the admin UI.
- Acceptance scenarios for this live as executable `.feature` files (in the
  feature files of `Pho.Test.Acceptance`).

## Technical notes
- **Conflicts with current spec decisions — these must be revised when this is built:**
  - SPEC.md D-2 and AGENTS.md: "No public authoring API in v1 —
    Blazor components call in-process services directly." An MCP endpoint *is* a public
    authoring surface.
  - SPEC.md D-1 and F5.9 (verification is human-observed only): "There
    is no programmatic query endpoint and none is planned." MCP read access to received
    requests reverses this.
  - SPEC.md section 2.2 lists a public configuration API as a v1 non-goal.
  - SPEC.md U-2: anonymous authoring is already the v1 stance, so
    no-auth is consistent with it.
- **Library:** the official C# MCP SDK (`ModelContextProtocol.AspNetCore`) provides
  Streamable HTTP transport via `builder.Services.AddMcpServer().WithHttpTransport()` and
  `app.MapMcp("/mcp")`, with tools as attributed methods. Check its current version and
  .NET 10 support.
- **Where it plugs in:** `src/Pho.Web/Program.cs`. Map it on the **admin port** — decided
  by the user; no separate port (also, the mock
  port routes every request through `MockServingMiddleware` via `app.MapWhen(policy.IsMockTraffic, …)`,
  so `/mcp` there would be treated as mock traffic). It must also honour `Pho:PathBase`
  (sub-path behind a reverse proxy), like `/health` and `/export` do.
- **Tools wrap the existing in-process services** — no new business logic:
  `StubService`, `GroupService`, `TreeEditService`, `ConfigHistoryService`
  (`src/Pho.Domain`), `IReceivedRequestLog` / `EfReceivedRequestLog`,
  `IConfigPorter` / `EfConfigPorter` (`ExportJsonAsync`, import) in `src/Pho.Infrastructure`.
- **Concurrency / live UI:** the Blazor Interactive Server UI may hold state from before an
  MCP edit. Check whether open UI sessions refresh (or at least don't overwrite) after an
  agent changes the configuration; SPEC.md section 11.3 "Concurrency and
  consistency" is the relevant section.
- Tests: MCP tool tests in `Pho.Test.Integration` (in-process MCP client against
  `WebApplicationFactory<Program>`), scenarios in `Pho.Test.Acceptance` (feature files).

## Dependencies
- None. The single spec (`SPEC.md`), the test-project layout and the executable feature
  files in `Pho.Test.Acceptance` already exist.

## Decisions
- **Admin port**, no separate port.
- **Always on** — no setting to disable the MCP endpoint.
- **Tools and MCP resources.** Tools for every action; resources for read-only views an
  agent may want to load as context, e.g. the stub tree, a single stub, a received request,
  the history list, and the export document. Resource templates with ids
  (e.g. `pho://stubs/{id}`, `pho://received-requests/{id}`). Consider resource-change
  notifications (`notifications/resources/list_changed` / `updated`) when configuration
  changes, if the SDK makes it cheap.
- **History records agent-made changes, including "on behalf of".**

### Attribution in history
- Every configuration revision records its **origin**: the admin UI, or MCP.
- For MCP changes, record the **agent identity** automatically from the MCP `initialize`
  handshake's `clientInfo` (`name`, `version` — e.g. `claude-code 2.x`). No agent effort
  needed; available per MCP session.
- **On behalf of:** MCP has no standard field for the human the agent works for, so Pho
  asks for it: every mutating tool takes an optional `onBehalfOf` string, and its tool
  description asks the agent to fill it with the name of the person or team it is
  working for. Recorded verbatim.
- It is **self-reported and unverified** (the endpoint is no-auth) and must be shown as
  such in the UI, e.g. *"via claude-code 2.1 · on behalf of 'Tom' (as stated by the
  agent)"*.
- It is **optional**: agents may legitimately decline to share a user's name with an
  external service (Claude's own privacy rules do this by default unless the user says
  otherwise), so a missing value is normal. Users who want it filled in can tell their
  agent so (e.g. in `AGENTS.md`/`CLAUDE.md` of the repo under test).
- Optional convenience: a session-level `set_on_behalf_of` tool so the agent states it
  once per session rather than on every call; per-call value overrides it.
- UI changes: the history/undo list shows origin, agent and on-behalf-of for each
  revision. UI edits show "admin UI" (no user identity — the UI is anonymous in v1).
- Domain change: `ConfigRevision` gains fields for origin, agent name/version, and
  on-behalf-of (SPEC.md section 5.8 "Configuration history"; EF migration in
  `src/Pho.Infrastructure`; set via `ConfigHistoryService`). Export/import is unaffected
  (history isn't exported).

## Open questions
- Should received-request clearing (not a configuration change, so not in history) also be
  attributed somewhere, e.g. a log entry?

## Out of scope
- Authentication/authorization for MCP (explicitly no-auth).
- Any way to disable the endpoint.
- A REST authoring API.
