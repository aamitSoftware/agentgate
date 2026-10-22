import type { User } from "oidc-client-ts";
import { LogIn } from "lucide-react";
import { createContext, use, useEffect, useMemo, useState, type ReactNode } from "react";
import { onUnauthorized } from "@/api/client";
import { BrandMark } from "@/components/brand";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { identityOf, userManager, type Identity } from "./session";

/** The realm roles the console knows (AgentGate.Abstractions Roles). */
export const Roles = {
  RegistryAdmin: "registry-admin",
  SecurityReviewer: "security-reviewer",
  Auditor: "auditor",
} as const;

const knownRoles: string[] = Object.values(Roles);

export interface AuthState {
  identity: Identity;
  /** The roles above that this user holds, for display. */
  roles: string[];
  canReview: boolean;
  canAdminister: boolean;
  /** Can see everything, change nothing (auditor). */
  readOnly: boolean;
  signOut: () => void;
}

/** Exported for tests, which provide an identity without signing in. */
export const AuthContext = createContext<AuthState | null>(null);

export function useAuth(): AuthState {
  const state = use(AuthContext);
  if (!state) throw new Error("useAuth outside AuthProvider");
  return state;
}

type Phase = { kind: "loading" } | { kind: "signedOut"; error?: string } | { kind: "signedIn"; user: User };

const returnToKey = "agentgate.returnTo";

let callback: Promise<User> | undefined;

/** An authorization code can be exchanged once; StrictMode runs effects twice, so the exchange is shared. */
function completeSignIn(): Promise<User> {
  callback ??= userManager.signinRedirectCallback();
  return callback;
}

function signIn() {
  sessionStorage.setItem(returnToKey, window.location.pathname + window.location.search);
  void userManager.signinRedirect();
}

/** Gates the whole console: nothing renders until there is a signed-in user (every API needs a token anyway). */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [phase, setPhase] = useState<Phase>({ kind: "loading" });
  // The session ran out while the console was open: keep the page (and anything typed) and offer to sign in.
  const [expired, setExpired] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const settle = (next: Phase) => {
      if (!cancelled) setPhase(next);
    };

    (async () => {
      if (window.location.pathname === "/auth/callback") {
        try {
          const user = await completeSignIn();
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

    const onLoaded = (user: User) => {
      setExpired(false);
      settle({ kind: "signedIn", user });
    };
    const onUnloaded = () => settle({ kind: "signedOut" });
    const onRenewFailed = () => setExpired(true);
    userManager.events.addUserLoaded(onLoaded);
    userManager.events.addUserUnloaded(onUnloaded);
    userManager.events.addSilentRenewError(onRenewFailed);
    const stopListening = onUnauthorized(onRenewFailed);
    return () => {
      cancelled = true;
      userManager.events.removeUserLoaded(onLoaded);
      userManager.events.removeUserUnloaded(onUnloaded);
      userManager.events.removeSilentRenewError(onRenewFailed);
      stopListening();
    };
  }, []);

  const state = useMemo<AuthState | null>(() => {
    if (phase.kind !== "signedIn") return null;
    const identity = identityOf(phase.user);
    const canReview = identity.roles.includes(Roles.SecurityReviewer);
    const canAdminister = identity.roles.includes(Roles.RegistryAdmin);
    return {
      identity,
      roles: identity.roles.filter((role) => knownRoles.includes(role)),
      canReview,
      canAdminister,
      readOnly: !canReview && !canAdminister,
      signOut: () => void userManager.signoutRedirect(),
    };
  }, [phase]);

  if (phase.kind === "loading") return <SignInFrame busy />;
  if (!state) return <SignInFrame error={phase.kind === "signedOut" ? phase.error : undefined} />;
  return (
    <AuthContext value={state}>
      {children}
      <SessionExpired open={expired} />
    </AuthContext>
  );
}

function SignInFrame({ busy, error }: { busy?: boolean; error?: string }) {
  return (
    <main className="grid min-h-svh place-items-center bg-background p-4">
      <div className="flex w-full max-w-sm flex-col gap-6 rounded-lg border bg-card p-6">
        <div className="flex items-center gap-2.5">
          <BrandMark />
          <span className="text-lg font-semibold tracking-tight">AgentGate</span>
        </div>
        <div className="flex flex-col gap-1.5">
          <h1 className="text-xl font-semibold tracking-tight">Sign in to the governance console</h1>
          <p className="text-muted-foreground">
            Review MCP servers and agent grants, and watch what agents do at the gateway. Use your realm account.
          </p>
        </div>
        {error && (
          <Alert variant="destructive">
            <AlertTitle>Sign-in did not complete</AlertTitle>
            <AlertDescription>
              <p>{error}.</p>
              <p>Try again; if it keeps failing, check that Keycloak is running.</p>
            </AlertDescription>
          </Alert>
        )}
        <Button size="lg" onClick={signIn} disabled={busy}>
          {busy ? <Spinner data-icon="inline-start" /> : <LogIn data-icon="inline-start" />}
          {busy ? "Checking your session…" : "Sign in with Keycloak"}
        </Button>
      </div>
    </main>
  );
}

function SessionExpired({ open }: { open: boolean }) {
  return (
    <AlertDialog open={open}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Your session has expired</AlertDialogTitle>
          <AlertDialogDescription>
            Sign in again to keep working. You will come back to this page; anything not yet submitted on it is lost.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogAction onClick={signIn}>Sign in again</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
