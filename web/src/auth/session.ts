import { UserManager, WebStorageStateStore, type User } from "oidc-client-ts";

/** Keycloak OIDC, authorization code + PKCE, public client (no secret in the browser). */
export const userManager = new UserManager({
  authority: __OIDC_AUTHORITY__,
  client_id: "agentgate-console",
  redirect_uri: `${window.location.origin}/auth/callback`,
  post_logout_redirect_uri: window.location.origin,
  response_type: "code",
  scope: "openid profile",
  automaticSilentRenew: true, // refresh-token renewal before the access token expires
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
});

export async function getAccessToken(): Promise<string | null> {
  const user = await userManager.getUser();
  return user && !user.expired ? user.access_token : null;
}

export interface Identity {
  /** The subject the services record as ActorId (four-eyes compares these). */
  sub: string;
  username: string;
  roles: string[];
}

/** The access token's claims are what the APIs authorize on, so the console reads roles from there too. */
export function identityOf(user: User): Identity {
  const payload = decodeJwtPayload(user.access_token);
  const roles = Array.isArray(payload.roles) ? (payload.roles as string[]) : [];
  return {
    sub: String(payload.sub ?? user.profile.sub),
    username: String(user.profile.preferred_username ?? payload.preferred_username ?? user.profile.sub),
    roles,
  };
}

export function decodeJwtPayload(token: string): Record<string, unknown> {
  const part = token.split(".")[1] ?? "";
  const base64 = part.replace(/-/g, "+").replace(/_/g, "/").padEnd(Math.ceil(part.length / 4) * 4, "=");
  try {
    return JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(base64), (char) => char.charCodeAt(0)))) as Record<string, unknown>;
  } catch {
    return {};
  }
}
