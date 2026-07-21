# 7. Non-Functional Requirements

## Deployment

- Pho **must be deployable via Docker Compose** (`docker compose up`), running the application plus its database together. This is the supported deployment path; see [`08-architecture.md`](08-architecture.md) for the topology and the README for user-facing steps.
- Persistent data lives in a named Docker volume so it survives container restarts and `docker compose down` (without `-v`).

## Persistence

- Stub definitions, groups, configuration history (ConfigRevisions), and received-request logs are stored in a **persistent store** (a database) that survives restarts.
- Backup of mock definitions is handled at the application level via export/import (F8), independent of database-level backups.

## Concurrency and consistency

- v1 is a **single shared instance** with one global set of stubs — no per-session isolation. Multiple test authors and multiple SUTs may interact with it simultaneously.
- Concurrent edits and concurrent mock traffic must not corrupt state; the mock-serving path should reflect the currently persisted, enabled stubs.

## Performance

- The mock-serving surface is on the hot path of others' test suites; response latency should be low and predictable under typical stub counts.
  > **Open question:** concrete latency/throughput targets are not yet set.

## Security

- The **mock-serving surface is unauthenticated** by design (the SUT calls it as the real service).
- The **authoring UI/API uses anonymous authorization** in v1 (no access control), assuming deployment on a trusted network. A later iteration may add Active Directory in front of the authoring surface (`02-users-and-roles.md`).

## Observability

- Application logging sufficient to diagnose match failures and errors.
  > **Open question:** metrics/health endpoints for the container (e.g. a `/health` check for Compose) — likely needed for the Compose healthcheck; confirm with the stack decision.

## Configuration settings

Operational limits are **configurable** (e.g. via environment variables / config, finalized with the stack), with these defaults:

| Setting | Default | Notes |
|---------|---------|-------|
| Received-request log retention | **1 day** | Records older than this are pruned automatically; log is also clearable from the UI. |
| Configuration history retention | **1 year** | Older ConfigRevisions pruned; also bounds undo/redo depth. |

## Retention

- Received-request logs and configuration history are bounded by the retention settings above rather than growing unbounded.
- Pruning is automatic (age-based). Undo/redo depth (F7) is limited to the configuration history still within the retention window.
