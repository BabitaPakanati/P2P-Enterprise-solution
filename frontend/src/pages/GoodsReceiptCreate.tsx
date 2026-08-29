import { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { PackageCheck } from "lucide-react";
import { useSession } from "../context/SessionContext";
import { getOrder } from "../api/procurement";
import { getPurchaseOrderReceiptStatus, createGoodsReceipt } from "../api/receiving";
import { ApiError } from "../api/client";
import type { OrderDetail, PurchaseOrderReceiptStatus } from "../api/types";
import { DynamicFields, type CustomFieldValues } from "../components/DynamicFields";

interface LineInput {
  purchaseOrderLineId: string;
  itemDescription: string;
  uom: string;
  quantityOrdered: number;
  quantityRemaining: number;
  quantityReceived: string; // free-text so the field can be cleared while typing
  quantityRejected: string;
}

export function GoodsReceiptCreate() {
  const [params] = useSearchParams();
  const purchaseOrderId = params.get("fromPurchaseOrder") ?? "";
  const { api } = useSession();
  const navigate = useNavigate();

  const [po, setPo] = useState<OrderDetail | null>(null);
  const [receiptStatus, setReceiptStatus] = useState<PurchaseOrderReceiptStatus | null>(null);
  const [deliveryDate, setDeliveryDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [deliveryNoteNumber, setDeliveryNoteNumber] = useState("");
  const [location, setLocation] = useState("");
  const [lines, setLines] = useState<LineInput[]>([]);
  const [customFields, setCustomFields] = useState<CustomFieldValues>({});
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!purchaseOrderId) return;
    Promise.all([getOrder(api, purchaseOrderId), getPurchaseOrderReceiptStatus(api, purchaseOrderId)]).then(([orderDto, status]) => {
      setPo(orderDto);
      setReceiptStatus(status);
      setLines(status.lines.map((l) => ({
        purchaseOrderLineId: l.purchaseOrderLineId,
        itemDescription: l.itemDescription,
        uom: l.uom,
        quantityOrdered: l.quantityOrdered,
        quantityRemaining: l.quantityRemaining,
        quantityReceived: l.quantityRemaining > 0 ? String(l.quantityRemaining) : "",
        quantityRejected: "0",
      })));
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [purchaseOrderId]);

  const updateLine = (i: number, patch: Partial<LineInput>) =>
    setLines((prev) => prev.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const submittedLines = lines
      .filter((l) => Number(l.quantityReceived) > 0)
      .map((l) => ({ purchaseOrderLineId: l.purchaseOrderLineId, quantityReceived: Number(l.quantityReceived), quantityRejected: Number(l.quantityRejected) || 0 }));
    if (submittedLines.length === 0) {
      setError("Enter a quantity received for at least one line.");
      return;
    }

    setSaving(true);
    try {
      const { id } = await createGoodsReceipt(api, {
        purchaseOrderId, deliveryDate, deliveryNoteNumber: deliveryNoteNumber || undefined, location: location || undefined,
        lines: submittedLines, customFields,
      });
      navigate(`/goods-receipts/${id}`);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not record the goods receipt.");
    } finally {
      setSaving(false);
    }
  };

  if (!purchaseOrderId) return <div className="error-banner">No source purchase order specified.</div>;
  if (!po || !receiptStatus) return <div className="loading">Loading purchase order…</div>;

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Record Goods Receipt</h1>
          <p>Against PO {po.poNumber} — {po.supplierName}</p>
        </div>
      </div>

      <form className="card" onSubmit={submit}>
        {error && <div className="error-banner">{error}</div>}

        <div className="field-row">
          <div className="field">
            <label>Delivery date</label>
            <input type="date" value={deliveryDate} onChange={(e) => setDeliveryDate(e.target.value)} required />
          </div>
          <div className="field">
            <label>Delivery note # <span className="hint">(optional)</span></label>
            <input value={deliveryNoteNumber} onChange={(e) => setDeliveryNoteNumber(e.target.value)} placeholder="e.g. DN-20481" />
          </div>
          <div className="field">
            <label>Location <span className="hint">(optional)</span></label>
            <input value={location} onChange={(e) => setLocation(e.target.value)} placeholder="e.g. Main Warehouse" />
          </div>
        </div>

        <table className="line-table">
          <thead>
            <tr>
              <th>Item</th><th style={{ width: 90 }} className="num">Ordered</th><th style={{ width: 90 }} className="num">Remaining</th>
              <th style={{ width: 110 }}>Qty received</th><th style={{ width: 110 }}>Qty rejected</th>
            </tr>
          </thead>
          <tbody>
            {lines.map((l, i) => (
              <tr key={l.purchaseOrderLineId}>
                <td>{l.itemDescription} <span className="hint">({l.uom})</span></td>
                <td className="num">{l.quantityOrdered}</td>
                <td className="num">{l.quantityRemaining}</td>
                <td><input type="number" min={0} step="0.01" max={l.quantityRemaining} value={l.quantityReceived} onChange={(e) => updateLine(i, { quantityReceived: e.target.value })} /></td>
                <td><input type="number" min={0} step="0.01" value={l.quantityRejected} onChange={(e) => updateLine(i, { quantityRejected: e.target.value })} /></td>
              </tr>
            ))}
          </tbody>
        </table>
        {lines.every((l) => l.quantityRemaining <= 0) && <p className="hint" style={{ marginTop: "0.6rem" }}>Every line on this PO has already been fully received.</p>}

        <DynamicFields entityType="GoodsReceipt" values={customFields} onChange={setCustomFields} />

        <div className="form-actions">
          <button type="submit" className="primary" disabled={saving}><PackageCheck size={14} strokeWidth={2.25} />{saving ? "Recording…" : "Record Goods Receipt"}</button>
          <button type="button" disabled={saving} onClick={() => navigate(`/purchase-orders/${po.id}`)}>Cancel</button>
        </div>
      </form>
    </>
  );
}
