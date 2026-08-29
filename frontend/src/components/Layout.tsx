import { NavLink, Outlet } from "react-router-dom";
import { LayoutDashboard, FileText, CheckSquare, ShoppingCart, Layers } from "lucide-react";
import { useSession } from "../context/SessionContext";
import { ThemeToggle } from "./ThemeToggle";

const NAV = [
  { to: "/", label: "Dashboard", icon: LayoutDashboard, end: true },
  { to: "/requisitions", label: "Requisitions", icon: FileText, end: false },
  { to: "/approvals", label: "Approvals", icon: CheckSquare, end: false },
  { to: "/purchase-orders", label: "Purchase Orders", icon: ShoppingCart, end: false },
];

function initials(label: string) {
  return label
    .split(" ")
    .filter((w) => w[0] && w[0] === w[0].toUpperCase())
    .slice(0, 2)
    .map((w) => w[0])
    .join("") || label.slice(0, 2).toUpperCase();
}

export function Layout() {
  const { orgCode, orgs, setOrgCode, users, currentUserId, setCurrentUserId, ready, error } = useSession();
  const currentUser = users.find((u) => u.id === currentUserId);
  const currentOrg = orgs.find((o) => o.code === orgCode);

  return (
    <div className="app-shell">
      <aside className="rail">
        <div className="rail-brand">
          <div className="mark"><Layers size={16} strokeWidth={2.25} /></div>
          <div className="name">P2P<small>Control Tower</small></div>
        </div>

        <nav>
          <div className="rail-section-label">Workspace</div>
          {NAV.map(({ to, label, icon: Icon, end }) => (
            <NavLink key={to} to={to} end={end} className={({ isActive }) => (isActive ? "active" : "")}>
              <Icon size={16} strokeWidth={2} />
              {label}
            </NavLink>
          ))}
        </nav>

        <div className="rail-footer">
          <div className="account-chip">
            <div className="avatar">{currentUser ? initials(currentUser.label) : "—"}</div>
            <div className="who">
              <span className="name">{currentUser?.label.replace(/\s*\(.*\)/, "") ?? "Loading…"}</span>
              <span className="org">{currentOrg?.label}</span>
            </div>
          </div>
        </div>
      </aside>

      <div className="main-col">
        <div className="top-bar">
          <div className="session-picker">
            <div className="picker">
              <span>Organisation</span>
              <select value={orgCode} onChange={(e) => setOrgCode(e.target.value)}>
                {orgs.map((o) => <option key={o.code} value={o.code}>{o.label}</option>)}
              </select>
            </div>
            <div className="picker">
              <span>Signed in as</span>
              <select value={currentUserId ?? ""} onChange={(e) => setCurrentUserId(e.target.value)} disabled={users.length === 0}>
                {users.map((u) => <option key={u.id} value={u.id}>{u.label}</option>)}
              </select>
            </div>
          </div>
          <ThemeToggle />
        </div>
        <div className="content">
          {error && <div className="error-banner">Could not reach the API: {error}. Is the backend running on http://localhost:5282?</div>}
          {!ready ? <div className="loading">Loading organisation…</div> : <Outlet />}
        </div>
      </div>
    </div>
  );
}
