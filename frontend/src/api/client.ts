const BASE_URL = "http://localhost:5282";

export class ApiError extends Error {}

interface RequestOptions {
  method?: "GET" | "POST";
  body?: unknown;
  orgCode: string;
  userId?: string | null;
}

async function request<T>(path: string, opts: RequestOptions): Promise<T> {
  const headers: Record<string, string> = { "X-Org-Code": opts.orgCode };
  if (opts.userId) headers["X-User-Id"] = opts.userId;
  if (opts.body !== undefined) headers["Content-Type"] = "application/json";

  const res = await fetch(`${BASE_URL}${path}`, {
    method: opts.method ?? "GET",
    headers,
    body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
  });

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
 * Every backend action needs an org + acting user - X-Org-Code / X-User-Id stand in
 * for real auth claims, see docs/ARCHITECTURE.md. This factory just closes over the
 * current session so every call site doesn't have to.
 */
export function createApi(orgCode: string, userId: string | null) {
  const get = <T,>(path: string) => request<T>(path, { orgCode, userId });
  const post = <T,>(path: string, body?: unknown) => request<T>(path, { method: "POST", body: body ?? {}, orgCode, userId });

  return { get, post, orgCode, userId };
}

export type Api = ReturnType<typeof createApi>;
