# AgentGate console: design

The rules for using this are in [.claude/skills/console-ui/SKILL.md](../.claude/skills/console-ui/SKILL.md). This file is the spec: who it is for, the direction, the tokens and the signature elements.

## Product

- **Who:** security reviewers deciding whether an MCP server's tools and an agent's grants are safe; registry admins registering servers and requesting grants; auditors reading what happened. They arrive from a notification ("a manifest drifted", "an agent was suspended") and need the decision context in one screen.
- **The job:** decide (approve, reject, suspend, revoke) with the evidence in view, and trust that what the console shows is current.
- **Feel:** a calm instrument. Dense like a review tool, quiet until something needs a person, unambiguous when it does.
- **Mode:** Operate (`impeccable` `reference/operate.md`). Standard navigation and controls; the brand lives in precise details.

## Direction

- **Restrained palette.** Cool ink neutrals; one accent, *ink indigo*, for primary actions, selection and focus. Red, amber and green are reserved for state: risk, status, decisions.
- **One family.** IBM Plex Sans for everything; IBM Plex Mono only for identifiers (hashes, tool names, client IDs, JSON).
- **Borders-only depth.** Hairline borders and one surface step; no shadows except overlays (menus, dialogs, toasts). Same canvas under sidebar and content.
- **Density.** Compact: 13px tables, 14px body, 36px controls, 40px rows.

### Signature elements

Both carry information; neither is decoration.

1. **Manifest fingerprint.** Every manifest hash renders as a 6-cell colour strip derived from the hash bytes, next to its short hash (`Hash`). Pinned and drifted manifests are told apart at a glance in tables, the review header and the diff. Cells use a fixed lightness and low chroma (identity, not state), so they never compete with status colours.
2. **Two seals.** Four-eyes approval is drawn as two seal slots (`Seals`): empty, sealed by the first reviewer (initials and time), sealed by the second. The same element appears in the approvals queue, the review and grant decisions, and timelines, so "awaiting second approval" reads identically everywhere.

## Tokens

Defined in `web/src/styles/index.css` as CSS variables for `:root` (light) and `.dark`, mapped to Tailwind through `@theme inline` with shadcn names. Every text/background pair below was checked against WCAG 2.2 AA (≥ 4.5:1) in both themes.

| Role | Light | Dark | Use |
|---|---|---|---|
| `background` | `oklch(0.985 0.003 250)` | `oklch(0.165 0.012 265)` | page canvas, sidebar |
| `card` | `oklch(1 0 0)` | `oklch(0.195 0.014 265)` | panels, tables, dialogs |
| `muted` | `oklch(0.965 0.005 260)` | `oklch(0.235 0.016 265)` | hover rows, code, inputs' wells |
| `foreground` | `oklch(0.22 0.025 265)` | `oklch(0.955 0.006 265)` | primary text |
| `muted-foreground` | `oklch(0.47 0.025 265)` | `oklch(0.73 0.018 265)` | secondary text, metadata (≥ 6:1) |
| `border` / `input` | `oklch(0.905 0.008 265)` | `oklch(0.30 0.016 265)` | hairlines, control outlines |
| `primary` | `oklch(0.47 0.17 268)` | `oklch(0.72 0.13 268)` | primary action, selection, links |
| `ring` | = `primary` | = `primary` | focus-visible ring |
| `destructive` | `oklch(0.52 0.19 27)` | `oklch(0.74 0.15 25)` | destructive button |
| `danger` / `-subtle` | `oklch(0.52 0.19 27)` / `oklch(0.965 0.022 25)` | `oklch(0.74 0.15 25)` / `oklch(0.27 0.06 25)` | high risk, denied, quarantined, revoked |
| `warning` / `-subtle` | `oklch(0.50 0.11 65)` / `oklch(0.97 0.04 85)` | `oklch(0.80 0.13 80)` / `oklch(0.28 0.05 75)` | medium risk, awaiting, suspended |
| `success` / `-subtle` | `oklch(0.47 0.11 152)` / `oklch(0.965 0.03 155)` | `oklch(0.76 0.13 152)` / `oklch(0.26 0.045 155)` | approved, active, allowed |
| `info` / `-subtle` | `oklch(0.47 0.15 255)` / `oklch(0.965 0.02 255)` | `oklch(0.75 0.12 255)` / `oklch(0.27 0.05 255)` | informational events |

### Type scale (fixed rem, ratio about 1.15)

| Token | Size / line height | Use |
|---|---|---|
| `text-xs` | 12 / 16 | metadata, timestamps, badges |
| `text-sm` | 13 / 20 | tables, dense UI, secondary text |
| `text-base` | 14 / 22 | body, forms, descriptions |
| `text-lg` | 16 / 24 | panel titles |
| `text-xl` | 20 / 28 | page titles |
| `text-2xl` | 24 / 32 | sign-in, key figures |

Weights: 400 body, 500 labels and emphasis, 600 titles. Headings use `text-balance` and slight negative tracking from `text-xl` up. Numbers that change or align use `tabular-nums`.

### Space, radius, motion

- **Spacing:** 4px base; Tailwind's default scale (`gap-1` = 4px). Component padding 8–12px, panel padding 16px, section gaps 24px, page gutter 24px (16px under `sm`).
- **Radius:** `rounded-sm` 4px (badges, chips), `rounded-md` 6px (controls), `rounded-lg` 8px (panels, dialogs). Nested elements are concentric.
- **Motion:** 150ms `cubic-bezier(0.23, 1, 0.32, 1)` for hover, press and overlays; new feed rows fade a subtle highlight for 1.2s. Transform and opacity only; none under `prefers-reduced-motion` except opacity.

## Layout

- **Shell:** a 232px sidebar (Overview; Governance: Approvals; Inventory: Servers, Agents, Grants; Monitoring: Live activity), a user menu at its foot (roles, theme, sign out), ⌘K command palette. Under `md` the sidebar becomes a sheet behind a top-bar menu button.
- **Pages:** `PageHeader` (breadcrumb, title, status, one line of metadata, actions), then content in a 12-column grid; detail pages use a main column and a 360px rail (decision, timeline).
- **Tables** are the default for collections; cards only for grouped decisions (a grant request with its seals).
