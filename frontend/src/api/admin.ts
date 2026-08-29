import { createApi, type Api, ApiError } from "./client";

const BASE_URL = "http://localhost:5282";

export interface PlatformLoginResponse {
  token: string;
  adminId: string;
  displayName: string;
  email: string;
}

/** Pre-auth, same rationale as the org-user login in client.ts. */
export async function platformLogin(email: string, password: string): Promise<PlatformLoginResponse> {
  const res = await fetch(`${BASE_URL}/api/v1/platform/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) {
    let message = `${res.status} ${res.statusText}`;
    try {
      const data = await res.json();
      if (data?.error) message = data.error;
    } catch {
      /* not JSON */
    }
    throw new ApiError(message);
  }
  return res.json();
}

export interface PlatformAdminSeedResponse {
  adminId: string;
  email: string;
  devPassword: string;
  alreadySeeded: boolean;
}

/** Dev-only bootstrap - see PlatformAdminSeeder. */
export async function seedPlatformAdmin(): Promise<PlatformAdminSeedResponse> {
  const res = await fetch(`${BASE_URL}/api/v1/platform/_diagnostics/seed-admin`, { method: "POST" });
  if (!res.ok) throw new ApiError(`${res.status} ${res.statusText}`);
  return res.json();
}

export interface OrganisationSummary {
  id: string;
  orgCode: string;
  displayName: string;
  schemaName: string;
  status: string;
  createdAtUtc: string;
}

export const adminApi = (token: string | null): Api => createApi(token);

export const listOrganisations = (api: Api) => api.get<OrganisationSummary[]>("/api/v1/platform/organisations");
export const createOrganisation = (api: Api, orgCode: string, displayName: string) =>
  api.post<OrganisationSummary>("/api/v1/platform/organisations", { orgCode, displayName });
