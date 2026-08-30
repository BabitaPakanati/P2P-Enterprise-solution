import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import { adminApi, platformLogin } from "../api/admin";
import { ApiError, type Api } from "../api/client";

const STORAGE_KEY = "p2p.adminSession";

export interface AdminUser {
  token: string;
  adminId: string;
  displayName: string;
  email: string;
}

interface AdminSessionValue {
  admin: AdminUser | null;
  api: Api;
  ready: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  loginLoading: boolean;
  loginError: string | null;
}

const AdminSessionContext = createContext<AdminSessionValue | null>(null);

function loadStored(): AdminUser | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as AdminUser) : null;
  } catch {
    return null;
  }
}

/**
 * Entirely separate from SessionContext (org users) on purpose - a platform admin's
 * token carries no organisation at all (see JwtTokenService.CreatePlatformAdminToken),
 * so mixing the two sessions would mean one context sometimes has an org and
 * sometimes doesn't. Two contexts, two storage keys, two login flows - simpler than
 * one context trying to represent both.
 */
export function AdminSessionProvider({ children }: { children: ReactNode }) {
  const [admin, setAdmin] = useState<AdminUser | null>(loadStored);
  const [loginLoading, setLoginLoading] = useState(false);
  const [loginError, setLoginError] = useState<string | null>(null);

  const login = useCallback(async (email: string, password: string) => {
    setLoginLoading(true);
    setLoginError(null);
    try {
      const result = await platformLogin(email, password);
      const user: AdminUser = { token: result.token, adminId: result.adminId, displayName: result.displayName, email: result.email };
      localStorage.setItem(STORAGE_KEY, JSON.stringify(user));
      setAdmin(user);
    } catch (e) {
      setLoginError(e instanceof ApiError ? e.message : "Could not reach the API.");
      throw e;
    } finally {
      setLoginLoading(false);
    }
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY);
    setAdmin(null);
  }, []);

  // See client.ts's createApi comment on onUnauthorized.
  const api = useMemo(() => adminApi(admin?.token ?? null, logout), [admin?.token, logout]);

  const value: AdminSessionValue = { admin, api, ready: admin !== null, login, logout, loginLoading, loginError };

  return <AdminSessionContext.Provider value={value}>{children}</AdminSessionContext.Provider>;
}

export function useAdminSession(): AdminSessionValue {
  const ctx = useContext(AdminSessionContext);
  if (!ctx) throw new Error("useAdminSession must be used within an AdminSessionProvider.");
  return ctx;
}
