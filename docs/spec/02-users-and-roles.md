# 2. Users and Roles

## Personas

- **Test author (developer / QA engineer)** — the primary and, in v1, essentially the only interactive user. They create and manage stubs in the web UI, and inspect received requests to verify behavior.
- **System under test (SUT)** — a non-human client: the application or test process whose HTTP requests Pho answers. The SUT does not authenticate or configure anything; it simply sends requests to the mock endpoints and receives responses.

## Roles and permissions (v1)

v1 runs as a **single shared instance** with no per-user separation. All test authors who can reach the UI have the same full capabilities: manage stubs, view received requests, and export/import definitions.

**Authentication — decided:** the configuration UI (and its authoring API) uses **anonymous authorization by default** — no login, no access control. Pho is expected to run on a trusted network (local machine or an internal CI network), matching how most mock servers are deployed.

The **mock-serving endpoints are always unauthenticated**, because the SUT calls them as if they were the real service. Authorization, when it exists, applies only to the authoring UI/API, never to the served mocks.

> **Future (not v1):** a later iteration may add **Active Directory** authentication to gate the configuration UI. The design should not preclude introducing an auth layer in front of the authoring surface later, but v1 ships anonymous.
