import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { LayoutDashboard, FileText, CheckSquare, ShoppingCart, Layers, LogOut, Shield, GitBranch } from "lucide-react";
import { useSession } from "../context/SessionContext";
import { ThemeToggle } from "./ThemeToggle";

const NAV = [
  { to: "/", label: "Dashboard", icon: LayoutDashboard, end: true },
  { to: "/requisitions", label: "Requisitions", icon: FileText, end: false },
  { to: "/approvals", label: "Approvals", icon: CheckSquare, end: false },
  { to: "/purchase-orders", label: "Purchase Orders", icon: ShoppingCart, end: false },
];

const SETTINGS_NAV = [
  { to: "/settings/roles", label: "Approval Roles", icon: Shield, end: false },
  { to: "/settings/workflows", label: "Workflows", icon: GitBranch, end: false },
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
  const { user, logout } = useSession();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate("/login");
  };

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

          <div className="rail-section-label">Settings</div>
          {SETTINGS_NAV.map(({ to, label, icon: Icon, end }) => (
            <NavLink key={to} to={to} end={end} className={({ isActive }) => (isActive ? "active" : "")}>
              <Icon size={16} strokeWidth={2} />
              {label}
            </NavLink>
          ))}
        </nav>

        <div className="rail-footer">
          <div className="account-chip">
            <div className="avatar">{user ? initials(user.displayName) : "—"}</div>
            <div className="who">
              <span className="name">{user?.displayName ?? "—"}</span>
              <span className="org">{user?.orgDisplayName}</span>
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
