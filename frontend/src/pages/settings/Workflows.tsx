import { useEffect, useState } from "react";
import { Plus, GitBranch, PenSquare } from "lucide-react";
import { useSession } from "../../context/SessionContext";
import {
  listRoles, listWorkflows, createWorkflowDefinition, createWorkflowVersion,
  KNOWN_ENTITY_TYPES, type RoleDto, type WorkflowDefinitionDto, type WorkflowStepInput,
} from "../../api/settings";
import { ApiError } from "../../api/client";
import { StatusBadge } from "../../components/StatusBadge";
import { WorkflowStepEditor, emptyStep } from "../../components/WorkflowStepEditor";

export function Workflows() {
  const { api, ready } = useSession();
  const [workflows, setWorkflows] = useState<WorkflowDefinitionDto[]>([]);
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = () => {
    setLoading(true);
    Promise.all([listWorkflows(api), listRoles(api)])
      .then(([w, r]) => { setWorkflows(w); setRoles(r); })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    if (ready) reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, ready]);

  const configuredTypes = new Set(workflows.map((w) => w.entityType));
  const availableTypes = KNOWN_ENTITY_TYPES.filter((t) => !configuredTypes.has(t));

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Approval Workflows</h1>
          <p>How creation and approval works for each transaction type. Changing one never edits a version already in use - it adds a new one and retires the old.</p>
        </div>
      </div>

      {error && <div className="error-banner">{error}</div>}
      {roles.length === 0 && !loading && (
        <div className="error-banner">No approval roles exist yet - create one under Approval Roles first, then come back here.</div>
      )}

      {loading ? (
        <div className="loading">Loading…</div>
      ) : (
        <>
          {workflows.map((wf) => (
            <WorkflowCard key={wf.id} workflow={wf} roles={roles} onChanged={reload} onError={setError} />
          ))}

          {availableTypes.length > 0 && roles.length > 0 && (
            <CreateWorkflowCard availableTypes={availableTypes} roles={roles} onCreated={reload} onError={setError} />
          )}
        </>
      )}
    </>
  );
}

