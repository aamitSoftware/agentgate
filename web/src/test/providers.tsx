import { render } from "@testing-library/react";
import type { ReactNode } from "react";
import { AuthContext, type AuthState } from "@/auth/auth";
import { TooltipProvider } from "@/components/ui/tooltip";

export const alice = "6c1f9d2e-3b7a-4f10-9a2e-1d5c8b7e4a01";
export const bob = "a93e0c47-5d21-4c8e-b6f4-0e2d9c71b8f3";

/** Renders as a signed-in user (a security reviewer unless told otherwise), without Keycloak. */
export function renderAs(ui: ReactNode, auth: Partial<AuthState> = {}) {
  const state: AuthState = {
    identity: { sub: alice, username: "alice", roles: ["security-reviewer"] },
    roles: ["security-reviewer"],
    canReview: true,
    canAdminister: false,
    readOnly: false,
    signOut: () => {},
    ...auth,
  };
  return render(
    <AuthContext value={state}>
      <TooltipProvider>{ui}</TooltipProvider>
    </AuthContext>,
  );
}
