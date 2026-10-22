import { expect, test } from "@playwright/test";
import { fixtureNow, signInWithFixtures } from "./fixtures";

// The look of each page in each theme, so a change to tokens or components shows up as a reviewed diff.
// The browser's clock is frozen at the fixtures' moment, so "2 minutes ago" can't become "3 minutes ago" on a
// slow runner and reflow the page; times are masked too. Update with
// `npx playwright test visual --update-snapshots` after looking at the images.
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
    await page.clock.setFixedTime(fixtureNow);
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
        // CI's runner rasterises text slightly differently from a developer machine (about 1.5% of pixels on the
        // text-heaviest page); a moved panel or a changed colour changes far more than 3%.
        maxDiffPixelRatio: 0.03,
      });
    }
  });
}
