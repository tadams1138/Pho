# AGENTS.md

Entry point for every AI agent working in this repository (Claude Code, Codex, Cursor, ...).
`CLAUDE.md` only points here.

## Project

Pho is a mock web service: a configurable stub server used to imitate real web services during
testing (comparable to WireMock, Mockoon, or MockServer). Test authors define fake endpoints and
canned responses through a web UI; Pho serves them so a system under test can run against a
controllable stand-in. It also records received requests so calls can be verified.

## Spec-first, in three layers

The specification leads and code follows. Update the specification in the same change as the
behavior. It is split into three layers; put each fact in the right one.

| Layer | Holds | Must not hold |
|---|---|---|
| [`SPEC.md`](SPEC.md) | What the product does, in product language, stack-free. Requirement IDs (F1.x, R-x, M-x, ...) are stable and cited from code comments. | Hosting, ports, endpoints, frameworks, storage technology, code or JSON samples, Gherkin, process rules. |
| Gherkin feature files in [`tests/Pho.Test.Acceptance/Features/`](tests/Pho.Test.Acceptance/Features) | Decisions and behavior verified at a more granular level, still without saying *how* (e.g. "the service reports when it is healthy", "the default ports are ..."). Executable. | Mechanism (URLs, class names, markup). |
| Code, including the step definitions behind the Gherkin | The exact mechanism (e.g. the `/health` endpoint the compose healthcheck uses). | - |

- Things that merely exist to support a decision (health endpoint, default ports, file names,
  schemas) live in Gherkin and/or code and in this file, never in `SPEC.md`.
- Every behavior in a scenario must trace to a `SPEC.md` requirement or to a decision recorded in
  this file.
- Coding standards and process rules live here, not in `SPEC.md`.
- **Regenerating this implementation takes `SPEC.md` + the feature files + this file together.**
  Keep all three complete enough for that.
- Undecided items are marked inline in `SPEC.md` with `> **Open question:**` callouts.

## Stack

.NET 10 (`net10.0`) · ASP.NET Core (hosts the mock-serving surface and in-process authoring
services) · Blazor Web App, Interactive Server render mode · EF Core + SQLite · Dockerfile + Docker
Compose · tests: xUnit + FluentAssertions **7.x (never 8.0.0+)**, Reqnroll (xUnit) for Gherkin,
bUnit for UI scenarios, `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`) for
hosted-app tests. A future authentication candidate is Active Directory in front of the admin UI
(not v1).

## Build / Test / Run

Solution file is `Pho.slnx` (the .NET 10 XML solution format); arg-less commands find it.

- Build: `dotnet build`
- Test all: `dotnet test`
- Single test: `dotnet test --filter "FullyQualifiedName~<substring>"`
- Run the app: `dotnet run --project src/Pho.Web`

## Structure

```
Pho.slnx
src/
  Pho.Domain/          entities + pure logic (matching, history, tree rules, drafts); no external deps
  Pho.Infrastructure/  EF Core DbContext (SQLite), repositories, porter, history store, retention pruning
  Pho.Web/             ASP.NET Core host: Blazor UI + mock-serving middleware + in-process app services
tests/
  Pho.Test.Unit/         pure in-memory logic tests (refs Pho.Domain)
  Pho.Test.Integration/  real SQLite/EF Core and WebApplicationFactory-hosted app
  Pho.Test.Acceptance/   Features/*.feature + step definitions (Reqnroll + xUnit; HTTP via
                         WebApplicationFactory, UI via bUnit)
Dockerfile  docker-compose.yml  install.sh  SPEC.md  TODO.md  TODO/
```

### Concept to code names

stub = `Stub`; matching rules = `RequestMatcher` (method `HttpMethodMatch`, path
`PathMatcher`/`PathMatchType`, query/header `ParamMatcher`, value rule `MatchRule`/`MatchRuleType`,
`IgnoreCase`); response definition = `ResponseDefinition` (+ `HeaderValue`); group = `Group`
(`ParentGroupId`); received request = `ReceivedRequest` (`MatchOutcome` MatchedOne/NoMatch/Ambiguous,
`MatchedStubIds`, `ResponseStatus`); configuration = `ConfigSnapshot`; configuration revision =
`ConfigRevision` (stored as `ConfigRevisionRecord`: Sequence, CreatedAt, Summary, StateJson; current
position in the single-row `HistoryState`); resolution = `MockResolver`/`MockServer`; editor draft =
`StubDraft`; tree = `GroupTree`, `TreeFlattener`, `TreeRow`, `TreeSelection`, `TreeDeletion`,
`TreeMoveRules`, `TreeExpansion`, `TreeStubSelection`; Basic helper = `BasicAuth`; body helpers =
`BodyFormatter`; import modes = `ImportMode.ReplaceAll/Merge`.

