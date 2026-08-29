import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { ShieldCheck, Building2, LogOut } from "lucide-react";
import { useAdminSession } from "../context/AdminSessionContext";
import { ThemeToggle } from "./ThemeToggle";

export function AdminLayout() {
  const { admin, logout } = useAdminSession();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate("/admin/login");
  };

  return (
    <div className="app-shell">
      <aside className="rail">
        <div className="rail-brand">
          <div className="mark" style={{ background: "linear-gradient(155deg, var(--critical), #7a2a2a)" }}>
            <ShieldCheck size={16} strokeWidth={2.25} />
          </div>
          <div className="name">Root<small>Platform Admin</small></div>
        </div>

        <nav>
          <div className="rail-section-label">Platform</div>
          <NavLink to="/admin/organisations" className={({ isActive }) => (isActive ? "active" : "")}>
            <Building2 size={16} strokeWidth={2} />
            Organisations
          </NavLink>
        </nav>

        <div className="rail-footer">
          <div className="account-chip">
            <div className="avatar">{admin?.displayName?.slice(0, 2).toUpperCase() ?? "—"}</div>
            <div className="who">
              <span className="name">{admin?.displayName ?? "—"}</span>
              <span className="org">{admin?.email}</span>
            </div>
          </div>
          <button className="ghost small" style={{ width: "100%", justifyContent: "center", marginTop: "0.4rem" }} onClick={handleLogout}>
            <LogOut size={13} strokeWidth={2.25} />Sign out
          </button>
        </div>
      </aside>

      <div className="main-col">
        <div className="top-bar">
          <div />
          <ThemeToggle />
        </div>
        <div className="content">
          <Outlet />
        </div>
      </div>
    </div>
  );
}
