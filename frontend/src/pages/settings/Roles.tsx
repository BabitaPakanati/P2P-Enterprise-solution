import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Plus, Shield } from "lucide-react";
import { useSession } from "../../context/SessionContext";
import { listRoles, createRole, type RoleDto } from "../../api/settings";
import { ApiError } from "../../api/client";

export function Roles() {
  const { api, ready } = useSession();
  const [params] = useSearchParams();
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [loading, setLoading] = useState(true);
  // Arriving from Workflows' "no roles yet" prompt (?new=1) opens the form immediately.
  const [showForm, setShowForm] = useState(() => params.get("new") === "1");
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reload = () => {
    setLoading(true);
    listRoles(api).then(setRoles).finally(() => setLoading(false));
  };

  useEffect(() => {
    if (ready) reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, ready]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setCreating(true);
    try {
      await createRole(api, { code: code.toUpperCase().replace(/\s+/g, "_"), name, description: description || undefined });
      setCode("");
      setName("");
      setDescription("");
      setShowForm(false);
      reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not create the role.");
    } finally {
      setCreating(false);
    }
  };

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Approval Roles</h1>
          <p>Workflow steps approve against a role, never a specific person - see who currently holds each one under Administration → Users (not built yet).</p>
        </div>
        <div className="actions">
          <button className="primary" onClick={() => setShowForm((v) => !v)}><Plus size={15} strokeWidth={2.25} />Add Role</button>
        </div>
      </div>

      {showForm && (
        <form className="card" style={{ marginBottom: "1.5rem" }} onSubmit={submit}>
          {error && <div className="error-banner">{error}</div>}
          <div className="field-row">
            <div className="field">
              <label>Name</label>
              <input value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Finance Director" required />
            </div>
            <div className="field">
              <label>Code</label>
              <input value={code} onChange={(e) => setCode(e.target.value)} placeholder="e.g. FINANCE_DIRECTOR" required />
            </div>
          </div>
          <div className="field" style={{ marginBottom: "1rem" }}>
            <label>Description <span className="hint">(optional)</span></label>
            <input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="What this role approves" />
          </div>
          <div className="form-actions">
            <button type="submit" className="primary" disabled={creating}><Shield size={14} strokeWidth={2.25} />{creating ? "Creating…" : "Create Role"}</button>
            <button type="button" disabled={creating} onClick={() => { setShowForm(false); setCode(""); setName(""); setDescription(""); setError(null); }}>Cancel</button>
          </div>
        </form>
      )}

      <div className="table-wrap">
        <div className="table-scroll">
          <table>
            <thead><tr><th>Name</th><th>Code</th><th>Description</th></tr></thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={3} className="table-empty">Loading…</td></tr>
              ) : roles.length === 0 ? (
                <tr><td colSpan={3} className="table-empty">No roles yet.</td></tr>
              ) : (
                roles.map((r) => (
                  <tr key={r.id}>
                    <td>{r.name}</td>
                    <td className="mono">{r.code}</td>
                    <td>{r.description ?? "—"}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
}
