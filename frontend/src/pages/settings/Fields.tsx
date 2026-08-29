import { useEffect, useState } from "react";
import { Plus, Layers, PenSquare, XCircle } from "lucide-react";
import { useSession } from "../../context/SessionContext";
import {
  listFields, createField, updateField, deactivateField,
  FIELD_DATA_TYPES, KNOWN_ENTITY_TYPES, type FieldDefinitionDto, type FieldDataType,
} from "../../api/settings";
import { ApiError } from "../../api/client";
import { StatusBadge } from "../../components/StatusBadge";

interface FormState {
  label: string;
  fieldKey: string;
  dataType: FieldDataType;
  isRequired: boolean;
  selectOptionsText: string; // comma-separated, simplest input for a variable-length list
  dependsOnFieldKey: string;
  dependsOnValue: string;
  sequence: number;
}

const emptyForm = (sequence: number): FormState => ({
  label: "", fieldKey: "", dataType: "Text", isRequired: false, selectOptionsText: "", dependsOnFieldKey: "", dependsOnValue: "", sequence,
});

function toFieldKey(label: string) {
  const words = label.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return "";
  return words[0].toLowerCase() + words.slice(1).map((w) => w[0].toUpperCase() + w.slice(1)).join("");
}

export function Fields() {
  const { api, ready } = useSession();
  const [entityType, setEntityType] = useState<(typeof KNOWN_ENTITY_TYPES)[number]>(KNOWN_ENTITY_TYPES[0]);
  const [fields, setFields] = useState<FieldDefinitionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [formMode, setFormMode] = useState<"none" | "create" | string>("none"); // string = editing that field's id
  const [form, setForm] = useState<FormState>(emptyForm(1));
  const [saving, setSaving] = useState(false);

  const reload = () => {
    setLoading(true);
    listFields(api, entityType).then(setFields).finally(() => setLoading(false));
  };

  useEffect(() => {
    if (ready) reload();
    setFormMode("none");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, ready, entityType]);

  const startCreate = () => {
    setForm(emptyForm(fields.length + 1));
    setFormMode("create");
    setError(null);
  };

  const startEdit = (f: FieldDefinitionDto) => {
    setForm({
      label: f.label, fieldKey: f.fieldKey, dataType: f.dataType, isRequired: f.isRequired,
      selectOptionsText: (f.selectOptions ?? []).join(", "),
      dependsOnFieldKey: f.dependsOnFieldKey ?? "", dependsOnValue: f.dependsOnValue ?? "", sequence: f.sequence,
    });
    setFormMode(f.id);
    setError(null);
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSaving(true);
    const selectOptions = form.selectOptionsText.split(",").map((s) => s.trim()).filter(Boolean);
    try {
      if (formMode === "create") {
        await createField(api, {
          entityType, fieldKey: form.fieldKey, label: form.label, dataType: form.dataType, isRequired: form.isRequired,
          selectOptions: form.dataType === "Select" ? selectOptions : undefined,
          dependsOnFieldKey: form.dependsOnFieldKey || undefined, dependsOnValue: form.dependsOnValue || undefined,
          sequence: form.sequence,
        });
      } else {
        await updateField(api, formMode, {
          label: form.label, dataType: form.dataType, isRequired: form.isRequired,
          selectOptions: form.dataType === "Select" ? selectOptions : undefined,
          dependsOnFieldKey: form.dependsOnFieldKey || undefined, dependsOnValue: form.dependsOnValue || undefined,
          sequence: form.sequence,
        });
      }
      setFormMode("none");
      reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not save the field.");
    } finally {
      setSaving(false);
    }
  };

  const deactivate = async (id: string) => {
    setError(null);
    try {
      await deactivateField(api, id);
      reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not deactivate the field.");
    }
  };

  const otherFields = fields.filter((f) => f.isActive && f.id !== formMode);

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Custom Fields</h1>
          <p>Extra fields this org needs beyond the basics - mark required, set a type, and optionally show a field only when another field has a given value.</p>
        </div>
        <div className="actions">
          {formMode === "none" && <button className="primary" onClick={startCreate}><Plus size={15} strokeWidth={2.25} />Add Field</button>}
        </div>
      </div>

      <div className="tabs">
        {KNOWN_ENTITY_TYPES.map((t) => (
          <button key={t} className={entityType === t ? "active" : ""} onClick={() => setEntityType(t)}>{t}</button>
        ))}
      </div>

      {error && <div className="error-banner">{error}</div>}

      {formMode !== "none" && (
        <form className="card" style={{ marginBottom: "1.5rem" }} onSubmit={submit}>
          <h3>{formMode === "create" ? "New Field" : "Edit Field"}</h3>
          <div className="field-row">
            <div className="field">
              <label>Label</label>
              <input
                value={form.label}
                onChange={(e) => setForm((f) => ({ ...f, label: e.target.value, fieldKey: formMode === "create" ? toFieldKey(e.target.value) : f.fieldKey }))}
                placeholder="e.g. Asset Tag" required
              />
            </div>
            <div className="field">
              <label>Field key {formMode !== "create" && <span className="hint">(fixed once created)</span>}</label>
              <input value={form.fieldKey} onChange={(e) => setForm((f) => ({ ...f, fieldKey: e.target.value }))} disabled={formMode !== "create"} required />
            </div>
            <div className="field">
              <label>Data type</label>
              <select value={form.dataType} onChange={(e) => setForm((f) => ({ ...f, dataType: e.target.value as FieldDataType }))}>
                {FIELD_DATA_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
              </select>
            </div>
          </div>

          <div className="field-row">
            <div className="field">
              <label>&nbsp;</label>
              <label style={{ display: "flex", alignItems: "center", gap: "0.4rem", fontWeight: 400 }}>
                <input type="checkbox" style={{ width: "auto" }} checked={form.isRequired} onChange={(e) => setForm((f) => ({ ...f, isRequired: e.target.checked }))} />
                Required
              </label>
            </div>
            {form.dataType === "Select" && (
              <div className="field" style={{ gridColumn: "span 2" }}>
                <label>Options <span className="hint">(comma-separated)</span></label>
                <input value={form.selectOptionsText} onChange={(e) => setForm((f) => ({ ...f, selectOptionsText: e.target.value }))} placeholder="Standard, Emergency" />
              </div>
            )}
          </div>

          <div className="field-row">
            <div className="field">
              <label>Only show when <span className="hint">(optional)</span></label>
              <select value={form.dependsOnFieldKey} onChange={(e) => setForm((f) => ({ ...f, dependsOnFieldKey: e.target.value, dependsOnValue: "" }))}>
                <option value="">Always show</option>
                {otherFields.map((f) => <option key={f.fieldKey} value={f.fieldKey}>{f.label}</option>)}
              </select>
            </div>
            {form.dependsOnFieldKey && (
              <div className="field">
                <label>Equals</label>
                <input value={form.dependsOnValue} onChange={(e) => setForm((f) => ({ ...f, dependsOnValue: e.target.value }))} placeholder="e.g. Emergency" required />
              </div>
            )}
            <div className="field">
              <label>Display order</label>
              <input type="number" value={form.sequence} onChange={(e) => setForm((f) => ({ ...f, sequence: Number(e.target.value) }))} />
            </div>
          </div>

          <div className="form-actions">
            <button type="submit" className="primary" disabled={saving}><Layers size={14} strokeWidth={2.25} />{saving ? "Saving…" : formMode === "create" ? "Create Field" : "Save Changes"}</button>
            <button type="button" disabled={saving} onClick={() => setFormMode("none")}>Cancel</button>
          </div>
        </form>
      )}

      <div className="table-wrap">
        <div className="table-scroll">
          <table>
            <thead><tr><th>Label</th><th>Key</th><th>Type</th><th>Required</th><th>Depends on</th><th>Status</th><th></th></tr></thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={7} className="table-empty">Loading…</td></tr>
              ) : fields.length === 0 ? (
                <tr><td colSpan={7} className="table-empty">No custom fields configured for {entityType} yet.</td></tr>
              ) : (
                fields.map((f) => (
                  <tr key={f.id} style={{ opacity: f.isActive ? 1 : 0.55 }}>
                    <td>{f.label}</td>
                    <td className="mono">{f.fieldKey}</td>
                    <td>{f.dataType}</td>
                    <td>{f.isRequired ? "Yes" : "—"}</td>
                    <td>{f.dependsOnFieldKey ? `${f.dependsOnFieldKey} = ${f.dependsOnValue}` : "—"}</td>
                    <td><StatusBadge status={f.isActive ? "Active" : "Retired"} /></td>
                    <td className="actions-cell">
                      {f.isActive && (
                        <>
                          <button className="small" onClick={() => startEdit(f)}><PenSquare size={12} strokeWidth={2.25} /></button>
                          <button className="small danger" onClick={() => deactivate(f.id)}><XCircle size={12} strokeWidth={2.25} /></button>
                        </>
                      )}
                    </td>
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
