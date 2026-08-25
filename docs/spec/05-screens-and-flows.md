# 5. Screens and Flows

The UI is a single-page web application with three primary areas: the **stub tree**, the **stub editor**, and the **received-requests view**. Import/export sit in a backup toolbar at the top of the stub screen, above the tree and its authoring controls.

## Global layout

- A persistent left pane shows the **stub tree** (groups and stubs).
- The main pane shows whatever is selected: a stub in the editor, or the received-requests view.
- **Backup actions (export / import) sit at the top of the stub screen**, above the controls that add stubs and groups — deliberately *not* between those controls and the tree, where they interrupt the authoring flow.
- Below them a toolbar exposes the actions that operate on the tree: **new stub**, **add group** (including the parent group to add it inside), **delete selected**, **enable / disable selected**, and **undo / redo** (F6). **Clear log** belongs to the received-requests view; a **theme** control sits in the header.
- **Theme (F10):** the UI follows the system light/dark preference by default, with a manual light/dark override that persists.

## Stub tree (left pane)

- Displays groups as an expandable/collapsible **tree view**, nested to any depth, with stubs shown under their group and ungrouped stubs at the root.
- **The tree opens fully collapsed** — every group closed — so a large configuration starts as a short list of top-level groups. An import likewise leaves the tree collapsed.
- **Expand all / collapse all** act on the selected groups and everything nested under them, or on the whole tree when nothing is selected.
- **One row, one line.** A row never wraps: the tree scrolls **horizontally** for long paths and **vertically** within its own bounded height, so the stub editor beside it stays on screen however long the tree is.
- Each stub row shows **either** its name **or**, when it has none, the method and path it matches (e.g. `GET /users/1`) — never both. A disabled stub is struck through and tagged.
- Actions: add group (at the top level or inside a chosen group), add stub, **duplicate stub** (creates a disabled copy in the same group), move stubs and groups by dragging, and delete, enable, or disable the current selection.
- **Selection spans many rows.** Every row has a checkbox; a plain click selects just that row, ctrl/cmd-click adds or removes one, and shift-click selects a range. **Delete selected** removes everything selected in one undoable action, and **enable / disable** flip every stub the selection covers — selecting a group covers its contents — in a single undoable action. Rows carry **no per-row buttons** beyond the expand twisty: deleting and toggling are always toolbar actions over the selection.
- **Deleting a group** — alone or within a selection — first confirms how much goes, warning that the delete is **cascading** (all nested groups and stubs); the action is undoable (F6). Rows already covered by a selected group are not deleted twice.
- **Drag and drop rearranges the tree.** Dropping a row on a group moves it into that group (dropping on a stub means "into the group holding it"); a top-level drop area moves rows back to the root. Dropping a group into itself or its own descendant is refused. Dragging a row that is part of the selection moves the whole selection; a group carries its contents.
- **Keyboard navigation (F11):** ↑ / ↓ / Home / End move the active row, ← / → collapse and expand groups (stepping to parent or first child), space toggles the row's selection, and Delete raises the same confirmation as **Delete selected**. The active row scrolls into view and drives what the editor shows.
- States: **empty** (no groups or stubs yet, with a prompt to create the first stub), **populated**, and **loading**.

## Stub editor (main pane)

- The editor is a **panel beside the tree**, not a separate page: selecting exactly one stub — by click or from the keyboard — opens it; selecting a group, several rows, or nothing leaves the panel empty.
- Fields for the request matcher: method, path (with matcher type EXACT / WILDCARD / REGEX), and repeatable rows for query, header, and body rules (each with a match-rule type). Header rules are how a stub **matches on request headers**; header names compare case-insensitively.
- **Path and its match type share one line** — the type is a control to the right of the path box, not a field of its own.
- **The repeatable rule sections collapse.** Query parameter rules, header rules, and response headers are each a collapsible area, opened when the stub has rules of that kind and closed when it has none, so an ordinary stub is a short form rather than three empty tables. Each header states how many rules it holds.
- **Basic auth helper (F12):** beneath the header rules, a user id and password box build an `Authorization: Basic …` header rule — the pair is base64-encoded with a colon between them — replacing any Authorization rule already present. The reverse is shown too: hovering an `Authorization: Basic …` value reveals the decoded user id and password, so an encoded credential from an existing mock can be read without leaving the page.
- Fields for the response: status, repeatable header rows — the **headers the stub emits** — and body.
- Controls for `name`, `description`, and the `enabled` toggle. (There is no priority/ordering control — overlapping stubs are surfaced as an error, not ranked.) **There is no group control in the editor**: the tree already shows where a stub lives, and a stub is moved between groups by dragging it there.
- **The name defaults to the method and path.** A stub saved with the name box left blank is named after what it matches — `POST /sessions` — so a first save needs no typing; the box shows that default as its placeholder.
- **Save and unsaved changes:** edits are held in a draft until **saved**; the panel shows whether changes are unsaved and offers **revert**. Leaving a dirty draft — selecting another row, undo/redo, import, navigating to another view, or closing/reloading the page — warns first and offers **save**, **discard**, or **cancel**.
- **Body helpers (F8):** the request-body and response-body fields each provide JSON and XML **format** and **validate** buttons; validation results (including error locations) surface inline and are advisory — they never block saving.
- **Spell-check (F9):** free-text fields (name, description, body text) indicate possible typos; advisory only.
- There is no per-mock history or revert — only the whole-configuration undo/redo (F6); reverting a single stub's definition in isolation is not supported.
- **Validation** surfaces inline (e.g. missing path or status) and blocks save. (Distinct from the advisory body/spell helpers above.)
- Flow: select a stub → edit → save (persisted, tree refreshes) or revert; leaving without saving prompts.

## Received-requests view (main pane)

- List of ReceivedRequests **always sorted descending by time received** (most recent at top): timestamp, method, path, matched stub (or "no match"), and response status.
- **Paginated** with a page-size selector of **10 / 20 / 50 / 100** per page and page navigation; sort order and filters apply across all pages.
- Filter by method and by **URL path** (partial/substring match), combinable — so a high-volume log with many requests in a short span can be narrowed to the paths of interest.
- Select a row to see the **full request exactly as received** — method, path, query string, all headers, and the complete body — plus the time received, match outcome, and response status served.
- **Ambiguous matches are flagged (F4):** a request that matched more than one enabled stub is marked as an error and shows which stubs it matched, so overlapping stubs can be found and fixed.
- Action: **clear log**.

## Export / import flow

- Both live in the **backup toolbar at the top of the stub screen**, separated from the authoring controls below it.
- **Export**: downloads a JSON file of all stubs and the group tree.
- **Import**: selects a JSON file and a choice of **replace-all** or **merge**; on success the tree refreshes, on validation failure an error is shown and nothing changes. An import with unsaved editor changes prompts first.

## Visual design (decided)

Screens above are specified by **behavior, not visual design**; the notes here fix only what the behavior depends on.

- **No component library.** The UI is hand-written CSS in the root layout — the admin surface is small enough that a framework would cost more than it saves, and it keeps the app dependency-free beyond the stack in `08-architecture.md`.
- **Colors come from CSS custom properties** (`--bg`, `--fg`, `--border`, `--muted`, `--accent`, `--err`) declared once, so a theme is a redefinition of that set rather than per-component rules.
- **Light and dark themes (F10)** are realized as: `prefers-color-scheme` supplies the default, and an explicit choice sets a `data-theme` attribute on the root element that overrides it. The choice persists in browser local storage and is applied before first render so the page does not flash the wrong theme. Reverting to "follow system" clears both the stored value and the attribute.
