const BASE_URL = "http://localhost:5282";

export class ApiError extends Error {}

async function parseResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    let message = `${res.status} ${res.statusText}`;
    try {
      const data = await res.json();
      if (data?.error) message = data.error;
    } catch {
      /* body wasn't JSON - keep the status line */
    }
    throw new ApiError(message);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

/**
 * Every authenticated call carries the JWT a successful login issued - see
 * SessionContext. No more X-Org-Code/X-User-Id header stand-ins: the token's
 * org_id/org_code/schema/sub claims are what the backend's
 * IdentityResolutionMiddleware reads now.
 *
 * `onUnauthorized` fires on a 401 from any call this instance makes (an
 * expired or otherwise invalid token) - SessionContext/AdminSessionContext
 * wire it to `logout()`. Without this, a page whose fetch quietly rejected
 * left its state at its initial empty array and rendered a normal-looking
 * "nothing here yet" empty state instead of ever surfacing that the session
 * had lapsed - e.g. Workflows.tsx's "no roles configured" banner appearing
 * for a user who actually has roles, just no working session to fetch them
 * with. Real bug, found live: a browser tab left open for hours, its token
 * expired, and every settings page silently looked reset instead of prompting
 * a re-login.
 */
export function createApi(token: string | null, onUnauthorized?: () => void) {
  const request = async <T,>(path: string, method: "GET" | "POST" | "PUT", body?: unknown): Promise<T> => {
    const headers: Record<string, string> = {};
    if (token) headers.Authorization = `Bearer ${token}`;
    if (body !== undefined) headers["Content-Type"] = "application/json";

    const res = await fetch(`${BASE_URL}${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
    if (res.status === 401) onUnauthorized?.();
    return parseResponse<T>(res);
  };

  return {
    get: <T,>(path: string) => request<T>(path, "GET"),
    post: <T,>(path: string, body?: unknown) => request<T>(path, "POST", body ?? {}),
    put: <T,>(path: string, body?: unknown) => request<T>(path, "PUT", body ?? {}),
  };
}

export type Api = ReturnType<typeof createApi>;

export interface LoginResponse {
  token: string;
  userId: string;
  displayName: string;
  email: string;
  organisationId: string;
  orgCode: string;
  orgDisplayName: string;
}

/** Pre-auth: there's no token yet, so the org is picked via header, same as the dev-only diagnostics. */
export async function login(orgCode: string, email: string, password: string): Promise<LoginResponse> {
  const res = await fetch(`${BASE_URL}/api/v1/auth/login`, {
    method: "POST",
    headers: { "X-Org-Code": orgCode, "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  return parseResponse<LoginResponse>(res);
}

export interface SeedResponse {
  requesterId: string;
  approverId: string;
  roleId: string;
  alreadySeeded: boolean;
  devPassword: string;
}

/** Dev-only bootstrap so a fresh org has someone to log in as - see FoundationSeeder. */
export async function seedFoundationForOrg(orgCode: string): Promise<SeedResponse> {
  const res = await fetch(`${BASE_URL}/api/v1/_diagnostics/seed-foundation`, {
    method: "POST",
    headers: { "X-Org-Code": orgCode },
  });
  return parseResponse<SeedResponse>(res);
}
