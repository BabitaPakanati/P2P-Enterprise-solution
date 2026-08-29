import { NavLink, Outlet } from "react-router-dom";
import { useSession } from "../context/SessionContext";

export function Layout() {
  const { orgCode, orgs, setOrgCode, users, currentUserId, setCurrentUserId, ready, error } = useSession();

  return (
    <div className="app-shell">
      <aside className="rail">
        <div className="rail-brand">
          P2P <span>Control Tower</span>
        </div>
        <nav>
          <NavLink to="/" end>Dashboard</NavLink>
          <NavLink to="/requisitions">Requisitions</NavLink>
          <NavLink to="/approvals">Approvals</NavLink>
          <NavLink to="/purchase-orders">Purchase Orders</NavLink>
        </nav>
      </aside>
      <div className="main-col">
        <div className="top-bar">
          <div className="session-picker">
            <label>
              Organisation{" "}
              <select value={orgCode} onChange={(e) => setOrgCode(e.target.value)}>
                {orgs.map((o) => (
                  <option key={o.code} value={o.code}>{o.label}</option>
                ))}
              </select>
            </label>
            <label>
              Signed in as{" "}
              <select value={currentUserId ?? ""} onChange={(e) => setCurrentUserId(e.target.value)} disabled={users.length === 0}>
                {users.map((u) => (
                  <option key={u.id} value={u.id}>{u.label}</option>
                ))}
              </select>
            </label>
          </div>
        </div>
        <div className="content">
          {error && <div className="error-banner">Could not reach the API: {error}. Is the backend running on http://localhost:5282?</div>}
          {!ready ? <div className="loading">Loading organisation…</div> : <Outlet />}
        </div>
      </div>
    </div>
  );
}
