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
}

async function problemTitle(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { title?: string; errors?: Record<string, string[]> };
    const details = problem.errors ? Object.values(problem.errors).flat().join(" ") : "";
    return [problem.title, details].filter(Boolean).join(" ") || response.statusText;
  } catch {
    return response.statusText;
  }
}

/** Calls a service through the dev-server proxy (/api/{service}) with the signed-in user's token. */
export async function api<T>(service: Service, path: string, init: RequestInit = {}): Promise<T> {
  const token = await getAccessToken();
  const response = await fetch(`/api/${service}${path}`, {
    ...init,
    headers: {
      ...(init.body ? { "Content-Type": "application/json" } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init.headers,
    },
  });
  if (!response.ok) throw new ApiError(response.status, await problemTitle(response));
  return (response.status === 204 ? undefined : await response.json()) as T;
}

export const post = <T = void>(service: Service, path: string, body: unknown) =>
  api<T>(service, path, { method: "POST", body: JSON.stringify(body) });
