# 3. Domain Model

The domain's core concepts are the **Stub** (with the **RequestMatcher** / **ResponseDefinition** it owns), organized into a tree of **Group**s. Two histories sit alongside them: **ReceivedRequest** logs incoming traffic (for verification), and **ConfigRevision** versions the entire mock configuration (for undo/redo and per-mock history).

## Group

A group is an organizational folder for stubs. Groups form a tree: a group may contain stubs and/or child groups, nested to any depth (e.g. "Vendor1 Mocks" → "Auth" → individual stubs). Groups have no effect on request matching — they exist purely to organize the UI.

| Field | Type | Notes |
|-------|------|-------|
| `id` | identifier | Stable unique id. |
| `name` | string | Display label, e.g. "Vendor1 Mocks". Required. |
| `parentGroupId` | identifier? | The containing group, or null for a top-level (root) group. Must not create a cycle. |

A stub references its group via `groupId` (below). Stubs with a null `groupId` are ungrouped and shown at the tree root.

**Deletion is cascading:** deleting a group deletes everything nested under it — all descendant groups and all stubs contained in the group and its descendants. Because this is destructive and multi-entity, it is captured as a single undoable revision (see [Configuration history and undo/redo](#configuration-history-and-undoredo)).

## Stub

A stub is one mocking rule: "when a request matches *this*, respond with *that*." It is the central entity.

| Field | Type | Notes |
|-------|------|-------|
| `id` | identifier | Stable unique id (e.g. GUID). |
| `name` | string | Human label for the UI. Required. |
| `description` | string? | Optional longer note. |
| `groupId` | identifier? | The Group this stub belongs to, or null if ungrouped (shown at the tree root). |
| `enabled` | boolean | Default `true`. When `false`, the stub is ignored during matching but retained. Toggling `enabled` is how a user switches between alternative responses for the same endpoint without deleting or rewriting either. |
| `request` | RequestMatcher | Owned; see below. |
| `response` | ResponseDefinition | Owned; see below. |
| `createdAt` / `updatedAt` | timestamp | Set by the system. |

## RequestMatcher

Describes which incoming requests a stub applies to. A request matches the stub only if **all** specified criteria match; unspecified criteria are ignored (do not constrain).

| Field | Type | Notes |
|-------|------|-------|
| `method` | enum | `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, `OPTIONS`, or `ANY`. |
| `path` | PathMatcher | How the request path is matched (see below). Required. |
| `queryParams` | list of ParamMatcher | Each names a query parameter and a rule. |
| `headers` | list of ParamMatcher | Each names a header and a rule. |
| `body` | MatchRule? | Optional rule applied to the raw request body. |

**PathMatcher** — `{ type: EXACT | WILDCARD | REGEX, value: string }`
- `EXACT` — full string equality, e.g. `/users/123`.
- `WILDCARD` — path template with `*` / `{param}` segments, e.g. `/users/*` or `/users/{id}`.
- `REGEX` — regular expression over the full path.

**ParamMatcher** — `{ name: string, rule: MatchRule }`

**MatchRule** — `{ type: EQUALS | CONTAINS | REGEX | PRESENT | ABSENT, value?: string, ignoreCase?: boolean }`
- `EQUALS`, `CONTAINS`, `REGEX` compare against `value`.
- `PRESENT` / `ABSENT` assert the parameter/header exists or does not; `value` is ignored.
- `ignoreCase` — **defaults to false**; comparison against `value` is case-sensitive unless it is set.
  - Applies to `EQUALS` and `CONTAINS` only. It is **ignored for `REGEX`** — a regular expression states its own case-insensitivity inline with `(?i)`, and two competing ways to say it would be a defect. It is meaningless for `PRESENT` / `ABSENT`, which compare nothing.
  - An export that predates the field, or omits it, imports as `false` — existing configurations keep the behavior they had.

### Case sensitivity — names versus values

The two are separate decisions and are answered differently.

- **Names are always compared case-insensitively**, and this is not configurable. HTTP field names are case-insensitive per RFC 9110, so `Content-Type` and `content-type` are the same header; a stub that distinguished them would be matching on something the protocol says carries no meaning. The same applies to the `name` on a header `ParamMatcher`.
- **Values are compared case-sensitively by default**, and leniency is opted into per rule via `ignoreCase`. Header values are not case-insensitive in general, and some carry payloads where case is load-bearing: an `Authorization: Basic <token68>` credential is base64, whose alphabet is case-significant, so folding case there would collapse distinct credentials onto one rule and let a stub accept a password it was written to reject. A mock looser than the service it stands in for turns a passing test into a production failure, so the default is fidelity and the exception is explicit.

> Note that an **authentication scheme** (`Basic`, `Bearer`) *is* a case-insensitive token per RFC 9110 §11.1, while the credential after it is not. Pho does not special-case the `Authorization` header to encode that split — a header whose matching rules silently differ from every other header costs more in surprise than it saves. The Basic auth helper (F12) instead writes a `REGEX` rule of the form `^(?i:Basic)\s+<credential>$`, whose scoped inline option folds the case of the scheme word alone and leaves the base64 exact. That a single value can need case-insensitivity in one half and case-sensitivity in the other is precisely why `ignoreCase` is a rule-level flag that `REGEX` ignores, rather than a value-level behavior. See `04-features.md`.

## ResponseDefinition

What Pho returns when the owning stub is selected.

| Field | Type | Notes |
|-------|------|-------|
| `status` | integer | HTTP status code, e.g. `200`, `404`, `500`. Required. |
| `headers` | list of `{ name, value }` | Response headers, e.g. `Content-Type`. |
| `body` | string | Raw response body. May be empty. |

_(v1 has no delay, fault, or request-driven templating fields — see non-goals in `01-overview.md`.)_

_(Stub definition history is not stored per-stub. The entire configuration is versioned as a single history — see [Configuration history and undo/redo](#configuration-history-and-undoredo) below — and a single mock's history is a derived view of it.)_

## ReceivedRequest

An immutable record of one request Pho received on its **mock-serving surface**. A received request is simply **any inbound request that is not one of Pho's own authoring/UI-supporting endpoints** — it does **not** need to match a defined stub to be recorded. Every such request is logged whether it matched one stub, none, or several. Powers verification.

The capture is **verbatim** — the full set of headers and the complete, unmodified body are stored exactly as received, so the received-requests view can show the request in full.

| Field | Type | Notes |
|-------|------|-------|
| `id` | identifier | Unique id. |
| `receivedAt` | timestamp | When the request arrived. |
| `method` | string | HTTP method. |
| `path` | string | Request path. |
| `query` | string / map | Raw and/or parsed query string. |
| `headers` | map | Request headers. |
| `body` | string | Raw request body. |
| `matchOutcome` | enum | `MATCHED_ONE`, `NO_MATCH`, or `AMBIGUOUS` (more than one stub matched). |
| `matchedStubIds` | identifier[] | The stub that served it (one id) for `MATCHED_ONE`; **all** matching stub ids for `AMBIGUOUS`; empty for `NO_MATCH`. |
| `responseStatus` | integer | Status code actually returned (matched response, `404` no-match, or `500` ambiguous). |

**Retention — decided:** received-request records are retained for **1 day by default, configurable** (see `07-non-functional.md`). Records older than the retention window are pruned automatically. The log is also clearable on demand from the UI (F5).

## Matching resolution

Stubs have **no priority or ordering**. A request is expected to match at most one enabled stub; overlapping matchers are a configuration error, surfaced rather than silently resolved.

When a request arrives at the mock-serving surface:

1. Consider only stubs with `enabled == true`.
2. Select those whose `RequestMatcher` matches the request (all specified criteria satisfied).
3. **Exactly one match** → serve that stub's response.
4. **No match** → return the **no-match default**: HTTP `404` with a body indicating no stub matched. _(Whether this default is configurable is deferred to a later version.)_
5. **More than one match → ambiguous-match error.** Pho does not pick a winner. It:
   - returns an error response to the caller — **HTTP `500`** with a body identifying the conflict and the stubs involved;
   - logs an error naming **which stubs matched** (their ids/names) and the request; and
   - surfaces the conflict in the UI where feasible (see F4 / `05-screens-and-flows.md`).
6. Record a `ReceivedRequest` for every request — capturing whether it matched one stub, none, or several (with the ids of all matched stubs on a conflict).

## Configuration history and undo/redo

Pho versions the **entire mock configuration** — all stubs and the full group tree — as a single linear history. Every change (create/edit/delete/toggle/move a stub, or create/rename/move/**cascade-delete** a group) produces a new **ConfigRevision** representing the resulting configuration. This history backs undo/redo (F6). There is no per-mock history — only the whole-configuration timeline. It is separate from `ReceivedRequest`, which logs *traffic*, not configuration changes; undo/redo never alter traffic logs.

Versioning at the whole-configuration level (rather than per single mock) is deliberate: multi-entity actions — most importantly **cascade group deletion** — are captured and reversed atomically. Undoing a cascade delete restores the entire removed subtree simply by returning to the prior revision, with no special-case bookkeeping.

### ConfigRevision

| Field | Type | Notes |
|-------|------|-------|
| `id` | identifier | Unique id. |
| `sequence` | integer | Monotonically increasing; defines history order. |
| `createdAt` | timestamp | When the change was applied. |
| `summary` | string | Human-readable description of the change, e.g. "Deleted group 'Vendor1 Mocks' (3 stubs)", shown in the history/undo UI. |
| `state` | configuration snapshot | The complete configuration after this change: every stub definition and the full group tree. |

> **Implementation note (for the stack decision):** `state` is specified *logically* as a full snapshot. The implementation may store full snapshots, diffs, or use structural sharing to bound storage — as long as the behavior here (restore any retained revision exactly) holds.

### Undo / redo semantics

- The configuration has a **current revision** (the tip). **Undo** makes the previous revision current; **redo** moves forward again.
- Because every change — including cascade deletes — is just a revision, **all** changes are undoable/redoable uniformly, with no per-operation special-casing.
- Making a **new** change while not at the tip creates a new revision after the current pointer and discards the forward (redo) revisions (standard undo/redo semantics).
- **Undo depth is bounded by retention:** configuration history is retained 1 year by default (configurable — see `07-non-functional.md`); undo cannot reach past pruned revisions.

Export is a current-state-only snapshot — configuration history is **not** included in the backup file.
