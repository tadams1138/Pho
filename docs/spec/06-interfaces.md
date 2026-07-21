# 6. Interfaces

Pho exposes two distinct HTTP surfaces. Keeping them separate is a core design constraint.

## 6.1 Mock-serving surface (the runtime)

This is what the **system under test** calls. It must behave like the real service being mocked:

- Accepts **any** inbound request on any method and path that is not one of Pho's own authoring/UI endpoints — the request need not correspond to any defined stub.
- Resolves them against enabled stubs using the matching algorithm in [`03-domain-model.md`](03-domain-model.md#matching-resolution).
- Returns the matched stub's response, the no-match default (**HTTP 404**) when nothing matches, or the ambiguous-match error (**HTTP 500**) when more than one matches.
- Records a ReceivedRequest for every such call (matched or not). Requests to the authoring/UI endpoints are **not** recorded as received requests.
- **Is never authenticated** — the SUT treats Pho as the real dependency and sends whatever requests it normally would.

> **Open question:** how the mock-serving surface is addressed relative to the authoring UI/API — a distinct port, a path prefix, or a hostname convention — so that a stub for `/users` doesn't collide with the app's own routes. To be settled with the stack decision.

## 6.2 Authoring API (the app's own backend)

The web UI is a client of an internal backend API that persists and queries domain data. In v1 this API exists to serve the UI; it is **not** positioned as the public, test-facing configuration API (that is a v1 non-goal — see `01-overview.md`). It provides operations for:

- **Stubs** — list, get, create, update, delete, duplicate (copy into the same group, created disabled), toggle `enabled`.
- **Groups** — list (as a tree), create, rename, cascade-delete, move; assign a stub to a group.
- **Configuration history** — list the revisions in which a given stub changed (its derived history), inspect a revision, and revert a stub to an earlier version.
- **Undo / redo** — undo the last configuration change, redo, and report whether undo/redo are currently available.
- **Received requests** — list (descending by time received, filterable by method and URL-path substring, paginated with page size 10/20/50/100), get full detail, clear.
- **Export / import** — produce the backup JSON (stubs + group tree); import with replace-all or merge and validation.

Body JSON/XML formatting, spell-check hints, and theming (F9–F11) are **client-side UI concerns** and require no authoring-API operations.

The concrete shape (REST vs. other, paths, payloads) is defined once the stack is chosen; it must cover exactly the operations the features in `04-features.md` require.

## Verification — human-observed only (by design)

**Verification is intended to be human-observed in the UI only.** There is no programmatic query endpoint and none is planned; automated tests do not assert on received requests from code. Consequently, the authoring API has no intentionally public, test-facing contract — it exists solely to serve the UI.

Requirement: the received-requests view must expose the **full** request exactly as Pho received it — method, path, query string, **every header, and the complete body** — together with the **time the request was received**.
