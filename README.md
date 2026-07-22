# Pho

Pho is a **mock web service** — a configurable stub server that imitates real web services during testing (comparable to WireMock, Mockoon, or MockServer). Define fake endpoints and canned responses in a web UI; Pho serves them so a system under test can run against a controllable stand-in, and it records received requests so calls can be verified.

This project is **spec-first**: the authoritative specification lives in [`docs/spec/`](docs/spec/SPEC.md). Read it before contributing.

## Status

The v1 feature set is implemented (.NET 10, ASP.NET Core + Blazor, EF Core/SQLite) and deployable via Docker Compose. See the spec in [`docs/spec/`](docs/spec/SPEC.md) for the full behavior.

## Tech stack

.NET 10 · ASP.NET Core · Blazor (Interactive Server) · EF Core + SQLite · xUnit + FluentAssertions.

## Deploying with Docker Compose

Pho deploys as a **single container** (SQLite is in-process — no separate database service).

### Prerequisites

- Docker Engine 24+ with the Compose plugin (verify with `docker compose version`)

### Install and run

```bash
git clone https://github.com/tadams1138/Pho.git
cd Pho
docker compose up -d      # builds and starts Pho
```

- Admin UI: `http://localhost:8080` — create and manage mocks.
- Mock-serving surface: `http://localhost:8081` — point the system under test here; it receives your configured responses (or 404 when nothing matches).

The SQLite database is stored on a named Docker volume and persists across restarts.

```bash
docker compose down       # stop, keep data
docker compose down -v    # stop and delete the data volume
```

### Backup and restore

Mock definitions can be **exported to a JSON file** from the web UI for backup, and **imported** to restore them into another instance. Received-request logs are not part of a backup. See [`docs/spec/04-features.md`](docs/spec/04-features.md).

## Local development

Requires the .NET 10 SDK.

```bash
dotnet test                          # run the full test suite
dotnet run --project src/Pho.Web     # run the app
```

- Admin UI: `http://localhost:8080` — create and manage mocks.
- Mock-serving surface: `http://localhost:8081` — point the system under test here.

The SQLite database (`pho.db`) is created on first run in the working directory.
