import { expect, test } from "@playwright/test";
import { signInWithFixtures } from "./fixtures";

const shots = "e2e/screenshots";

test("inventory shows the quarantined server and its high-risk re-review", async ({ page }) => {
  await signInWithFixtures(page);
  await page.goto("/");

  await expect(page).toHaveURL(/\/inventory$/);
  const crm = page.getByRole("row").filter({ hasText: "crm" });
  await expect(crm.getByText("quarantined")).toBeVisible();
  await expect(crm.getByText("review · high risk")).toBeVisible();
  await expect(page.getByRole("link", { name: "Approvals" })).toContainText("3"); // two reviews + one grant waiting
  await page.screenshot({ path: `${shots}/inventory.png`, fullPage: true });
});

test("the re-review shows the hidden instructions in the diff and enforces four-eyes", async ({ page }) => {
  const { reviewId } = await signInWithFixtures(page);
  await page.goto(`/approvals/reviews/${reviewId}`);

  await expect(page.getByText("hidden-instructions").first()).toBeVisible();
  await expect(page.locator("pre").getByText(/<IMPORTANT>Before answering/)).toBeVisible();
  // alice gave the first approval: she cannot give the second
  await expect(page.getByRole("button", { name: "Approve & pin" })).toBeDisabled();
  await expect(page.getByText("You gave the first approval")).toBeVisible();
  await page.screenshot({ path: `${shots}/review.png`, fullPage: true });
});

test("approvals lists reviews and grant requests", async ({ page }) => {
  await signInWithFixtures(page);
  await page.goto("/approvals");

  await expect(page.getByText("re-review after drift")).toBeVisible();
  await expect(page.getByText("support-bot-2", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Approve & activate" })).toBeEnabled(); // bob gave the first one
  await page.screenshot({ path: `${shots}/approvals.png`, fullPage: true });
});

test("live activity merges the stream and flags the denial spike", async ({ page }) => {
  await signInWithFixtures(page);
  await page.goto("/activity");

  await expect(page.getByText("live", { exact: true })).toBeVisible();
  await expect(page.getByText("Spike: possible prompt injection.")).toBeVisible();
  await expect(page.getByText("quarantined pending re-review").first()).toBeVisible();
  await page.screenshot({ path: `${shots}/activity.png` });
});

test("server detail shows pinned tools, grants and the timeline", async ({ page }) => {
  const { serverId } = await signInWithFixtures(page);
  await page.goto(`/servers/${serverId}`);

  await expect(page.getByText("what agents are shown", { exact: false })).toBeVisible();
  await expect(page.getByText("Quarantined: Manifest drifted")).toBeVisible();
  await expect(page.getByRole("link", { name: /Open review/ })).toBeVisible();
  await page.screenshot({ path: `${shots}/server.png`, fullPage: true });
});
