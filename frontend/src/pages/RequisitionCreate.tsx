import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useSession } from "../context/SessionContext";
import { createRequisition } from "../api/procurement";
import { ApiError } from "../api/client";
import type { CreateRequisitionLineInput } from "../api/types";

const CATEGORIES = ["IT Services", "Facilities Management", "Professional Services", "MRO", "Marketing Services", "Logistics", "Packaging", "Industrial Raw Materials", "Utilities", "Capex"];
const emptyLine = (): CreateRequisitionLineInput => ({ itemDescription: "", quantity: 1, uom: "EA", estimatedUnitPrice: 0 });

export function RequisitionCreate() {
  const { api } = useSession();
  const navigate = useNavigate();
  const [description, setDescription] = useState("");
  const [category, setCategory] = useState(CATEGORIES[0]);
  const [requisitionType, setRequisitionType] = useState("Standard");
  const [requiredByDate, setRequiredByDate] = useState("");
  const [preferredSupplierName, setPreferredSupplierName] = useState("");
  const [lines, setLines] = useState<CreateRequisitionLineInput[]>([emptyLine()]);
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
    setSaving(true);
    try {
      const { id } = await createRequisition(api, {
        requiredByDate, requisitionType, description, category, currency: "USD",
        preferredSupplierName: preferredSupplierName || undefined, lines,
      });
      navigate(`/requisitions/${id}`);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Something went wrong creating the requisition.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Create Purchase Requisition</h1>
          <p>Saved as a draft first - submit separately once it looks right.</p>
        </div>
      </div>

      <form className="card" onSubmit={submit}>
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
                    <button type="button" className="small danger" onClick={() => setLines((prev) => prev.filter((_, idx) => idx !== i))}>Remove</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <button type="button" className="small" onClick={() => setLines((prev) => [...prev, emptyLine()])}>+ Add line</button>

        <div className="summary-list" style={{ marginTop: "1rem" }}>
          <div className="row"><span className="k">Estimated total</span><b>USD {total.toLocaleString()}</b></div>
        </div>

        <div className="form-actions">
          <button type="submit" className="primary" disabled={saving}>{saving ? "Saving…" : "Save Draft"}</button>
        </div>
      </form>
    </>
  );
}
