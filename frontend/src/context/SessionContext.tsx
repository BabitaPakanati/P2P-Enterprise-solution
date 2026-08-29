import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { createApi, type Api } from "../api/client";
import { seedFoundation } from "../api/procurement";

const ORGS = [
  { code: "acme", label: "Acme Corporation" },
  { code: "globex", label: "Globex Corporation" },
];

interface KnownUser {
  id: string;
  label: string;
}

interface SessionValue {
  orgCode: string;
  orgs: typeof ORGS;
  setOrgCode: (code: string) => void;
  users: KnownUser[];
  currentUserId: string | null;
  setCurrentUserId: (id: string) => void;
  api: Api;
  ready: boolean;
  error: string | null;
}

const SessionContext = createContext<SessionValue | null>(null);

/**
 * Stands in for real sign-in: picks an organisation (-> X-Org-Code) and an acting
 * user (-> X-User-Id) from the pair FoundationSeeder creates for that org. Calling
 * seed-foundation on every org switch is safe - it's idempotent (see
 * P2P.Api/Diagnostics/FoundationSeeder.cs) - and means this page never needs its own
 * "first run" step.
 */
export function SessionProvider({ children }: { children: ReactNode }) {
  const [orgCode, setOrgCodeState] = useState(() => localStorage.getItem("p2p.orgCode") ?? "acme");
  const [users, setUsers] = useState<KnownUser[]>([]);
  const [currentUserId, setCurrentUserIdState] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const bootstrap = useCallback(async (org: string) => {
    setReady(false);
    setError(null);
    try {
      const anonymousApi = createApi(org, null);
      const seed = await seedFoundation(anonymousApi);
      const knownUsers: KnownUser[] = [
        { id: seed.requesterId, label: "Priya Sharma (Requester)" },
        { id: seed.approverId, label: "Karan Mehta (Approver)" },
      ];
      setUsers(knownUsers);
      const stored = localStorage.getItem(`p2p.userId.${org}`);
      const nextUser = stored && knownUsers.some((u) => u.id === stored) ? stored : knownUsers[0].id;
      setCurrentUserIdState(nextUser);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not reach the API.");
    } finally {
      setReady(true);
    }
  }, []);

  useEffect(() => {
    bootstrap(orgCode);
  }, [orgCode, bootstrap]);

  const setOrgCode = useCallback((code: string) => {
    localStorage.setItem("p2p.orgCode", code);
    setOrgCodeState(code);
  }, []);

  const setCurrentUserId = useCallback(
    (id: string) => {
      localStorage.setItem(`p2p.userId.${orgCode}`, id);
      setCurrentUserIdState(id);
    },
    [orgCode],
  );

  const api = useMemo(() => createApi(orgCode, currentUserId), [orgCode, currentUserId]);

  const value: SessionValue = { orgCode, orgs: ORGS, setOrgCode, users, currentUserId, setCurrentUserId, api, ready, error };

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionValue {
  const ctx = useContext(SessionContext);
  if (!ctx) throw new Error("useSession must be used within a SessionProvider.");
  return ctx;
}