## Development process and coding standards

- **BDD/TDD, Red -> Green -> Refactor.** Acceptance criteria are Gherkin scenarios in
  `tests/Pho.Test.Acceptance/Features/`. Write the failing test from the criterion, then the
  minimum code to pass, then refactor. Given/When/Then appears only in feature files.
- **Test type drives the project, because it drives dependencies:**
  - pure, in-memory logic -> `Pho.Test.Unit` (xUnit, FluentAssertions, production projects only);
  - real infrastructure (SQLite/EF Core, the hosted app) -> `Pho.Test.Integration`
    (+ EF Core/SQLite, `Mvc.Testing`);
  - scenarios -> `Pho.Test.Acceptance` (+ Reqnroll, `Mvc.Testing`, bUnit). Steps drive the mock
    surface over HTTP and the UI through bUnit; no browser automation. Keep `.feature` text free
    of mechanism.
- Domain-first: start TDD in `Pho.Domain`. Add projects incrementally as their first tests are written.
- FluentAssertions stays on 7.x in every test project.
- Code comments cite `SPEC.md` by requirement ID or section; keep them correct when `SPEC.md` changes.
- If a scenario exposes a real gap between spec and code, report it rather than papering over it.

## Architecture and technical decisions

- **Two ports.** Admin UI on **8931** (Blazor UI, `/export`, `/health`, static assets); mock-serving
  surface on **8932**. Configurable via `Pho:AdminPort` / `Pho:MockPort` (env `Pho__AdminPort` /
  `Pho__MockPort`). Kestrel `ListenAnyIP` on both (skipped in the `Testing` environment, where
  TestServer has no sockets).
- **Routing.** `Program.cs` uses `app.MapWhen(policy.IsMockTraffic, mockApp =>
  mockApp.UseMiddleware<MockServingMiddleware>())`. `IMockTrafficPolicy`/`PortMockTrafficPolicy`
  decide by `Connection.LocalPort == mockPort`; the abstraction lets tests force either side
  (`AlwaysMockTrafficPolicy` double). Every request on the mock port is mock traffic, so stub paths
  never collide with UI routes; the mock branch ends before any admin middleware, so PathBase never
  affects matching.
