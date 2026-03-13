# Web Companion Feature Gaps

Purpose: the web companion dashboard should let users create and edit their own flashcard sets, then send those sets to the Unity VR app for rehearsal.

This file lists the major gaps that still remain after the current pass, which added:

- a real `Send To VR` action in the dashboard
- published-set endpoints for Unity consumption
- a first Unity import path from the companion server
- basic request validation and a health endpoint
- per-card saved font-size metadata
- unsaved-change state in the dashboard

## Highest-Priority Remaining Gaps

- The rich-text contract is only partially complete. The server now converts authored HTML into a Unity-friendly export format, but that translation is intentionally conservative and does not preserve every possible browser formatting edge case.
- Links are exported as display text only. There is no interactive link behavior in Unity.
- The Unity app currently imports published sets into the set list, but it does not yet auto-open the active published set or surface sync status in-scene.
- The companion server still stores authored content as browser HTML, which keeps the editor simple but means the authoring format and the VR export format are still different representations.

## Authoring Workflow Gaps

- No autosave or revision history.
- No duplicate-card or duplicate-set action.
- No drag-and-drop or manual reordering for cards.
- No drag-and-drop or manual reordering for sets.
- No card search, set search, tags, or filtering.
- No import/export workflow for JSON, CSV, or text outlines.
- No preview that shows the exact Unity-rendered version of a card before publishing.
- No explicit link editor for updating or removing existing links beyond normal rich-text editing.
- No keyboard shortcut layer for common editor actions.

## Dashboard UX Gaps

- Destructive confirmations still rely on browser `confirm()` dialogs.
- There is still no true loading state for long-running requests beyond inline status text.
- There is no per-card metadata such as speaker notes, timing, tags, or rehearsal cues.
- There is no accessibility pass for keyboard-only editing, focus management, screen readers, or color contrast tuning.

## Server and Data Gaps

- Storage is still a single local JSON file (`data/db.json`), so there is no multi-user support, backup strategy, version history, or conflict handling.
- HTML sanitization is basic and intentionally narrow. It removes obvious dangerous content and preserves only the formatting the app currently cares about, but it is not a full sanitizer for hostile input.
- There is no authentication or access control on the companion server.
- There is no server-side audit trail showing who changed or published a set.

## Unity Integration Gaps

- The Unity import URL is still configured manually in the Inspector, which is necessary for local development but not a polished device-pairing story.
- There is no in-VR refresh action, sync error UI, or retry flow beyond the current startup fetch and demo fallback.
- The active published set is not automatically loaded into the flashcard session.
- There is no end-to-end validation that authored formatting renders exactly as expected in TextMeshPro across all supported cards.

## Engineering Gaps

- The browser editor still relies on `document.execCommand`, which remains workable for this prototype but is deprecated and browser-fragile.
- There are no automated tests for the API.
- There are no automated tests for the browser editor behavior.
- There are no automated tests for the Unity import/export contract.

## Recommended Next Steps

1. Add a Unity-side active-set loader so publishing from the dashboard can update the in-session flashcard deck directly, not just the available set list.
2. Add a Unity preview/export preview inside the dashboard so authors can see how formatting will render before publishing.
3. Replace the current HTML-first storage model with a stricter intermediate card schema once the supported formatting surface is settled.
4. Add automated API coverage for create, update, publish, delete, and Unity export conversion.
5. Add import/export tools so sets can move in and out of the dashboard without manual re-entry.
