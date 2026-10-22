import { test } from "@playwright/test";
import { signInWithFixtures } from "./fixtures";

// Records the console walkthrough used for docs/images/console.gif (RECORD=1 npx playwright test walkthrough).
// Fixture data, not a live system: it shows what each screen says about the rug-pull and anomaly stories.
test.skip(!process.env.RECORD, "recording only");
test.use({ video: { mode: "on", size: { width: 1280, height: 800 } }, viewport: { width: 1280, height: 800 }, colorScheme: "light" });

test("walkthrough", async ({ page }) => {
  await signInWithFixtures(page);
  const pause = (ms: number) => page.waitForTimeout(ms);

  await page.goto("/");
  await pause(2500);
  await page.getByRole("link", { name: /Re-review crm's manifest/ }).click();
  await pause(2500);
  await page.locator("pre").first().scrollIntoViewIfNeeded();
  await pause(3000);
  await page.mouse.wheel(0, -2000);
  await pause(800);
  await page.getByRole("button", { name: "Reject…" }).click();
  await pause(1800);
  await page.keyboard.press("Escape");
  await page.getByRole("link", { name: "Live activity" }).click();
  await pause(2000);
  await page.getByRole("button", { name: "Show details" }).nth(5).click();
  await pause(2500);
  await page.getByRole("link", { name: "Servers" }).click();
  await pause(1500);
  await page.getByRole("link", { name: "crm", exact: true }).click();
  await pause(2500);
});
