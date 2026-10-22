---
name: console-ui
description: House rules for the AgentGate React console (web/) - the design tokens and type scale in web/DESIGN.md, which shadcn/ui or domain component to use for which job, the state matrix every data view must cover (loading skeleton, empty, error, forbidden, not found), confirmation rules for destructive governance actions, URL state, accessibility and copy style. Use when adding or changing any screen, component, style or copy in web/src, and before reviewing UI changes. Points to the vendored design skills for depth.
---

# Console UI

The console is an **Operate** surface: reviewers and admins come to finish a task (decide a review, act on a grant, read what an agent did). Familiar, dense, calm, and correct beats expressive. The design spec, tokens and signature elements are in [web/DESIGN.md](../../../web/DESIGN.md); this file is the rulebook.

## Skills to load

| Doing | Load |
|---|---|
| Any visual change | `impeccable` (Operate mode: `reference/operate.md`, then `reference/craft-floor.md` before editing) |
| Tokens, density, depth, states | `interface-design` (`design-deslop.md` for a cleanup pass) |
| Adding or composing components | `shadcn` (radix base) and `composition-patterns` |
| React code, data fetching, re-renders | `react-best-practices` |
| Reviewing a UI diff | `web-design-guidelines` (report `file:line`), then `accessibility` |

Precedence: this file and `web/DESIGN.md` → the vendored skills. Overridden upstream rule: headings and buttons use **sentence case** ("Request grant", "Approve and pin"), not Title Case.

## Components: which one for which job

Primitives live in `web/src/components/ui/` (shadcn, radix base; restyle through tokens, never fork per screen). Domain components live in `web/src/components/`.

| Job | Use | Not |
|---|---|---|
| Server, review, grant status; risk; gateway decision | `StatusBadge`, `RiskBadge`, `DecisionBadge` | a hand-styled `<span>` or a new tone map |
| Manifest hash | `Hash` (fingerprint + short hash + copy) | raw `slice(0, 12)` in markup |
| A list of tool names | `ToolList` (`ToolPicker` in forms) | `join(", ")`, ad-hoc chips |
| Who acted | `ActorName` | raw `sub` UUIDs or `system:*` strings |
| A point in time | `RelativeTime` (absolute UTC in the tooltip) | `toLocaleString`, bare clock text |
| Page title, breadcrumb, badges, actions | `PageHeader` | hand-built `<h1>` rows |
| Rows of records | `DataTable` (whole row is a link, numeric columns right-aligned and `tabular-nums`) | bare `<table>` |
| Any query-backed region | `QueryState` | `error ? … : data.length === 0 ? … : …` chains |
| Approve / reject with four-eyes | `DecisionPanel` | a copy of the decision block |
| Irreversible action (revoke a grant, reject a review) | `ConfirmDialog` (states the consequence, requires a reason, destructive button) | one-click buttons, `window.confirm` |
| Reversible action (suspend, resume) | inline form + toast | a modal |
| Feedback after a mutation | `toast` (sonner) | inline text that never clears |
| Icons | `lucide-react`, 16px, `aria-hidden` unless icon-only | Unicode glyphs (✓ → −) or emoji |

## State matrix (every data view)

| State | Shows |
|---|---|
| Loading | `Skeleton` in the final layout's shape. Never a number: a stat is not "0" until its query has data. |
| Empty | What would appear here and how it gets here ("Grants appear after two reviewers approve a request."). |
| Error | What failed and what to do, with Retry. |
| 403 | "Your role (auditor) can view but not change this." Hide controls the role cannot use; never show a control that will 403. |
| 404 | The page header with a link back to the list. |

Every interactive element has default, hover, focus-visible, active, disabled (with the reason visible, not only in `title`) and loading states.

## Rules

- **Tokens only.** Colours, radii, shadows and type sizes come from `web/src/styles/index.css` (`bg-background`, `text-muted-foreground`, `text-sm`…). No hex, no `text-[11px]`, no `dark:` overrides: themes switch tokens, components don't branch.
- **Colour means state.** Red, amber, green appear only for status, risk and decisions; the accent (`primary`) only for primary actions, selection and focus. Colour is never the only signal: badges carry a label and icon.
- **Monospace is for identifiers** (hashes, tool names, client IDs, JSON), never for headings, labels or badges.
- **No eyebrows.** No tracked-out uppercase labels above headings or as section titles; use the type scale.
- **URL is state.** Filters, tabs and search live in route search params (`validateSearch`); a reload or shared link restores the view.
- **Destructive means confirmed.** Irreversible governance actions go through `ConfirmDialog`; the reason is required and recorded in the audit trail.
- **Truncate safely.** Truncated text gets `min-w-0` on its flex parent and the full value in a tooltip or an expandable row.
- **Accessibility.** Every control is reachable and labelled; icon-only buttons have `aria-label`; live regions announce governance events, not every call; contrast ≥ 4.5:1 for text in both themes; respect `prefers-reduced-motion`.
- **Copy.** Sentence case; the product's words (manifest, pinned, grant, quarantined, four-eyes); controls name their action ("Approve and pin", "Revoke grant"); errors say what happened and what to do; numerals for counts; `…` not `...`.

## Verify

```bash
cd web && npm run lint && npm run typecheck && npm test && npm run build && npm run screenshots
```

`npm run screenshots` runs the Playwright suite against fixtures: page renders, role-based visibility, axe (no serious or critical violations) and visual snapshots in light and dark. Update snapshots only for intended changes (`npx playwright test --update-snapshots`), and look at them first.
