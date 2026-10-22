# 0007: The console's design system: shadcn/ui on Radix, semantic tokens, vendored design skills

**Status:** accepted

## Context
- **The M4 console worked but read as generated.**
  - Every region was the same grey panel with a tracked-out uppercase label.
  - There was no type scale (67 arbitrary sizes such as `text-[11px]`), and colour was used as decoration.
  - Raw ids and cut-off reasons showed through.
  - There was no light theme.
- **Under the surface were real defects.**
  - Stats showed 0 while loading.
  - Errors and 404s rendered blank pages.
  - Revoking and rejecting were single clicks.
  - Accessibility was absent: no ARIA, failing contrast, and no way to sign out on a phone.
  - The SSE buffers were unbounded.
  - Sign-in exchanged the authorization code twice under React StrictMode.
- **The primitives were ours alone.** Five hand-rolled primitives (Badge, Button, Panel, Table, Field) had no dialog, tooltip, toast or menu. Every overlay would have meant writing keyboard and focus handling ourselves.

## Decision
- **Components: shadcn/ui (new-york) on Radix** (`radix-ui`, MIT).
  - The components are copied into `web/src/components/ui` and restyled through tokens, never forked per screen.
  - Radix provides focus management, keyboard support and ARIA for dialogs, menus, popovers, tabs, selects and tooltips. cmdk (MIT) provides the ⌘K palette and sonner (MIT) the toasts.
  - The shadcn registry host was unreachable from the build environment. The sources therefore come from the shadcn-ui/ui repository at a pinned commit, with the CLI's import rewrites applied. `components.json` lets the CLI manage them from now on.
- **Tokens: semantic CSS variables, light and dark** (`web/src/styles/index.css`, specified in `web/DESIGN.md`).
  - Every text/background pair is checked against WCAG AA in both themes.
  - Themes switch variables; components never branch on the theme.
  - The theme follows the OS setting, with a stored override.
- **A house rulebook plus vendored skills.**
  - `.claude/skills/console-ui` is the rulebook. It says which component to use for which job, the state matrix every data view covers, how destructive actions are confirmed, URL state, accessibility and copy rules.
  - It points to seven public skills, vendored with their licences and a `SOURCE.md` each: web-design-guidelines (Vercel, MIT), impeccable (Apache-2.0), interface-design (MIT), react-best-practices and composition-patterns (Vercel, MIT), shadcn (MIT), and accessibility (Addy Osmani, MIT).
  - They are vendored rather than installed: runtime fetches, launchers and telemetry are removed, and the rules are pinned with the code they govern.
  - Rejected:
    - Rauno Freiberg's interface guidelines (no licence).
    - ui-ux-pro-max (generic, script-driven).
    - Anthropic's frontend-design, which is aimed at distinctive brand pages, the opposite of an operations console. Its anti-pattern list is quoted in the house rules.
- **Two signature elements that carry information:**
  - a colour fingerprint derived from each manifest hash, so pinned and drifted manifests differ at a glance;
  - two seals for four-eyes approval.
- **Guarded by tests.** ESLint (typescript-eslint, react-hooks, jsx-a11y, TanStack Query) runs in CI. So does Playwright, which checks behaviour, runs axe (no serious or critical violations on any page, in either theme) and compares visual baselines per page and theme.

## Consequences
- **Upstream updates are deliberate.** The `components/ui` files are ours to update through `npx shadcn add --diff`. The vendored skills are updated by re-copying from upstream and editing `SOURCE.md`.
- **The visual baselines must be regenerated on purpose** (`npx playwright test visual --update-snapshots`) and reviewed as images in the PR. They rely on Playwright's pinned Chromium and the bundled fonts.
- **Accessibility test licence.** axe-core and `@axe-core/playwright` are MPL-2.0. They are dev-only and never shipped or modified, which MPL permits in an MIT repository.
- **Not solved here:** Hub event summaries embed reviewer subject ids ("Rejected by 7a1d…"). Readable names need the services to record a display name next to `ActorId` (a follow-up). The console maps the ids it can, such as "You" and the system actors.