function WorkflowCard({ workflow, roles, onChanged, onError }: { workflow: WorkflowDefinitionDto; roles: RoleDto[]; onChanged: () => void; onError: (e: string | null) => void }) {
  const { api } = useSession();
  const [editing, setEditing] = useState(false);
  const [steps, setSteps] = useState<WorkflowStepInput[]>([]);
  const [saving, setSaving] = useState(false);

  const active = workflow.versions.find((v) => v.status === "Active");

  const startEdit = () => {
    setSteps(
      active
        ? active.steps.map((s) => ({ stepName: s.stepName, sequence: s.sequence, approvalRoleId: s.approvalRoleId, isMandatory: s.isMandatory, rules: s.rules.map((r) => ({ attribute: r.attribute, operator: r.operator, value: r.value, conjunction: r.conjunction })) }))
        : [emptyStep(1)],
    );
    setEditing(true);
  };

  const saveVersion = async () => {
    onError(null);
    setSaving(true);
    try {
      await createWorkflowVersion(api, workflow.id, steps);
      setEditing(false);
      onChanged();
    } catch (e) {
      onError(e instanceof ApiError ? e.message : "Could not save the new version.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="card" style={{ maxWidth: "none", marginBottom: "1.3rem" }}>
      <div className="page-header" style={{ marginBottom: "1rem" }}>
        <div>
          <h1 style={{ fontSize: "1.05rem" }}>{workflow.name}</h1>
          <p className="mono" style={{ fontSize: "0.78rem" }}>{workflow.entityType}</p>
        </div>
        {!editing && (
          <button onClick={startEdit}><PenSquare size={13} strokeWidth={2.25} />New Version</button>
        )}
      </div>

      {!editing ? (
        <div className="version-list">
          {workflow.versions.map((v) => (
            <div className={`version-card${v.status === "Active" ? " is-active" : ""}`} key={v.id}>
              <div className="vbody">
                <div className="vhead">
                  <span className="vnum">Version {v.versionNumber}</span>
                  <StatusBadge status={v.status} />
                </div>
                <div className="version-kv" style={{ marginBottom: v.steps.length ? "0.7rem" : 0 }}>
                  <div><div className="k">Effective from</div><div className="v">{v.effectiveFrom}</div></div>
                  <div><div className="k">Effective to</div><div className="v">{v.effectiveTo ?? "current"}</div></div>
                </div>
                {v.steps.map((s) => (
                  <div key={s.id} style={{ fontSize: "0.83rem", marginBottom: "0.4rem" }}>
                    <b>{s.sequence}. {s.stepName}</b> → <span className="badge accent"><span className="dot" />{s.approvalRoleName}</span>
                    {s.rules.length > 0 && (
                      <span className="hint" style={{ marginLeft: "0.5rem" }}>
                        when {s.rules.map((r) => `${r.attribute} ${r.operator} ${r.value}`).join(` ${s.rules[0].conjunction} `)}
                      </span>
                    )}
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div>
          <p className="hint" style={{ marginBottom: "0.9rem" }}>
            This becomes the new active version immediately - version {active?.versionNumber} stays visible in history but no longer applies to new submissions.
          </p>
          <WorkflowStepEditor steps={steps} onChange={setSteps} roles={roles} />
          <div className="form-actions">
            <button type="button" className="primary" disabled={saving} onClick={saveVersion}>{saving ? "Saving…" : "Activate New Version"}</button>
            <button type="button" disabled={saving} onClick={() => setEditing(false)}>Cancel</button>
          </div>
        </div>
      )}
    </div>
  );
}

function CreateWorkflowCard({ availableTypes, roles, onCreated, onError }: { availableTypes: readonly string[]; roles: RoleDto[]; onCreated: () => void; onError: (e: string | null) => void }) {
  const { api } = useSession();
  const [open, setOpen] = useState(false);
  const [entityType, setEntityType] = useState(availableTypes[0]);
  const [name, setName] = useState("");
  const [steps, setSteps] = useState<WorkflowStepInput[]>([emptyStep(1)]);
  const [creating, setCreating] = useState(false);

  if (!open) {
    return (
      <button className="primary" onClick={() => { setOpen(true); setName(`${availableTypes[0]} Approval`); }}>
        <Plus size={15} strokeWidth={2.25} />Configure workflow for {availableTypes[0]}
      </button>
    );
  }

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    onError(null);
    setCreating(true);
    try {
      await createWorkflowDefinition(api, { name, entityType, steps });
      setOpen(false);
      onCreated();
    } catch (e) {
      onError(e instanceof ApiError ? e.message : "Could not create the workflow.");
    } finally {
      setCreating(false);
    }
  };

  return (
    <form className="card" style={{ maxWidth: "none" }} onSubmit={submit}>
      <h3>New Workflow</h3>
      <div className="field-row">
        <div className="field">
          <label>Entity type</label>
          <select value={entityType} onChange={(e) => { setEntityType(e.target.value); setName(`${e.target.value} Approval`); }}>
            {availableTypes.map((t) => <option key={t} value={t}>{t}</option>)}
          </select>
        </div>
        <div className="field">
          <label>Name</label>
          <input value={name} onChange={(e) => setName(e.target.value)} required />
        </div>
      </div>
      <WorkflowStepEditor steps={steps} onChange={setSteps} roles={roles} />
      <div className="form-actions">
        <button type="submit" className="primary" disabled={creating}><GitBranch size={14} strokeWidth={2.25} />{creating ? "Creating…" : "Create Workflow"}</button>
        <button type="button" disabled={creating} onClick={() => { setOpen(false); setSteps([emptyStep(1)]); setName(""); }}>Cancel</button>
      </div>
    </form>
  );
}
