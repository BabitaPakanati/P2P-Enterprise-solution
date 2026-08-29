import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import { createApi, login as loginRequest, type Api, type LoginResponse } from "../api/client";
import { ApiError } from "../api/client";

const STORAGE_KEY = "p2p.session";

export interface SessionUser {
  token: string;
  userId: string;
  displayName: string;
  email: string;
  organisationId: string;
  orgCode: string;
  orgDisplayName: string;
}

interface SessionValue {
  user: SessionUser | null;
  api: Api;
  /** True once a real, authenticated session exists - kept under this name so every page's existing `if (ready)` guard still means the right thing. */
  ready: boolean;
  login: (orgCode: string, email: string, password: string) => Promise<void>;
  logout: () => void;
  loginLoading: boolean;
  loginError: string | null;
}

const SessionContext = createContext<SessionValue | null>(null);

function loadStoredUser(): SessionUser | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as SessionUser) : null;
  } catch {
    return null;
  }
}

function toSessionUser(r: LoginResponse): SessionUser {
  return {
    token: r.token, userId: r.userId, displayName: r.displayName, email: r.email,
    organisationId: r.organisationId, orgCode: r.orgCode, orgDisplayName: r.orgDisplayName,
  };
}

/**
 * Real authentication now (a signed JWT from POST /api/v1/auth/login), not the org
 * + acting-user dropdowns this used to expose - see docs/ARCHITECTURE.md's "harden"
 * milestone. The context shape (`api`, `ready`) is kept stable on purpose so pages
 * built against the old dev-header session didn't need touching.
 */
export function SessionProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<SessionUser | null>(loadStoredUser);
  const [loginLoading, setLoginLoading] = useState(false);
  const [loginError, setLoginError] = useState<string | null>(null);

  const login = useCallback(async (orgCode: string, email: string, password: string) => {
    setLoginLoading(true);
    setLoginError(null);
    try {
      const result = await loginRequest(orgCode, email, password);
      const sessionUser = toSessionUser(result);
      localStorage.setItem(STORAGE_KEY, JSON.stringify(sessionUser));
      setUser(sessionUser);
    } catch (e) {
      const message = e instanceof ApiError ? e.message : "Could not reach the API.";
      setLoginError(message);
      throw e;
    } finally {
      setLoginLoading(false);
    }
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY);
    setUser(null);
  }, []);

  const api = useMemo(() => createApi(user?.token ?? null), [user?.token]);

  const value: SessionValue = { user, api, ready: user !== null, login, logout, loginLoading, loginError };

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionValue {
  const ctx = useContext(SessionContext);
  if (!ctx) throw new Error("useSession must be used within a SessionProvider.");
  return ctx;
}
