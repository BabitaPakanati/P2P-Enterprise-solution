import { useEffect, useState } from "react";
import { Plus, Building2 } from "lucide-react";
import { useAdminSession } from "../../context/AdminSessionContext";
import { listOrganisations, createOrganisation, type OrganisationSummary } from "../../api/admin";
import { ApiError } from "../../api/client";
import { StatusBadge } from "../../components/StatusBadge";

export function OrganisationsList() {
  const { api, ready } = useAdminSession();
  const [orgs, setOrgs] = useState<OrganisationSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [orgCode, setOrgCode] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reload = () => {
    setLoading(true);
    listOrganisations(api).then(setOrgs).finally(() => setLoading(false));
  };

  useEffect(() => {
    if (ready) reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, ready]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!/^[a-z][a-z0-9_]{1,30}$/.test(orgCode)) {
      setError("Org code must be lowercase letters, digits, or underscores, starting with a letter (e.g. 'acme').");
      return;
    }
    setCreating(true);
    try {
      await createOrganisation(api, orgCode, displayName);
      setOrgCode("");
      setDisplayName("");
      setShowForm(false);
      reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not create the organisation.");
    } finally {
      setCreating(false);
    }
  };

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Organisations</h1>
          <p>Creating one provisions a real Postgres schema and applies every migration to it automatically - no manual scripts.</p>
        </div>
        <div className="actions">
          <button className="primary" onClick={() => setShowForm((v) => !v)}><Plus size={15} strokeWidth={2.25} />Create Organisation</button>
        </div>
      </div>

      {showForm && (
        <form className="card" style={{ marginBottom: "1.5rem" }} onSubmit={submit}>
          {error && <div className="error-banner">{error}</div>}
          <div className="field-row">
            <div className="field">
              <label>Display name</label>
              <input value={displayName} onChange={(e) => setDisplayName(e.target.value)} placeholder="e.g. Stark Industries" required />
            </div>
            <div className="field">
              <label>Org code</label>
              <input value={orgCode} onChange={(e) => setOrgCode(e.target.value.toLowerCase())} placeholder="e.g. stark" required />
              <span className="hint">Becomes schema org_{orgCode || "…"}</span>
            </div>
          </div>
          <div className="form-actions">
            <button type="submit" className="primary" disabled={creating}><Building2 size={14} strokeWidth={2.25} />{creating ? "Provisioning…" : "Provision Organisation"}</button>
          </div>
        </form>
      )}

      <div className="table-wrap">
        <div className="table-scroll">
          <table>
            <thead><tr><th>Organisation</th><th>Org code</th><th>Schema</th><th>Created</th><th>Status</th></tr></thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={5} className="table-empty">Loading…</td></tr>
              ) : orgs.length === 0 ? (
                <tr><td colSpan={5} className="table-empty">No organisations yet.</td></tr>
              ) : (
                orgs.map((o) => (
                  <tr key={o.id}>
                    <td>{o.displayName}</td>
                    <td className="mono">{o.orgCode}</td>
                    <td className="mono">{o.schemaName}</td>
                    <td className="num">{new Date(o.createdAtUtc).toLocaleDateString()}</td>
                    <td><StatusBadge status={o.status} /></td>
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
