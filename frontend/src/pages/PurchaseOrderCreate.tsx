import { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ShoppingCart } from "lucide-react";
import { useSession } from "../context/SessionContext";
import { getRequisition, createOrder } from "../api/procurement";
import { ApiError } from "../api/client";
import type { RequisitionDetail, CreateOrderLineInput } from "../api/types";

export function PurchaseOrderCreate() {
  const [params] = useSearchParams();
  const requisitionId = params.get("fromRequisition") ?? "";
  const { api } = useSession();
  const navigate = useNavigate();

  const [pr, setPr] = useState<RequisitionDetail | null>(null);
  const [supplierName, setSupplierName] = useState("");
  const [deliveryDate, setDeliveryDate] = useState("");
  const [lines, setLines] = useState<CreateOrderLineInput[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!requisitionId) return;
    getRequisition(api, requisitionId).then((r) => {
      setPr(r);
      setSupplierName(r.preferredSupplierName ?? "");
      setLines(r.lines.map((l) => ({ itemDescription: l.itemDescription, quantity: l.quantity, uom: l.uom, unitPrice: l.estimatedUnitPrice })));
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [requisitionId]);

  const updateLine = (i: number, patch: Partial<CreateOrderLineInput>) =>
    setLines((prev) => prev.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));

  const total = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!supplierName.trim()) { setError("Supplier is required."); return; }
    setSaving(true);
    setError(null);
    try {
      const { id } = await createOrder(api, {
        sourceRequisitionId: requisitionId, supplierName, deliveryDate: deliveryDate || undefined, lines,
      });
      navigate(`/purchase-orders/${id}`);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not create the purchase order.");
    } finally {
      setSaving(false);
    }
  };

  if (!requisitionId) return <div className="error-banner">No source requisition specified.</div>;
  if (!pr) return <div className="loading">Loading requisition…</div>;

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Create Purchase Order</h1>
          <p>From requisition {pr.requisitionNumber} — {pr.description}</p>
        </div>
      </div>

      <form className="card" onSubmit={submit}>
        {error && <div className="error-banner">{error}</div>}

        <div className="field-row">
          <div className="field">
            <label>Supplier</label>
            <input value={supplierName} onChange={(e) => setSupplierName(e.target.value)} required />
          </div>
          <div className="field">
            <label>Delivery date</label>
            <input type="date" value={deliveryDate} onChange={(e) => setDeliveryDate(e.target.value)} />
          </div>
        </div>

        <table className="line-table">
          <thead>
            <tr><th>Item</th><th style={{ width: 90 }}>Qty</th><th style={{ width: 90 }}>UOM</th><th style={{ width: 130 }}>Unit price</th><th style={{ width: 110 }} className="num">Line value</th></tr>
          </thead>
          <tbody>
            {lines.map((l, i) => (
              <tr key={i}>
                <td><input value={l.itemDescription} onChange={(e) => updateLine(i, { itemDescription: e.target.value })} /></td>
                <td><input type="number" min={0} step="0.01" value={l.quantity} onChange={(e) => updateLine(i, { quantity: Number(e.target.value) })} /></td>
                <td><input value={l.uom} onChange={(e) => updateLine(i, { uom: e.target.value })} /></td>
                <td><input type="number" min={0} step="0.01" value={l.unitPrice} onChange={(e) => updateLine(i, { unitPrice: Number(e.target.value) })} /></td>
                <td className="num">{(l.quantity * l.unitPrice).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>

        <div className="summary-list" style={{ marginTop: "1.1rem" }}>
          <div className="row"><span className="k">PO total</span><span className="v-strong">{pr.currency} {total.toLocaleString()}</span></div>
        </div>

        <div className="form-actions">
          <button type="submit" className="primary" disabled={saving}><ShoppingCart size={14} strokeWidth={2.25} />{saving ? "Creating…" : "Create Purchase Order"}</button>
          <button type="button" disabled={saving} onClick={() => navigate(`/requisitions/${pr.id}`)}>Cancel</button>
        </div>
      </form>
    </>
  );
}
