import { test } from "@playwright/test";
import { signInWithFixtures } from "./fixtures";

// Records the console walkthrough used for docs/images/console.gif (RECORD=1 npx playwright test walkthrough).
// Fixture data, not a live system: it shows what each screen says about the rug-pull and anomaly stories.
test.skip(!process.env.RECORD, "recording only");
test.use({ video: { mode: "on", size: { width: 1280, height: 800 } }, viewport: { width: 1280, height: 800 } });

test("walkthrough", async ({ page }) => {
  const { serverId } = await signInWithFixtures(page);
  const pause = (ms: number) => page.waitForTimeout(ms);

  await page.goto("/inventory");
  await pause(2500);
  await page.getByRole("link", { name: "crm", exact: true }).click();
  await pause(2500);
  await page.getByRole("link", { name: /Open review/ }).click();
  await pause(2000);
  await page.locator("pre").first().scrollIntoViewIfNeeded();
  await pause(3000);
  await page.mouse.wheel(0, -2000);
  await page.getByText("You gave the first approval").scrollIntoViewIfNeeded();
  await pause(2000);
  await page.getByRole("link", { name: "Live activity" }).click();
  await pause(3500);
  await page.goto(`/servers/${serverId}`);
  await pause(500);
});
