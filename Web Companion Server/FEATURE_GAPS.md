# Web Companion Feature Gaps

Purpose: the web companion dashboard should let users create and edit their own flashcard sets, then send those sets to the Unity VR app for rehearsal.

This file lists the remaining gaps after the current toolbar and local save-flow fixes.

## Highest-Priority Gaps

- Unity handoff is still missing. The dashboard can save sets locally, but there is no explicit "Send to VR" action, no Unity-facing sync endpoint, and no import flow implemented on the Unity side.
- The formatting contract between web and Unity is undefined. The editor stores HTML from `contenteditable`, while the Unity app currently expects plain string values and TextMeshPro-friendly content. Lists, links, colors, and inline HTML styles need a translation layer before they can render reliably in VR.
- The font-size controls are editor-level only. The `+`, `-`, and numeric font-size controls change the browser editor presentation, but that size is not serialized into each saved card.
- There is no validation around what formatting is allowed. Users can save rich HTML, but the project does not yet define which markup is supported end to end.

## Authoring Workflow Gaps

- No autosave or unsaved-change indicator.
- No duplicate-card or duplicate-set action.
- No drag-and-drop or manual reordering for cards.
- No drag-and-drop or manual reordering for sets.
- No card search, set search, tags, or filtering.
- No import/export workflow for JSON, CSV, or text outlines.
- No preview that shows how a card will look once rendered in Unity.
- No explicit way to remove or edit an existing link besides editing the raw rich text directly.
- No keyboard shortcut layer for common editor actions.

## Dashboard UX Gaps

- Success and error handling still relies on browser `alert()` and `confirm()` dialogs instead of inline notifications.
- There is no empty-state guidance for first-time users.
- There are no loading states while requests are in flight.
- There is no per-card metadata such as speaker notes, timing, tags, or rehearsal cues.
- There is no accessibility pass for keyboard-only editing, focus states, screen readers, or color contrast in the editing tools.

## Server and Data Gaps

- Storage is still a single local JSON file (`data/db.json`), so there is no multi-user support, backup strategy, version history, or conflict handling.
- There is no request validation for limits such as maximum set size, maximum card count, or payload size beyond the basic body-parser cap.
- Saved HTML is not sanitized before storage or reuse, which is risky if this app ever stops being strictly local.
- There is no API contract versioning for future Unity integration.
- There is no health check or diagnostics endpoint for the companion server.

## Engineering Gaps

- The editor is still built on `document.execCommand`, which works for this prototype but is deprecated and brittle across browsers.
- There are no automated tests for the API.
- There are no automated tests for the browser editor behavior.
- There is no end-to-end test that verifies a set authored in the dashboard can be consumed by the Unity app.

## Recommended Next Steps

1. Define a strict flashcard content schema that Unity can render, including which formatting features are supported.
2. Implement a real Unity sync path, either by having Unity fetch `/api/sets` directly or by adding an explicit export/send endpoint designed for the VR app.
3. Persist formatting in a Unity-safe way, especially font sizing and any rich-text features that need to survive round-tripping.
4. Add unsaved-change state, inline notifications, and a first-time-user flow so the dashboard behaves like a real authoring tool instead of a prototype.
5. Add basic automated coverage for create, update, delete, and formatting-related persistence.
