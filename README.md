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

- Docker Engine, plus either flavor of Docker Compose:
  - **Compose v2** — `docker compose` (verify with `docker compose version`), or
  - **Compose v1** — `docker-compose` **1.24.1 or newer** (verify with `docker-compose version`).

The compose file uses format `3.7`, which both support, so no server upgrade is required. The commands below use `docker compose` (v2); on older servers just substitute `docker-compose` (with a hyphen). The `install.sh` script auto-detects whichever you have.

### Quick install (one command)

On any Linux machine with Docker, run:

```bash
bash <(curl -fsSL https://raw.githubusercontent.com/tadams1138/Pho/main/install.sh)
```

The installer asks which **admin** and **mock** ports to use — press **[Enter]** to accept the defaults (8931 / 8932) — then downloads the source, builds the image, and starts Pho with Docker Compose. **git is not required**: if it isn't installed, the script fetches a source tarball with `curl`/`wget` + `tar` instead. It is **safe to run repeatedly** — re-running updates an existing checkout in place and redeploys.

### Manual install

With git (re-run safe — clones the first time, updates thereafter):

```bash
git clone --depth 1 https://github.com/tadams1138/Pho.git Pho 2>/dev/null || { git -C Pho fetch --depth 1 origin main && git -C Pho reset --hard FETCH_HEAD; }
cd Pho
./install.sh              # prompts for ports, then builds and starts
# — or, to use the default ports without prompts —
docker compose up -d      # builds and starts Pho on 8931 / 8932
```

Without git (download and extract the source — re-extracting is safe to repeat):

```bash
curl -fsSL https://github.com/tadams1138/Pho/archive/refs/heads/main.tar.gz | tar -xz
cd Pho-main
./install.sh
```

- Admin UI: `http://localhost:8931` — create and manage mocks.
- Mock-serving surface: `http://localhost:8932` — point the system under test here; it receives your configured responses (or 404 when nothing matches).

To run on different ports without the installer, set them in a `.env` file next to `docker-compose.yml`:

```bash
echo -e "PHO_ADMIN_PORT=18931\nPHO_MOCK_PORT=18932" > .env
docker compose up -d
```

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

- Admin UI: `http://localhost:8931` — create and manage mocks.
- Mock-serving surface: `http://localhost:8932` — point the system under test here.

The SQLite database (`pho.db`) is created on first run in the working directory.
