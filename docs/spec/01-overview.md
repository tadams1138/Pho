# 1. Overview

## What Pho is

Pho is a **mock web service**: a standing server that imitates real web services so software can be tested against a controllable stand-in instead of a live dependency. A test author configures fake endpoints ("stubs") — each a rule that matches certain incoming requests and returns a canned response. The system under test sends its normal HTTP requests to Pho and receives those configured responses. Pho also records every request it receives so that callers can verify the right calls were made.

It is comparable to WireMock, Mockoon, and MockServer, with a browser-based authoring experience.

## Problem it solves

Testing code that depends on external web services is slow, flaky, and hard to control: the real service may be unavailable, rate-limited, non-deterministic, or unable to produce the error conditions a test needs. Pho replaces that dependency with responses the tester fully controls, and lets them toggle between different behaviors without editing test code.

## Primary users

Developers and QA engineers setting up controllable dependencies for automated or manual testing. See [`02-users-and-roles.md`](02-users-and-roles.md).

## v1 scope

The first version delivers:

- **Stub management via a web UI** — create, view, edit, delete, and **duplicate** stubs. (Web UI is the only configuration surface in v1.)
- **Nested grouping** — organize stubs into a tree of user-defined groups (e.g. "Vendor1 Mocks"), which may be nested to any depth, shown as a tree view.
- **Request matching** — match incoming requests by method, path, query, headers, and body.
- **Response definition** — return a configured status code, headers, and body.
- **Enable / disable stubs** — toggle a stub on or off without deleting or rewriting it, so a user can switch between alternative responses for the same endpoint quickly.
- **Request verification (spying)** — record received requests and view them **in full** (all headers, complete body, time received) in the UI to confirm what was called. Human-observed only in v1.
- **Configuration history** — the whole mock configuration is versioned on every change; a single mock's history is a derived view, with revert.
- **Undo / redo** — global, ordered undo/redo of every configuration change, including cascade group deletion, by stepping through configuration history.
- **Body helpers** — JSON/XML format and validate buttons on request/response body fields; advisory spell-check on free-text fields.
- **Basic auth helper** — build an `Authorization: Basic …` header rule from a user id and password, and read an encoded one back, without encoding base64 by hand.
- **Light / dark theme** — follows system preference, with a manual override.
- **Export / import mock definitions** — export all stubs and the group tree to a JSON file for backup, and import to restore.
- **Persistent storage** — stub definitions, groups, and history survive restarts.
- **Configurable retention** — request logs (default 1 day) and configuration history (default 1 year).
- **Single shared instance** — one global set of stubs; no per-session isolation.
- **Docker Compose deployment** — the whole system runs via `docker compose up`.

## Non-goals for v1

Explicitly out of scope (candidates for later versions):

- A programmatic **admin/config API** for tests to create/reset stubs or assert on received requests at runtime. Configuration and verification are UI-only by design (`06-interfaces.md`).
- **Config-as-code** loading of stub files at startup (distinct from manual export/import).
- **OpenAPI/Swagger import** to generate stubs.
- **Stateful scenarios** (different response on the Nth call, state machines).
- **Latency and fault injection** (artificial delays, forced connection errors).
- **Per-session / namespaced isolation** for parallel test suites.
- **Authentication on the configuration UI** — v1 is anonymous; **Active Directory** authentication is a candidate for a later iteration (`02-users-and-roles.md`).
- **Response templating** driven by request data (echoing request values into the response) — may be reconsidered; treated as out of scope unless promoted.

## Open questions

The only load-bearing open decision is the **technology stack**, deliberately deferred until the spec is complete (see [`08-architecture.md`](08-architecture.md)). Verification is settled: **human-observed in the UI only, by design** — no programmatic query endpoint ([`06-interfaces.md`](06-interfaces.md)).
