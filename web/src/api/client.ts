import { getAccessToken } from "@/auth/session";

export type Service = "registry" | "review" | "hub";

/** A failed call, with the problem-details title the API wrote for people (e.g. the four-eyes refusal). */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
  }

  get forbidden() {
    return this.status === 403;
  }

  get notFound() {
    return this.status === 404;
  }
}

let unauthorized: (() => void) | undefined;

/** The auth layer listens here: a 401 means the session is gone, and it offers to sign in again. */
export function onUnauthorized(handler: () => void): () => void {
  unauthorized = handler;
  return () => {
    if (unauthorized === handler) unauthorized = undefined;
  };
}

/** For callers outside `api` (the activity stream) that learn the session is gone. */
export const reportUnauthorized = () => unauthorized?.();

async function problemTitle(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { title?: string; errors?: Record<string, string[]> };
    const details = problem.errors ? Object.values(problem.errors).flat().join(" ") : "";
    return [problem.title, details].filter(Boolean).join(" ") || response.statusText;
  } catch {
    return response.statusText;
  }
}

const sessionExpired = "Your session has expired. Sign in again to continue.";

/** Calls a service through the dev-server proxy (/api/{service}) with the signed-in user's token. */
export async function api<T>(service: Service, path: string, init: RequestInit = {}): Promise<T> {
  const token = await getAccessToken();
  if (!token) {
    unauthorized?.();
    throw new ApiError(401, sessionExpired);
  }
  const headers = new Headers(init.headers);
  if (init.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  headers.set("Authorization", `Bearer ${token}`);

  const response = await fetch(`/api/${service}${path}`, { ...init, headers });
  if (response.status === 401) {
    unauthorized?.();
    throw new ApiError(401, sessionExpired);
  }
  if (!response.ok) throw new ApiError(response.status, await problemTitle(response));
  return (response.status === 204 ? undefined : await response.json()) as T;
}

export const post = <T = void>(service: Service, path: string, body: unknown) =>
  api<T>(service, path, { method: "POST", body: JSON.stringify(body) });
