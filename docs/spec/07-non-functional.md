# 7. Non-Functional Requirements

## Deployment

- Pho **must be deployable via Docker Compose** (`docker compose up`). With SQLite the application is a **single service** (no separate database container); see [`08-architecture.md`](08-architecture.md) for the topology and the README for user-facing steps.
- The compose file targets **format `3.7`**, the highest supported by legacy `docker-compose` **v1.24.1**, so it runs unchanged on both Compose v1 (`docker-compose`) and v2 (`docker compose`) — no server upgrade required. It relies only on features within that support: `${VAR:-default}` substitution, top-level named volumes, and `healthcheck` with `start_period`.
- A Linux **install script** (`install.sh`) provides one-command setup: it prompts for the admin and mock **host** ports (defaulting to 8931 / 8932 when the user presses Enter), clones the repo if needed, writes a `.env`, and runs `docker compose up -d --build`. The README documents a copy-paste `bash <(curl …)` invocation.
- The application exposes two ports: the **admin UI** (default 8931) and the **mock-serving surface** (default 8932). The **host** port mappings are configurable via `PHO_ADMIN_PORT` / `PHO_MOCK_PORT` (read from `.env` by Compose); the container-internal ports remain 8931 / 8932.
- The SQLite database file lives on a named Docker volume so data survives container restarts and `docker compose down` (without `-v`).

## Persistence

- Stub definitions, groups, configuration history (ConfigRevisions), and received-request logs are stored in a **persistent store** (a database) that survives restarts.
- Backup of mock definitions is handled at the application level via export/import (F8), independent of database-level backups.

## Concurrency and consistency

- v1 is a **single shared instance** with one global set of stubs — no per-session isolation. Multiple test authors and multiple SUTs may interact with it simultaneously.
- Concurrent edits and concurrent mock traffic must not corrupt state; the mock-serving path should reflect the currently persisted, enabled stubs.

## Performance

- The mock-serving surface is on the hot path of others' test suites; response latency should be **low and predictable** under typical stub counts. **Decided:** v1 sets no concrete numeric latency/throughput target — "low and predictable" is the qualitative goal; specific SLOs may be added in a later iteration if a need arises.

## Security

- The **mock-serving surface is unauthenticated** by design (the SUT calls it as the real service).
- The **authoring UI/API uses anonymous authorization** in v1 (no access control), assuming deployment on a trusted network. A later iteration may add Active Directory in front of the authoring surface (`02-users-and-roles.md`).

## Observability

- Application logging sufficient to diagnose match failures and errors.
- **Health endpoint (decided):** the app exposes `GET /health` on the **admin port** (returns `200 Healthy`). The Docker Compose `app` service uses it as its `healthcheck` (curl against `http://localhost:8931/health`). No broader metrics surface is included in v1.

## Configuration settings

Operational limits are **configurable** (e.g. via environment variables / config, finalized with the stack), with these defaults:

| Setting | Default | Notes |
|---------|---------|-------|
| Received-request log retention | **1 day** | Records older than this are pruned automatically; log is also clearable from the UI. |
| Configuration history retention | **1 year** | Older ConfigRevisions pruned; also bounds undo/redo depth. |

## Retention

- Received-request logs and configuration history are bounded by the retention settings above rather than growing unbounded.
- Pruning is automatic (age-based). Undo/redo depth (F7) is limited to the configuration history still within the retention window.
