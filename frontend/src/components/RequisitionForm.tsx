import { useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { ApiError } from "../api/client";
import type { CreateRequisitionLineInput } from "../api/types";

const CATEGORIES = ["IT Services", "Facilities Management", "Professional Services", "MRO", "Marketing Services", "Logistics", "Packaging", "Industrial Raw Materials", "Utilities", "Capex"];
const emptyLine = (): CreateRequisitionLineInput => ({ itemDescription: "", quantity: 1, uom: "EA", estimatedUnitPrice: 0 });

export interface RequisitionFormValues {
  description: string;
  category: string;
  requisitionType: string;
  requiredByDate: string;
  preferredSupplierName: string;
  lines: CreateRequisitionLineInput[];
}

interface RequisitionFormProps {
  initial?: Partial<RequisitionFormValues>;
  /** Amending an already-approved requisition needs a reason; creating or editing a draft doesn't. */
  requireChangeReason?: boolean;
  onSubmit: (values: RequisitionFormValues, changeReason: string) => Promise<void>;
  submitLabel: string;
  submittingLabel: string;
  submitIcon: React.ReactNode;
  hint?: string;
}

/**
 * Shared by Create, Edit (Draft), and Amend (post-approval) - all three ask for the
 * same fields; only what happens on submit and whether a change reason is required
 * differ. See RequisitionCreate.tsx / RequisitionEdit.tsx / RequisitionDetail.tsx's
 * amend tab for how each wires this up.
 */
export function RequisitionForm({ initial, requireChangeReason, onSubmit, submitLabel, submittingLabel, submitIcon, hint }: RequisitionFormProps) {
  const [description, setDescription] = useState(initial?.description ?? "");
  const [category, setCategory] = useState(initial?.category ?? CATEGORIES[0]);
  const [requisitionType, setRequisitionType] = useState(initial?.requisitionType ?? "Standard");
  const [requiredByDate, setRequiredByDate] = useState(initial?.requiredByDate ?? "");
  const [preferredSupplierName, setPreferredSupplierName] = useState(initial?.preferredSupplierName ?? "");
  const [lines, setLines] = useState<CreateRequisitionLineInput[]>(initial?.lines?.length ? initial.lines : [emptyLine()]);
  const [changeReason, setChangeReason] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const updateLine = (i: number, patch: Partial<CreateRequisitionLineInput>) =>
    setLines((prev) => prev.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));

  const total = lines.reduce((sum, l) => sum + l.quantity * l.estimatedUnitPrice, 0);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!requiredByDate) { setError("Required-by date is required."); return; }
    if (lines.some((l) => !l.itemDescription.trim())) { setError("Every line needs an item description."); return; }
    if (requireChangeReason && !changeReason.trim()) { setError("A change reason is required."); return; }
    setSaving(true);
    try {
      await onSubmit({ description, category, requisitionType, requiredByDate, preferredSupplierName, lines }, changeReason);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Something went wrong.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <form className="card" onSubmit={submit}>
      {hint && <p className="hint" style={{ marginBottom: "1rem" }}>{hint}</p>}
      {error && <div className="error-banner">{error}</div>}

      <div className="field-row">
        <div className="field">
          <label>Description</label>
          <input value={description} onChange={(e) => setDescription(e.target.value)} required placeholder="e.g. Laptops for new hires" />
        </div>
        <div className="field">
          <label>Category</label>
          <select value={category} onChange={(e) => setCategory(e.target.value)}>
            {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
        </div>
      </div>

      <div className="field-row">
        <div className="field">
          <label>Requisition type</label>
          <select value={requisitionType} onChange={(e) => setRequisitionType(e.target.value)}>
            <option>Standard</option>
            <option>Blanket</option>
            <option>Emergency</option>
          </select>
        </div>
        <div className="field">
          <label>Required-by date</label>
          <input type="date" value={requiredByDate} onChange={(e) => setRequiredByDate(e.target.value)} required />
        </div>
        <div className="field">
          <label>Preferred supplier <span className="hint">(optional)</span></label>
          <input value={preferredSupplierName} onChange={(e) => setPreferredSupplierName(e.target.value)} placeholder="e.g. Dell Technologies" />
        </div>
      </div>

      {requireChangeReason && (
        <div className="field" style={{ marginBottom: "1rem" }}>
          <label>Change reason</label>
          <input value={changeReason} onChange={(e) => setChangeReason(e.target.value)} placeholder="e.g. Additional units needed" required />
        </div>
      )}

      <table className="line-table">
        <thead>
          <tr><th>Item</th><th style={{ width: 90 }}>Qty</th><th style={{ width: 90 }}>UOM</th><th style={{ width: 130 }}>Unit price</th><th style={{ width: 110 }} className="num">Est. value</th><th></th></tr>
        </thead>
        <tbody>
          {lines.map((l, i) => (
            <tr key={i}>
              <td><input value={l.itemDescription} onChange={(e) => updateLine(i, { itemDescription: e.target.value })} placeholder="Item description" /></td>
              <td><input type="number" min={0} step="0.01" value={l.quantity} onChange={(e) => updateLine(i, { quantity: Number(e.target.value) })} /></td>
              <td><input value={l.uom} onChange={(e) => updateLine(i, { uom: e.target.value })} /></td>
              <td><input type="number" min={0} step="0.01" value={l.estimatedUnitPrice} onChange={(e) => updateLine(i, { estimatedUnitPrice: Number(e.target.value) })} /></td>
              <td className="num">{(l.quantity * l.estimatedUnitPrice).toLocaleString()}</td>
              <td>
                {lines.length > 1 && (
                  <button type="button" className="small danger" onClick={() => setLines((prev) => prev.filter((_, idx) => idx !== i))}><Trash2 size={12} strokeWidth={2.25} />Remove</button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <button type="button" className="small" style={{ marginTop: "0.6rem" }} onClick={() => setLines((prev) => [...prev, emptyLine()])}><Plus size={13} strokeWidth={2.25} />Add line</button>

      <div className="summary-list" style={{ marginTop: "1.1rem" }}>
        <div className="row"><span className="k">Estimated total</span><span className="v-strong">USD {total.toLocaleString()}</span></div>
      </div>

      <div className="form-actions">
        <button type="submit" className="primary" disabled={saving}>{submitIcon}{saving ? submittingLabel : submitLabel}</button>
      </div>
    </form>
  );
}
