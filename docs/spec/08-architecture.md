# 8. Architecture

## Stack decision — CHOSEN

| Concern | Choice |
|---------|--------|
| Runtime | **.NET 10** (`net10.0`) |
| Backend | **ASP.NET Core** — hosts the mock-serving surface and the authoring backend |
| Frontend | **Blazor Web App**, Interactive Server render mode (single-language C#) |
| Data | **EF Core** with **SQLite** |
| Tests | **xUnit** + **FluentAssertions 7.x** (never 8.0.0+, per project rule) |
| Deploy | Dockerfile + Docker Compose |

Blazor Server means the "authoring API" (see `06-interfaces.md`) is realized as in-process application services the Blazor components call directly — there is no separate HTTP authoring API in v1, consistent with configuration being UI-only. Only the **mock-serving surface** is a public HTTP surface.

Requirements the stack must satisfy:

- Full-stack web app: a browser UI (the authoring experience, incl. a tree view) plus a backend that serves both the UI's API and the mock-serving surface.
- A persistent database.
- Packaged and run via **Docker Compose**.
- Testable under a BDD/TDD workflow (per the global rules).

## Two HTTP surfaces — separate ports (decided)

A core constraint (see `06-interfaces.md`): the **mock-serving surface** must be separable from the **admin UI**, so a stub for an arbitrary path cannot collide with the app's own routes. **Decision: two ports.** The app listens on:

- **Admin port** (default `8931`) — the Blazor UI and its supporting endpoints.
- **Mock port** (default `8932`) — the mock-serving surface; the system under test points here. It treats *every* request on this port as mock traffic (matched against stubs, else 404), so it never collides with UI routes.

Both ports are configurable. Kestrel listens on both; middleware branches by port.

## Hosting behind a reverse proxy (decided)

The admin UI must work when a reverse proxy mounts it under a **sub-path** (e.g. `https://host/Pho/`) as well as at a host root. **Decision: nothing in the app hardcodes a prefix.**

- **Every link the UI emits is relative** — to the `<base href>` the page renders, never to `/`. That covers the navigation, the export download, and the Blazor script (`_framework/blazor.web.js`); a link written as `/received` would resolve to the proxy's root and 404, so absolute app links are a defect.
- **The base href is the request's `PathBase`**, plus a trailing slash, and `/` when there is none. Direct access is therefore unchanged.
- `PathBase` is established in one of two ways, both **admin-surface only** — the mock-serving branch is left untouched, so a prefix never alters what a stub matches:
  - the proxy announces the prefix it stripped via **`X-Forwarded-Prefix`** (with `X-Forwarded-Proto` / `X-Forwarded-Host` honoured alongside it); or
  - the prefix is configured as **`Pho:PathBase`**, for proxies that strip it silently.
- Forwarded headers are accepted from any proxy address, since Pho sits on a test network behind one and the headers only affect link generation.

## Docker Compose topology

With SQLite there is a **single service**:

- **`app`** — the Pho application (Blazor UI + mock-serving surface). Builds from a `Dockerfile`. Publishes the admin and mock ports. Persists the SQLite database file to a **named volume** so mock definitions survive restarts. A Compose `healthcheck` probes `GET /health` on the admin port (`curl -fsS http://localhost:8931/health`) for orchestration.

No separate database container is needed (SQLite is in-process). Concrete image, ports, env vars, and volume name are defined in `docker-compose.yml` during implementation.

**Static web assets / the Blazor script (`_framework/blazor.web.js`).** The admin UI is only interactive if this script is published and served. Two build details are load-bearing:

- The `Dockerfile` restores with only the `.csproj` files present (for layer caching), then publishes. That publish **must not** use `dotnet publish --no-restore`: publishing `--no-restore` against a csproj-only restore makes the static-web-assets pipeline emit an *empty* manifest with **no `wwwroot`**, so `_framework/blazor.web.js` is never published and the admin page 404s / never boots. Letting publish restore again (packages are already cached in the build layer) regenerates the assets correctly. This only reproduces in the Docker/Linux build — a local `dotnet publish` on the dev box always includes the assets, which makes the failure easy to miss.
- The host serves the assets from `wwwroot` via `app.UseStaticFiles()` (content root is `/app`, set by the Dockerfile `WORKDIR`). The Compose deploy must be rebuilt (`docker-compose build`, `--no-cache` if in doubt) after any change here — `docker-compose up -d` alone reuses a stale image.

## Project structure

```
Pho.slnx
src/
  Pho.Domain/          # entities + pure logic (matching, history); no external deps
  Pho.Infrastructure/  # EF Core DbContext, SQLite, repositories, retention/pruning
  Pho.Web/             # ASP.NET Core host: Blazor UI + mock-serving middleware + app services
tests/
  Pho.Domain.Tests/    # xUnit + FluentAssertions (unit)
  Pho.Web.Tests/       # integration tests (WebApplicationFactory) — added with the web slice
docs/spec/             # this specification
Dockerfile
docker-compose.yml
```

Projects are added incrementally as their first tests are written (BDD/TDD), not all up front.
