import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import { signInWithFixtures } from "./fixtures";

// Every page, in both themes, has no serious or critical WCAG 2.2 A/AA violation (accessibility skill).
const pages = (ids: { serverId: string; agentId: string; reviewId: string }) => [
  "/",
  "/approvals",
  "/approvals?tab=grants",
  `/approvals/reviews/${ids.reviewId}`,
  "/servers",
  `/servers/${ids.serverId}`,
  "/agents",
  `/agents/${ids.agentId}`,
  "/grants",
  "/activity",
];

for (const scheme of ["light", "dark"] as const) {
  test(`no serious accessibility violations, ${scheme}`, async ({ page }) => {
    const ids = await signInWithFixtures(page);
    await page.emulateMedia({ colorScheme: scheme });
    const failures: string[] = [];
    for (const path of pages(ids)) {
      await page.goto(path);
      await page.getByRole("heading", { level: 1 }).waitFor();
      await page.waitForLoadState("networkidle");
      const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"]).analyze();
      for (const violation of results.violations.filter((candidate) => candidate.impact === "serious" || candidate.impact === "critical"))
        failures.push(`${path}: ${violation.id} (${violation.impact}) ${violation.nodes.map((node) => node.target.join(" ")).slice(0, 3).join(", ")} — ${violation.nodes[0]?.failureSummary?.split("\n").slice(0, 2).join(" ")}`);
    }
    expect(failures, failures.join("\n")).toEqual([]);
  });
}
