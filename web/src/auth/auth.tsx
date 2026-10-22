import type { User } from "oidc-client-ts";
import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { identityOf, userManager, type Identity } from "./session";

export const Roles = {
  RegistryAdmin: "registry-admin",
  SecurityReviewer: "security-reviewer",
  Auditor: "auditor",
} as const;

interface AuthState {
  identity: Identity;
  canReview: boolean;
  canAdminister: boolean;
  signOut: () => void;
}

const AuthContext = createContext<AuthState | null>(null);

export function useAuth(): AuthState {
  const state = useContext(AuthContext);
  if (!state) throw new Error("useAuth outside AuthProvider");
  return state;
}

type Phase = { kind: "loading" } | { kind: "signedOut"; error?: string } | { kind: "signedIn"; user: User };

const returnToKey = "agentgate.returnTo";

/** Gates the whole console: nothing renders until there is a signed-in user (every API needs a token anyway). */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [phase, setPhase] = useState<Phase>({ kind: "loading" });

  useEffect(() => {
    let cancelled = false;
    const settle = (next: Phase) => {
      if (!cancelled) setPhase(next);
    };

    (async () => {
      if (window.location.pathname === "/auth/callback") {
        try {
          const user = await userManager.signinRedirectCallback();
          const returnTo = sessionStorage.getItem(returnToKey) ?? "/";
          sessionStorage.removeItem(returnToKey);
          window.history.replaceState(null, "", returnTo);
          settle({ kind: "signedIn", user });
        } catch (error) {
          settle({ kind: "signedOut", error: error instanceof Error ? error.message : String(error) });
        }
        return;
      }
      const user = await userManager.getUser();
      settle(user && !user.expired ? { kind: "signedIn", user } : { kind: "signedOut" });
    })();

    const onLoaded = (user: User) => settle({ kind: "signedIn", user });
    const onUnloaded = () => settle({ kind: "signedOut" });
    userManager.events.addUserLoaded(onLoaded);
    userManager.events.addUserUnloaded(onUnloaded);
    userManager.events.addSilentRenewError(onUnloaded);
    return () => {
      cancelled = true;
      userManager.events.removeUserLoaded(onLoaded);
      userManager.events.removeUserUnloaded(onUnloaded);
      userManager.events.removeSilentRenewError(onUnloaded);
    };
  }, []);

  const state = useMemo<AuthState | null>(() => {
    if (phase.kind !== "signedIn") return null;
    const identity = identityOf(phase.user);
    return {
      identity,
      canReview: identity.roles.includes(Roles.SecurityReviewer),
      canAdminister: identity.roles.includes(Roles.RegistryAdmin),
      signOut: () => void userManager.signoutRedirect(),
    };
  }, [phase]);

  if (phase.kind === "loading") return null;
  if (!state) return <SignIn error={phase.kind === "signedOut" ? phase.error : undefined} />;
  return <AuthContext.Provider value={state}>{children}</AuthContext.Provider>;
}

function SignIn({ error }: { error?: string }) {
  const signIn = () => {
    sessionStorage.setItem(returnToKey, window.location.pathname + window.location.search);
    void userManager.signinRedirect();
  };
  return (
    <main className="grid h-full place-items-center bg-canvas">
      <div className="w-[22rem] rounded-md border border-line bg-surface p-6">
        <div className="mb-1 flex items-center gap-2 font-mono text-[11px] uppercase tracking-[0.18em] text-ink-faint">
          <span className="size-1.5 rounded-full bg-accent" />
          AgentGate
        </div>
        <h1 className="mb-1 text-lg font-semibold">Governance console</h1>
        <p className="mb-5 text-ink-muted">MCP servers, reviews, grants and live agent activity. Sign in with your realm account.</p>
        {error && <p className="mb-4 rounded border border-danger/40 bg-danger-soft px-3 py-2 text-danger">{error}</p>}
        <button
          onClick={signIn}
          className="w-full rounded border border-line-strong bg-raised px-3 py-2 font-medium hover:border-ink-faint hover:bg-line"
        >
          Sign in with Keycloak
        </button>
      </div>
    </main>
  );
}