- **No public authoring API in v1.** Blazor components call in-process services directly
  (`StubService`, `GroupService`, `TreeEditService`, `ConfigHistoryService`, `IReceivedRequestLog`,
  `IConfigPorter`). Only the mock-serving surface, the `/export` download and `/health` are HTTP.
  Each UI action calls the service(s) then `ConfigHistoryService.RecordAsync(summary)` once, so one
  action is one revision. Body format/validate, spell-check (browser `spellcheck="true"`) and theme
  are client-side concerns. Any future public API (cf. TODO #2) must stay distinct from this.
- **Stubs have no priority.** Multiple enabled stubs matching one request is an error (HTTP 500 +
  logged + flagged in the UI), not resolved by ranking.
- **History is whole-configuration** (`ConfigRevision`), not per-stub; this makes cascade-delete
  undo atomic. Logically a full snapshot per revision; any retained revision must restore exactly.
  Current implementation stores full JSON snapshots; sequence 0 is the implicit empty baseline
  (never stored); restore deletes and re-inserts all stubs and groups preserving ids.
- **Persistence.** EF Core + SQLite; schema created by `Database.EnsureCreated()` at start-up (not in
  `Testing`), no migrations. Stub `Request`/`Response` and received-request headers/matched ids are
  JSON columns via value converters. Matching runs in memory after loading all stubs per mock request
  (`EfStubStore.GetAll`).
- **Configuration keys.** `ConnectionStrings:Pho` (default `Data Source=pho.db`; container
  `Data Source=/data/pho.db`); `Pho:AdminPort`, `Pho:MockPort`; `Pho:ReceivedRequestRetentionHours`
  (default 24); `Pho:PathBase`. No key yet for configuration-history retention (not implemented).
- **Export format.** JSON download from admin `/export` (`application/json`, attachment
  `pho-mocks.json`): the serialized `ConfigSnapshot` { Stubs, Groups }, enums as strings. Import reads
  JSON (max 10 MB via InputFile); `JsonException` -> "The file is not valid JSON."; null Stubs/Groups
  -> "The file is not a valid Pho export."; missing `IgnoreCase` deserializes to false (backward
  compatibility).

### Reverse proxy mechanics

- Every link the UI emits is relative to `<base href>` (navigation `./`, `received`; export
  `export`; Blazor script `_framework/blazor.web.js`). An absolute app link such as `/received`
  would resolve to the proxy root: that is a defect.
- `<base href>` = `Request.PathBase` + trailing `/`, or `/` when empty (`App.razor` `BaseHref`).
- PathBase sources (admin only): `UseForwardedHeaders` with `XForwardedProto | XForwardedHost |
  XForwardedPrefix`, `KnownNetworks`/`KnownProxies` cleared (headers only affect link generation); or
  `Pho:PathBase`, normalized (trimmed, slashes stripped, `/` prepended; accepts `Pho`, `/Pho`,
  `/Pho/`), applied via `UsePathBase` plus a fallback middleware that sets PathBase when absent.
- Compose/env: `PHO_PATH_BASE` -> `Pho__PathBase`. Blazor Server needs WebSocket upgrade through the
  proxy. IIS URL Rewrite/ARR sends no `X-Forwarded-Prefix`, so use `PHO_PATH_BASE`. Operator
  instructions live in `README.md`.

### Docker Compose hosting

- Single compose service `app` (SQLite in-process, no DB container): `build: .`, `image: pho`, ports
  `"${PHO_ADMIN_PORT:-8931}:8931"` and `"${PHO_MOCK_PORT:-8932}:8932"` (container ports fixed), env
  `ConnectionStrings__Pho=Data Source=/data/pho.db`, `Pho__PathBase=${PHO_PATH_BASE:-}`, named volume
  `pho-data:/data`, `restart: unless-stopped`, healthcheck.
- Compose file format **3.7**, the highest legacy docker-compose **v1.24.1** supports, so it runs
  unchanged on Compose v1 and v2. Uses only `${VAR:-default}`, top-level named volumes and
  `healthcheck` with `start_period`.
- Data survives restarts and `docker compose down`; `down -v` deletes the volume.
- Dockerfile: SDK 10.0 build stage restores with only the three `src/*.csproj` copied (layer
  caching), then `COPY . .` and `dotnet publish src/Pho.Web/Pho.Web.csproj -c Release -o /app`.
  **Never use `--no-restore`**: after a csproj-only restore the static-web-assets pipeline emits an
  empty manifest, `_framework/blazor.web.js` is never published and the UI never boots (reproduces
  only in the Docker/Linux build). Runtime stage aspnet 10.0, `WORKDIR /app` (content root), installs
  `curl` for the healthcheck, `ASPNETCORE_URLS=""`, `Pho__AdminPort=8931`, `Pho__MockPort=8932`,
  `ConnectionStrings__Pho="Data Source=/data/pho.db"`, `EXPOSE 8931 8932`, `VOLUME /data`. The host
  serves assets via `app.UseStaticFiles()`. After build/asset changes rebuild
  (`docker-compose build`, `--no-cache` if in doubt); `up -d` alone reuses a stale image.
- `install.sh` (Linux): detects `docker compose` v2 or `docker-compose` v1; prompts for admin and
  mock **host** ports (Enter = 8931/8932; validates 1-65535; refuses equal ports) and an optional
  admin sub-path (`PHO_PATH_BASE`); fetches source if not in a checkout (git clone/update, or
  `curl`/`wget` + `tar` without git); writes `.env` (`PHO_ADMIN_PORT`, `PHO_MOCK_PORT`,
  `PHO_PATH_BASE`); runs `docker compose up -d --build`. Safe to re-run.

### Health check

`AddHealthChecks()` + `MapHealthChecks("/health")` on the admin port returns `200 Healthy`. The
compose healthcheck is `["CMD", "curl", "-fsS", "http://localhost:8931/health"]` (interval 30s,
timeout 3s, retries 3, start_period 10s). No broader metrics surface in v1. Covered by
`Health_endpoint_reports_healthy`.

### Visual design mechanism

- No component library; hand-written CSS in the root layout (`App.razor`).
- Colors are CSS custom properties `--bg`, `--fg`, `--border`, `--muted`, `--accent`, `--err`.
- Theme: `prefers-color-scheme` gives the default; an explicit choice sets `data-theme` on the root
  element (overrides it), persisted in `localStorage` key `pho-theme`; an inline script applies it
  before first render; "System" removes both (`phoSetTheme`).
- Unsaved-changes guard: in-app navigation via `NavigationManager.RegisterLocationChangingHandler`;
  reload/close via a `beforeunload` handler toggled by `phoSetUnsaved`.
- Tree keyboard: `phoTree.capture` stops page scroll for arrows/Home/End/space; `phoTree.reveal`
  scrolls the active row into view.

## TODO

Work still to be done is listed in [`TODO.md`](TODO.md), with each item's details in
`TODO/<number>-<title>.md`. New feature ideas get the next number (highest + 1) and a
detail file. When an item is fully implemented, describe the behavior in the spec,
remove it from `TODO.md`, delete its detail file, and ask whether to renumber.
