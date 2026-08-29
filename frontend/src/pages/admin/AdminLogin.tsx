import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { ShieldCheck, LogIn, Sparkles } from "lucide-react";
import { useAdminSession } from "../../context/AdminSessionContext";
import { seedPlatformAdmin } from "../../api/admin";
import { ApiError } from "../../api/client";

export function AdminLogin() {
  const { login, loginLoading, loginError } = useAdminSession();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [seedInfo, setSeedInfo] = useState<{ email: string; password: string } | null>(null);
  const [seeding, setSeeding] = useState(false);
  const [seedError, setSeedError] = useState<string | null>(null);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await login(email, password);
      navigate("/admin/organisations");
    } catch {
      /* loginError already set */
    }
  };

  const seed = async () => {
    setSeeding(true);
    setSeedError(null);
    try {
      const result = await seedPlatformAdmin();
      setSeedInfo({ email: result.email, password: result.devPassword });
    } catch (e) {
      setSeedError(e instanceof ApiError ? e.message : "Could not reach the API.");
    } finally {
      setSeeding(false);
    }
  };

  return (
    <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", background: "var(--bg)" }}>
      <div style={{ width: 400 }}>
        <div style={{ display: "flex", alignItems: "center", gap: "0.6rem", justifyContent: "center", marginBottom: "1.8rem" }}>
          <div className="mark" style={{ width: 34, height: 34, background: "linear-gradient(155deg, var(--critical), #7a2a2a)" }}>
            <ShieldCheck size={18} strokeWidth={2.25} />
          </div>
          <div style={{ fontWeight: 700, fontSize: "1.15rem" }}>Platform Root Admin</div>
        </div>

        <form className="card" style={{ maxWidth: "none" }} onSubmit={submit}>
          <h3>Sign in</h3>
          <p className="hint" style={{ marginBottom: "1rem" }}>Operates across every organisation - not an org-level account.</p>
          {loginError && <div className="error-banner">{loginError}</div>}

          <div className="field" style={{ marginBottom: "1rem" }}>
            <label>Email</label>
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="root@platform.local" required autoFocus />
          </div>
          <div className="field" style={{ marginBottom: "1.1rem" }}>
            <label>Password</label>
            <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          </div>

          <button type="submit" className="primary" disabled={loginLoading} style={{ width: "100%", justifyContent: "center" }}>
            <LogIn size={14} strokeWidth={2.25} />{loginLoading ? "Signing in…" : "Sign in"}
          </button>
        </form>

        <div className="card" style={{ maxWidth: "none", marginTop: "0.9rem" }}>
          <p className="hint" style={{ marginBottom: "0.7rem" }}>No root admin yet? Seed the default one (dev only).</p>
          {seedError && <div className="error-banner">{seedError}</div>}
          <button type="button" disabled={seeding} onClick={seed}>
            <Sparkles size={13} strokeWidth={2.25} />{seeding ? "Seeding…" : "Seed root admin"}
          </button>
          {seedInfo && (
            <div className="summary-list" style={{ marginTop: "0.9rem", fontSize: "0.8rem" }}>
              <div className="row"><span className="k">Email</span><span className="mono">{seedInfo.email}</span></div>
              <div className="row"><span className="k">Password</span><span className="mono">{seedInfo.password}</span></div>
            </div>
          )}
        </div>

        <p className="hint" style={{ textAlign: "center", marginTop: "1rem" }}>
          Looking for the org sign-in? <a href="/login">Go there instead</a>.
        </p>
      </div>
    </div>
  );
}
