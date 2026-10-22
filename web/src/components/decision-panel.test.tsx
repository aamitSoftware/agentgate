import { fireEvent, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { alice, bob, renderAs } from "@/test/providers";
import { DecisionPanel } from "./decision-panel";

const panel = (props: Partial<Parameters<typeof DecisionPanel>[0]> = {}) => (
  <DecisionPanel
    subject="the manifest"
    firstApproverId={null}
    complete={false}
    undecided
    closedText="Closed."
    finalApproveLabel="Approve and pin"
    rejectConsequence="It stays quarantined."
    onDecide={vi.fn().mockResolvedValue(undefined)}
    {...props}
  />
);

describe("DecisionPanel", () => {
  it("does not let the first approver give the second approval, and says why", () => {
    renderAs(panel({ firstApproverId: alice }));
    expect(screen.getByRole("button", { name: "Approve and pin" })).toBeDisabled();
    expect(screen.getByText(/You gave the first approval/)).toBeVisible();
  });

  it("lets a different reviewer give the final approval", async () => {
    const onDecide = vi.fn().mockResolvedValue(undefined);
    renderAs(panel({ firstApproverId: bob, onDecide }));
    fireEvent.click(screen.getByRole("button", { name: "Approve and pin" }));
    await waitFor(() => expect(onDecide).toHaveBeenCalledWith("approve", ""));
  });

  it("requires a reason before approving a high-risk manifest", () => {
    renderAs(panel({ highRisk: true }));
    const approve = screen.getByRole("button", { name: "Approve" });
    expect(approve).toBeDisabled();
    fireEvent.change(screen.getByLabelText("Why is this acceptable?"), { target: { value: "Vendor removed the tag; verified." } });
    expect(approve).toBeEnabled();
  });

  it("shows a refusal from the service next to the buttons", async () => {
    renderAs(panel({ onDecide: vi.fn().mockRejectedValue(new Error("The pending manifest changed.")) }));
    fireEvent.click(screen.getByRole("button", { name: "Approve" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("The pending manifest changed.");
  });

  it("explains, instead of offering buttons, to someone who cannot review", () => {
    renderAs(panel(), { canReview: false, readOnly: true, roles: ["auditor"] });
    expect(screen.getByText(/Security reviewers decide/)).toBeVisible();
    expect(screen.queryByRole("button", { name: /Approve/ })).toBeNull();
  });
});
