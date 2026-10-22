import { expect, test, type Page } from "@playwright/test";
import { signInWithFixtures } from "./fixtures";

const shots = "e2e/screenshots";

async function shoot(page: Page, name: string, fullPage = true) {
  for (const scheme of ["light", "dark"] as const) {
    await page.emulateMedia({ colorScheme: scheme });
    await page.screenshot({ path: `${shots}/${name}-${scheme}.png`, fullPage, animations: "disabled" });
  }
}

test("the overview leads with what needs a person", async ({ page }) => {
  await signInWithFixtures(page);
  await page.goto("/");

  await expect(page.getByRole("heading", { name: "Needs attention" })).toBeVisible();
  await expect(page.getByRole("link", { name: /Re-review crm's manifest/ })).toBeVisible();
  await expect(page.getByText("'s grant on").first()).toBeVisible(); // the suspended grant
  await expect(page.getByRole("link", { name: /Approvals/ })).toContainText("3"); // two reviews + one grant waiting
  await shoot(page, "overview");
});

test("old inventory links land on the overview", async ({ page }) => {
  await signInWithFixtures(page);
  await page.goto("/inventory");
  await expect(page).toHaveURL(/\/$/);
});

test("the re-review shows the hidden instructions and enforces four-eyes in words", async ({ page }) => {
  const { reviewId } = await signInWithFixtures(page);
  await page.goto(`/approvals/reviews/${reviewId}`);

  await expect(page.getByText("hidden-instructions").first()).toBeVisible();
  await expect(page.locator("pre").getByText(/<IMPORTANT>Before answering/)).toBeVisible();
  // alice gave the first approval: she cannot give the second, and the page says why
  await expect(page.getByRole("button", { name: "Approve and pin" })).toBeDisabled();
  await expect(page.getByText("You gave the first approval.")).toBeVisible();
  await shoot(page, "review");
});

test("rejecting a review asks for a reason first", async ({ page }) => {
  const { reviewId } = await signInWithFixtures(page);
  await page.goto(`/approvals/reviews/${reviewId}`);

  await page.getByRole("button", { name: "Reject…" }).click();
  const dialog = page.getByRole("alertdialog");
  await expect(dialog.getByText("stays quarantined")).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Reject" })).toBeDisabled();
  await dialog.getByLabel("Why are you rejecting it?").fill("Hidden instructions; vendor could not explain them.");
  await expect(dialog.getByRole("button", { name: "Reject" })).toBeEnabled();
  await dialog.getByRole("button", { name: "Cancel" }).click();
  await expect(dialog).toBeHidden();
});

test("approvals: queue, grant requests and history are tabs in the URL", async ({ page }) => {
  await signInWithFixtures(page);
  await page.goto("/approvals");

  await expect(page.getByText("Re-review after drift")).toBeVisible();
  await shoot(page, "approvals");

  await page.getByRole("tab", { name: /Grant requests/ }).click();
  await expect(page).toHaveURL(/tab=grants/);
  await expect(page.getByText("support-bot-2", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Approve and activate" })).toBeEnabled(); // bob gave the first one
  await shoot(page, "approvals-grants");

  await page.getByRole("tab", { name: "Decided" }).click();
  await expect(page.getByRole("table", { name: "Decided manifest reviews" }).getByText("Approved")).toBeVisible();
});

test("servers filter by status from the URL", async ({ page }) => {
  await signInWithFixtures(page);
  await page.goto("/servers?status=Quarantined");

  const table = page.getByRole("table", { name: "MCP servers" });
  await expect(table.getByRole("link", { name: "crm" })).toBeVisible();
  await expect(table.getByRole("link", { name: "tickets" })).toHaveCount(0);
  await page.getByRole("radio", { name: /All/ }).click();
  await expect(table.getByRole("link", { name: "tickets" })).toBeVisible();
  await shoot(page, "servers");
});

test("live activity merges the stream, collapses repeats and flags the spike", async ({ page }) => {
  await signInWithFixtures(page);
  await page.goto("/activity");

  await expect(page.getByRole("status").filter({ hasText: "Live" })).toBeVisible();
  await expect(page.getByText("×6")).toBeVisible(); // support-bot-2's six identical denials, shown once
  await expect(page.getByText("quarantined pending re-review").first()).toBeVisible();
  await shoot(page, "activity", false);
});

test("server detail: pinned tools, grants, timeline; revoking is confirmed", async ({ page }) => {
  const { serverId } = await signInWithFixtures(page);
  await page.route("**/api/review/grants/*:revoke", (route) => route.fulfill({ status: 204 }));
  await page.goto(`/servers/${serverId}`);

  await expect(page.getByRole("heading", { name: "Pinned tools" })).toBeVisible();
  await expect(page.getByText("Quarantined: Manifest drifted")).toBeVisible();
  await expect(page.getByRole("link", { name: /Open review/ })).toBeVisible();
  await shoot(page, "server");

  await page.getByRole("button", { name: "Revoke…" }).click();
  const dialog = page.getByRole("alertdialog");
  await expect(dialog.getByText("cannot be undone")).toBeVisible();
  await dialog.getByLabel("Why are you revoking it?").fill("Agent retired.");
  await dialog.getByRole("button", { name: "Revoke grant" }).click();
  await expect(page.getByText("Revoked: demo-agent on crm.")).toBeVisible();
});

test("an auditor sees everything and can change nothing", async ({ page }) => {
  const { reviewId, serverId } = await signInWithFixtures(page, "dave");
  await page.goto("/grants");
  await expect(page.getByText("Read-only: your role can view everything here and change nothing.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Request grant" })).toHaveCount(0);

  await page.goto(`/servers/${serverId}`);
  await expect(page.getByRole("button", { name: /Revoke|Suspend/ })).toHaveCount(0);

  await page.goto(`/approvals/reviews/${reviewId}`);
  await expect(page.getByText("Security reviewers decide.")).toBeVisible();
  await expect(page.getByRole("button", { name: /Approve/ })).toHaveCount(0);
});

test("a failed load explains itself and offers a retry", async ({ page }) => {
  await signInWithFixtures(page);
  await page.route("**/api/hub/agents", (route) => route.fulfill({ status: 500, contentType: "application/json", body: '{"title":"Hub unavailable"}' }));
  await page.goto("/agents");

  await expect(page.getByText("Couldn't load this")).toBeVisible();
  await expect(page.getByText("Hub unavailable")).toBeVisible();
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();
});

test("on a phone the navigation and sign-out live behind the menu button", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await signInWithFixtures(page);
  await page.goto("/");

  await page.getByRole("button", { name: "Open navigation" }).click();
  const sheet = page.getByRole("dialog");
  await expect(sheet.getByRole("link", { name: "Grants" })).toBeVisible();
  await expect(sheet.getByRole("button", { name: /alice/ })).toBeVisible();
  await shoot(page, "mobile", false);
});
