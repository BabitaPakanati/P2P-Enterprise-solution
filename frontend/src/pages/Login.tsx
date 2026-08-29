import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Layers, LogIn, Sparkles } from "lucide-react";
import { useSession } from "../context/SessionContext";
import { seedFoundationForOrg, ApiError } from "../api/client";

const ORGS = [
  { code: "acme", label: "Acme Corporation" },
  { code: "globex", label: "Globex Corporation" },
];

export function Login() {
  const { login, loginLoading, loginError } = useSession();
  const navigate = useNavigate();
  const [orgCode, setOrgCode] = useState(ORGS[0].code);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [seedInfo, setSeedInfo] = useState<{ requester: string; approver: string; password: string } | null>(null);
  const [seeding, setSeeding] = useState(false);
  const [seedError, setSeedError] = useState<string | null>(null);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await login(orgCode, email, password);
      navigate("/");
    } catch {
      /* loginError already set by the context */
    }
  };

  const seed = async () => {
    setSeeding(true);
    setSeedError(null);
    try {
      const result = await seedFoundationForOrg(orgCode);
      setSeedInfo({ requester: `requester@${orgCode}.example`, approver: `approver@${orgCode}.example`, password: result.devPassword });
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
          <div className="mark" style={{ width: 34, height: 34 }}><Layers size={18} strokeWidth={2.25} /></div>
          <div style={{ fontWeight: 700, fontSize: "1.15rem" }}>P2P Control Tower</div>
        </div>

        <form className="card" style={{ maxWidth: "none" }} onSubmit={submit}>
          <h3>Sign in</h3>
          {loginError && <div className="error-banner">{loginError}</div>}

          <div className="field" style={{ marginBottom: "1rem" }}>
            <label>Organisation</label>
            <select value={orgCode} onChange={(e) => { setOrgCode(e.target.value); setSeedInfo(null); }}>
              {ORGS.map((o) => <option key={o.code} value={o.code}>{o.label}</option>)}
            </select>
          </div>
          <div className="field" style={{ marginBottom: "1rem" }}>
            <label>Email</label>
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="you@acme.example" required autoFocus />
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
          <p className="hint" style={{ marginBottom: "0.7rem" }}>
            New organisation, or don't have an account yet? Seed demo users for <b>{ORGS.find((o) => o.code === orgCode)?.label}</b>.
          </p>
          {seedError && <div className="error-banner">{seedError}</div>}
          <button type="button" disabled={seeding} onClick={seed}>
            <Sparkles size={13} strokeWidth={2.25} />{seeding ? "Seeding…" : "Seed demo data"}
          </button>
          {seedInfo && (
            <div className="summary-list" style={{ marginTop: "0.9rem", fontSize: "0.8rem" }}>
              <div className="row"><span className="k">Requester</span><span className="mono">{seedInfo.requester}</span></div>
              <div className="row"><span className="k">Approver</span><span className="mono">{seedInfo.approver}</span></div>
              <div className="row"><span className="k">Password</span><span className="mono">{seedInfo.password}</span></div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
