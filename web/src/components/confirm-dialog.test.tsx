import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "./confirm-dialog";

const dialog = (onConfirm: (reason: string) => Promise<unknown>) => (
  <ConfirmDialog
    trigger={<Button>Revoke…</Button>}
    title="Revoke demo-agent's grant?"
    consequence="It loses access immediately."
    confirmLabel="Revoke grant"
    reasonLabel="Why are you revoking it?"
    onConfirm={onConfirm}
  />
);

describe("ConfirmDialog", () => {
  it("states the consequence and requires a reason before confirming", async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    render(dialog(onConfirm));
    fireEvent.click(screen.getByRole("button", { name: "Revoke…" }));

    expect(screen.getByText("It loses access immediately.")).toBeVisible();
    const confirm = screen.getByRole("button", { name: "Revoke grant" });
    expect(confirm).toBeDisabled();
    fireEvent.change(screen.getByLabelText("Why are you revoking it?"), { target: { value: "  Agent retired.  " } });
    fireEvent.click(confirm);

    await waitFor(() => expect(onConfirm).toHaveBeenCalledWith("Agent retired."));
    await waitFor(() => expect(screen.queryByRole("alertdialog")).toBeNull());
  });

  it("stays open with the service's refusal", async () => {
    render(dialog(vi.fn().mockRejectedValue(new Error("Grant is already revoked."))));
    fireEvent.click(screen.getByRole("button", { name: "Revoke…" }));
    fireEvent.change(screen.getByLabelText("Why are you revoking it?"), { target: { value: "Retired." } });
    fireEvent.click(screen.getByRole("button", { name: "Revoke grant" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Grant is already revoked.");
    expect(screen.getByRole("alertdialog")).toBeVisible();
  });
});
