---
name: interface-design:design-review
description: Strict, multi-pass craft and visual-hierarchy review of a UI build, with severity scoring and an explicit approval bar. The design counterpart to a deep code-quality review.
---

# Design Review

An unusually strict review focused on visual craft and hierarchy. The bar is not "does this work" or "does the grid align" — it is **"would a design lead at Linear or Apple put their name on this?"** Most generated UI is *correct* (it renders, it aligns, colors don't clash) and *not crafted* (nothing was decided, the hierarchy is flat, it looks like every other app). This review pulls a build from correct toward crafted, scores what's wrong by how much it matters, and blocks what isn't there yet.

Be ambitious. Don't stop at "this border is a little strong." Look for the decisions that were never made — the defaulted typeface, the absent focal point, the monotone layout — and push for the version that looks designed.

This review **judges by default**. Report findings and a verdict; only rebuild when the user asks. Keep the review and the mutation cleanly separate.

## The Gap: Correct vs Crafted

There's a distance between correct and crafted. Correct means the layout holds and nothing clashes. Crafted means someone cared about every decision down to the last pixel — the way you tell a hand-thrown mug from an injection-molded one. Both hold coffee; one has presence. Generated output lives in *correct*. Your job is to find every place it defaulted instead of decided, weigh how much each one costs, and name the crafted version.

---

# How to run it

Five steps, in order. Don't skip to the dimension checklist — the procedure is what keeps the review honest and stops it from becoming a list of your personal taste.

## Step 1 — Scope and intent

Establish two things:

- **What's under review** — the current build, a named component, or the branch's UI changes. Bound it.
- **What it's trying to be** — read `.interface-design/system.md` if it exists, and infer the intended user, task, and feel. *You review against the design's own intent, not a generic ideal.* A deliberately dense trading terminal is not failing for being dense. If intent is genuinely unknowable, say so and review against general craft — but note the assumption.

## Step 2 — See the whole first

Before picking anything apart, look at the rendered output as a user would. If a render tool (`show_widget`/`visualize`) or a screenshot is available, use it; otherwise read the layout holistically before the lines. Step back and ask the big questions:

- Does one thing lead, or is it a parking lot where everything competes equally?
- Does it breathe, or is it a monotone grid of identical boxes?
- Does it look like *this specific product*, or like every other dashboard?
- Squint (blur your eyes): does hierarchy survive? Does anything jump out harshly?

The worst slop is compositional — absent decisions that live in the whole, invisible in any single line of CSS. This step catches them. Everything after is detail.

## Step 3 — Run the lenses

Review through each lens **independently** — one concern at a time, so findings don't blur together. For each, the test is "did they decide, or default?"

**Lens A · Hierarchy** — the highest-value lens.
- Name the focal element. Does it actually win (size, weight, contrast, isolation), or sit equal to its neighbors?
- Is the rest *demoted* — secondary actions ghosted, metadata muted? Promotion without demotion isn't hierarchy.
- Can you tell primary / secondary / metadata tiers apart without reading the words?

**Lens B · Typography & color**
- Hierarchy from size + weight + color together, or size alone (the weak default)?
- A real type scale (a ratio), or arbitrary near-equal sizes? Tightened tracking on large headings, or default (reads as a document)?
- A four-step text-color ramp, or just "text" and "gray text"? Updating numbers in `tabular-nums`?
- One intentional accent (~60/30/10, accent ≤ ~10%), or several competing? Does color *mean* something (status/action/identity) or decorate?
- One hue across surfaces shifting only lightness, or different hues fragmenting the space? Does the palette feel like it came from the product's world?

**Lens C · Surfaces & depth**
- Elevation steps whisper-quiet (a few percent), or dramatic jumps? Borders low-opacity and findable-not-loud, or harsh 1px solid gray?
- One depth strategy committed, or mixed (borders + heavy shadows ad hoc)? Sidebar same-canvas + border, or a different-colored "sidebar world"?
- Nested radii concentric (`outer = inner + padding`)? Squint with borders mentally removed — is structure still perceptible through surface alone?

**Lens D · Composition & rhythm**
- Does the layout breathe unevenly (dense zones giving way to open), or is it monotone — same card size, gap, density everywhere?
- Do proportions state a relationship (280px vs 360px sidebar say different things)? Can you articulate what they say? Is content width considered, or full-bleed and unconsidered?

**Lens E · States, polish & motion**
- Every interactive element: default, hover, active, focus, disabled? Data: loading, empty, error? Missing states are the fastest tell of unfinished work.
- Hit areas 44×44px (WCAG, 40 min)? Optical alignment on icons/buttons? Font smoothing on root? `text-wrap: balance` on headings?
- Anything animating that shouldn't (keyboard-repeated actions)? UI durations < 300ms, custom ease-out (never `ease-in`)? Press feedback (`scale(0.97)`)? Origin-aware popovers? Only `transform`/`opacity` animated, never `transition: all`?

**Lens F · Structure, reuse & content**
- **Reinvention.** Did they hand-roll what the project (or the platform) already provides? A `<div onClick>` instead of `<button>`/`<a>`; a from-scratch dropdown/select/modal/tooltip instead of a headless accessible primitive or the project's existing component; a one-off className string instead of the shipped `Button`/variant; the same long utility string duplicated instead of a component; hardcoded literals (`bg-white`, `border-gray-200`) instead of semantic tokens. A hand-rolled interactive control missing keyboard nav, focus management, or ARIA is the worst case — it's broken, not just inconsistent.
- **Structural hacks.** Open the CSS and find the lies: negative margins undoing parent padding, escape-hatch `calc()`, absolute positioning to dodge layout flow. Each is a shortcut where a clean solution exists — the correct answer is simpler than the hack.
- **Content.** Read every visible string as a user would — not for typos, for truth. Does the screen tell one coherent story, or do the title, body, and metrics belong to three different products? Content incoherence breaks the illusion faster than any visual flaw.

## Step 4 — Score and filter

For every candidate finding, assign a **severity**, then drop the false positives. This is what separates a review from a wishlist.

Severity:
- **Blocker** — the design reads as generic or broken because of this. No focal point, flat hierarchy, monotone layout, timid/competing palette, missing states, structural hacks. These fail the approval bar.
- **Should-fix** — real craft gap that a design lead would call out, but the screen still functions. Slack heading tracking, a slightly harsh border, one inconsistent radius.
- **Note** — minor; mention once, don't dwell.

Then **filter out false positives** — do *not* raise these:

- **Taste, not defect.** "I'd have used a different font / accent / layout." If the choice is coherent with the intent and executed well, it is not a finding, even if you'd have chosen differently. Distinctive ≠ wrong.
- **A bold choice working as intended.** A saturated palette or dramatic scale that matches a stated bold intent is a success, not a flag.
- **Outside the scope** you set in Step 1, or on lines the branch didn't touch (for a diff review).
- **Ratified by `system.md`.** If the project's design system already decided it, it's a decision, not a defect.
- **A lint/format/compile concern** — owned elsewhere, not a design finding.

If you can't say *why a finding costs the user or makes the UI read as generated*, it's taste — cut it. Prefer a few high-conviction findings over a long cosmetic list. A short review that names the three things actually holding the design back beats forty nitpicks.

## Step 5 — Report and verdict

Synthesize across lenses. Prioritize:

1. Hierarchy & focal-point failures
2. Typography & color defaults that flatten the design
3. Surface/depth and composition-rhythm issues
4. Missing states, polish, and motion problems
5. Structural hacks and content incoherence

For each finding give three things: **what defaulted**, **why it reads as generic/flat or costs the user**, and the **specific crafted fix** (the decision, not a patch). Tag each with its severity. When a render tool is available, end by showing the improved version beside the current one so the gap is *shown*, not narrated — reasoning stays in your text; the widget shows visuals only.

Then state the verdict against the bar below.

---

# Approval Bar

Do not approve because it renders and aligns. To pass:

- a clear focal point and readable hierarchy (survives the squint test)
- typography with real size + weight + color hierarchy, not defaulted fonts or size-only tiers
- restrained, meaningful color (one accent, ~60/30/10, motivated)
- subtle layering — quiet elevation, findable-not-harsh borders, one committed depth strategy
- composition with rhythm, and proportions that state relationships
- complete states, adequate polish, purposeful sub-300ms motion
- reuses the project's components/system and accessible primitives — no hand-rolled control that a primitive should own
- no structural hacks; coherent content
- would not be mistaken for generic AI output

**Presumptive blockers** (unless the author justifies them against intent): no focal point · size-only or defaulted typography · monotone layout · timid or competing-accent palette · harsh or fragmented surfaces · missing states · structural hacks · an inaccessible hand-rolled control (no keyboard/focus/ARIA) where a primitive or existing component should be used. Any one present → **not approved**; leave explicit, actionable feedback and push for the crafted version.

---

# Applying fixes

Only when the user asks. Then rebuild the flagged parts **from the decision, not as patches over the defaults** — re-derive the focal point, the type hierarchy, the surface system, rather than nudging values. Start with blockers, then should-fix. Don't narrate the walk-through; do the work and show the result. Leave structural rebuilds that exceed a focused fix as clearly-named follow-ups rather than half-doing them.
