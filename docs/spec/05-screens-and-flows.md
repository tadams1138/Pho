# 5. Screens and Flows

The UI is a single-page web application with three primary areas: the **stub tree**, the **stub editor**, and the **received-requests view**. Import/export are actions available from the stub tree area.

## Global layout

- A persistent left pane shows the **stub tree** (groups and stubs).
- The main pane shows whatever is selected: a stub in the editor, or the received-requests view.
- A toolbar exposes global actions: **undo / redo** (F7), **export / import**, **clear log**, and a **theme** control.
- **Theme (F11):** the UI follows the system light/dark preference by default, with a manual light/dark override that persists.

## Stub tree (left pane)

- Displays groups as an expandable/collapsible **tree view**, nested to any depth, with stubs shown under their group and ungrouped stubs at the root.
- Each stub row shows its name, method + path summary, and its **enabled/disabled** state with a quick toggle.
- Actions: add group, add child group, rename/delete group, add stub, **duplicate stub** (creates a disabled copy in the same group), move a stub or group to another parent, delete stub.
- **Deleting a non-empty group** shows a confirmation warning that the delete is **cascading** (removes all nested groups and stubs) before proceeding; the action is undoable (F7).
- States: **empty** (no groups or stubs yet, with a prompt to create the first stub), **populated**, and **loading**.

## Stub editor (main pane)

- Fields for the request matcher: method, path (with matcher type EXACT / WILDCARD / REGEX), and repeatable rows for query, header, and body rules (each with a match-rule type).
- Fields for the response: status, repeatable header rows, and body.
- Controls for `name`, `description`, `group`, and the `enabled` toggle. (There is no priority/ordering control — overlapping stubs are surfaced as an error, not ranked.)
- **Body helpers (F9):** the request-body and response-body fields each provide JSON and XML **format** and **validate** buttons; validation results (including error locations) surface inline and are advisory — they never block saving.
- **Spell-check (F10):** free-text fields (name, description, body text) indicate possible typos; advisory only.
- **History (F6):** the editor gives access to the mock's history (derived from the configuration history) to view and revert to earlier definitions.
- **Validation** surfaces inline (e.g. missing path or status) and blocks save. (Distinct from the advisory body/spell helpers above.)
- Flow: select a stub → edit → save (persisted, tree refreshes) or cancel.

## Received-requests view (main pane)

- List of ReceivedRequests **always sorted descending by time received** (most recent at top): timestamp, method, path, matched stub (or "no match"), and response status.
- **Paginated** with a page-size selector of **10 / 20 / 50 / 100** per page and page navigation; sort order and filters apply across all pages.
- Filter by method and by **URL path** (partial/substring match), combinable — so a high-volume log with many requests in a short span can be narrowed to the paths of interest.
- Select a row to see the **full request exactly as received** — method, path, query string, all headers, and the complete body — plus the time received, match outcome, and response status served.
- **Ambiguous matches are flagged (F4):** a request that matched more than one enabled stub is marked as an error and shows which stubs it matched, so overlapping stubs can be found and fixed.
- Action: **clear log**.

## Export / import flow

- **Export**: toolbar action downloads a JSON file of all stubs and the group tree.
- **Import**: toolbar action selects a JSON file and choice of **replace-all** or **merge**; on success the tree refreshes, on validation failure an error is shown and nothing changes.

> **Open question:** visual design system / component library is not chosen; depends on the frontend stack (`08-architecture.md`). Whatever is chosen must support **light and dark themes** (F11). Screens above are specified by behavior, not visual design.
