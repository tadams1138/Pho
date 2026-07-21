# Pho

Pho is a **mock web service** — a configurable stub server that imitates real web services during testing (comparable to WireMock, Mockoon, or MockServer). Define fake endpoints and canned responses in a web UI; Pho serves them so a system under test can run against a controllable stand-in, and it records received requests so calls can be verified.

This project is **spec-first**: the authoritative specification lives in [`docs/spec/`](docs/spec/SPEC.md). Read it before contributing.

## Status

Greenfield — the specification is being written and implementation has not started. The instructions below describe the **intended** deployment. Steps marked _TODO_ await the stack decision (see [`docs/spec/08-architecture.md`](docs/spec/08-architecture.md)); `docker compose up` is not yet available.

## Deploying with Docker Compose

Pho is deployed via Docker Compose: the application plus a persistent database, so mock definitions survive restarts.

### Prerequisites

- Docker Engine 24+ with the Compose plugin (verify with `docker compose version`)

### Install and run

```bash
git clone https://github.com/tadams1138/Pho.git
cd Pho
docker compose up -d      # builds and starts Pho and its database
```

Open the web UI at `http://localhost:8080` _(port TBD)_ to create mocks. The system under test then sends its requests to the same host/port and receives your configured responses.

Mock definitions are stored in a named Docker volume and persist across restarts.

```bash
docker compose down       # stop, keep data
docker compose down -v    # stop and delete the data volume
```

### Backup and restore

Mock definitions can be **exported to a JSON file** from the web UI for backup, and **imported** to restore them into another instance. Received-request logs are not part of a backup. See [`docs/spec/04-features.md`](docs/spec/04-features.md).

## Local development

_TODO: local dev commands (run the app, run tests, lint/format, run a single test) once the stack is chosen — see [`docs/spec/08-architecture.md`](docs/spec/08-architecture.md)._
