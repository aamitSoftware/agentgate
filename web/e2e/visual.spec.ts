import { expect, test } from "@playwright/test";
import { signInWithFixtures } from "./fixtures";

// The look of each page in each theme, so a change to tokens or components shows up as a reviewed diff.
// Times are masked (they move); update with `npx playwright test visual --update-snapshots` after looking.
const pages = (ids: { serverId: string; reviewId: string }) => ({
  overview: "/",
  approvals: "/approvals",
  review: `/approvals/reviews/${ids.reviewId}`,
  servers: "/servers",
  server: `/servers/${ids.serverId}`,
  grants: "/grants?status=all",
});

for (const scheme of ["light", "dark"] as const) {
  test(`pages look as designed, ${scheme}`, async ({ page }) => {
    const ids = await signInWithFixtures(page);
    await page.emulateMedia({ colorScheme: scheme, reducedMotion: "reduce" });
    for (const [name, path] of Object.entries(pages(ids))) {
      await page.goto(path);
      await page.getByRole("heading", { level: 1 }).waitFor();
      await page.waitForLoadState("networkidle");
      await expect(page).toHaveScreenshot(`${name}-${scheme}.png`, {
        fullPage: true,
        animations: "disabled",
        mask: [page.locator("time")],
        maxDiffPixelRatio: 0.01,
      });
    }
  });
}
