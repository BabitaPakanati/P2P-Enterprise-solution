import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useSession } from "../context/SessionContext";
import { getOrder, getOrderVersions, submitOrder, sendOrder, amendOrder } from "../api/procurement";
import { ApiError } from "../api/client";
import type { OrderDetail, DocumentVersion, CreateOrderLineInput } from "../api/types";
import { StatusBadge } from "../components/StatusBadge";

export function PurchaseOrderDetail() {
  const { id } = useParams<{ id: string }>();
  const { api, ready } = useSession();
  const [po, setPo] = useState<OrderDetail | null>(null);
  const [versions, setVersions] = useState<DocumentVersion[]>([]);
  const [tab, setTab] = useState<"overview" | "history" | "amend">("overview");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reload = () => {
    if (!id) return;
    getOrder(api, id).then(setPo).catch((e) => setError(e instanceof ApiError ? e.message : "Failed to load."));
    getOrderVersions(api, id).then(setVersions);
  };

  useEffect(() => {
    if (ready) reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, ready, id]);

  const act = async (fn: () => Promise<void>) => {
    setBusy(true);
    setError(null);
    try {
      await fn();
      reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Action failed.");
    } finally {
      setBusy(false);
    }
  };

  if (!po) return <div className="loading">{error ?? "Loading…"}</div>;

  return (
    <>
      <div className="page-header">
        <div>
          <h1>{po.poNumber} <StatusBadge status={po.status} /></h1>
          <p>{po.supplierName} · v{po.currentVersionNumber}</p>
        </div>
        <div className="actions">
          {po.status === "Draft" && <button className="primary" disabled={busy} onClick={() => act(() => submitOrder(api, po.id))}>Submit for Approval</button>}
          {po.status === "Approved" && <button className="primary" disabled={busy} onClick={() => act(() => sendOrder(api, po.id))}>Send to Supplier</button>}
          {(po.status === "Approved" || po.status === "SentToSupplier") && (
            <button disabled={busy} onClick={() => setTab("amend")}>Amend</button>
          )}
        </div>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="tabs">
        <button className={tab === "overview" ? "active" : ""} onClick={() => setTab("overview")}>Overview</button>
        <button className={tab === "history" ? "active" : ""} onClick={() => setTab("history")}>Version History ({versions.length})</button>
        {(po.status === "Approved" || po.status === "SentToSupplier") && (
          <button className={tab === "amend" ? "active" : ""} onClick={() => setTab("amend")}>Amend</button>
        )}
      </div>

      {tab === "overview" && (
        <div className="detail-grid">
          <div className="table-wrap">
            <table>
              <thead><tr><th>Item</th><th className="num">Qty</th><th>UOM</th><th className="num">Unit price</th><th className="num">Value</th></tr></thead>
              <tbody>
                {po.lines.map((l) => (
                  <tr key={l.id}>
                    <td>{l.itemDescription}</td><td className="num">{l.quantity}</td><td>{l.uom}</td>
                    <td className="num">{l.unitPrice.toLocaleString()}</td><td className="num">{l.lineValue.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="card">
            <h3 style={{ marginBottom: "0.9rem" }}>Summary</h3>
            <div className="summary-list">
              <div className="row"><span className="k">Supplier</span><span>{po.supplierName}</span></div>
              <div className="row"><span className="k">PO date</span><span>{po.poDate}</span></div>
              <div className="row"><span className="k">Delivery date</span><span>{po.deliveryDate ?? "—"}</span></div>
              <div className="row"><span className="k">Source requisition</span><Link to={`/requisitions/${po.sourceRequisitionId}`}>View</Link></div>
              <div className="row"><span className="k">Total value</span><b>{po.currency} {po.totalValue.toLocaleString()}</b></div>
            </div>
          </div>
        </div>
      )}

      {tab === "history" && (
        <div>
          {versions.map((v) => (
            <div className="version-card" key={v.id}>
              <div className="vhead">
                <b>v{v.versionNumber}</b>
                <StatusBadge status={v.versionStatus} />
              </div>
              <div className="summary-list" style={{ marginBottom: "0.5rem" }}>
                <div className="row"><span className="k">Effective</span><span>{new Date(v.effectiveFrom).toLocaleString()}{v.effectiveTo ? ` → ${new Date(v.effectiveTo).toLocaleString()}` : " → current"}</span></div>
                {v.changeReason && <div className="row"><span className="k">Change reason</span><span>{v.changeReason}</span></div>}
              </div>
              <pre>{JSON.stringify(JSON.parse(v.payloadJson), null, 2)}</pre>
            </div>
          ))}
        </div>
      )}

      {tab === "amend" && <AmendForm po={po} onDone={() => { setTab("overview"); reload(); }} />}
    </>
  );
}

function AmendForm({ po, onDone }: { po: OrderDetail; onDone: () => void }) {
  const { api } = useSession();
  const [supplierName, setSupplierName] = useState(po.supplierName);
  const [deliveryDate, setDeliveryDate] = useState(po.deliveryDate ?? "");
  const [changeReason, setChangeReason] = useState("");
  const [lines, setLines] = useState<CreateOrderLineInput[]>(po.lines.map((l) => ({ itemDescription: l.itemDescription, quantity: l.quantity, uom: l.uom, unitPrice: l.unitPrice })));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const updateLine = (i: number, patch: Partial<CreateOrderLineInput>) =>
    setLines((prev) => prev.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));

  const total = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!changeReason.trim()) { setError("A change reason is required for an amendment."); return; }
    setSaving(true);
    setError(null);
    try {
      await amendOrder(api, po.id, { supplierName, deliveryDate: deliveryDate || undefined, changeReason, lines });
      onDone();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Amendment failed.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <form className="card" onSubmit={submit}>
      <p className="hint" style={{ marginBottom: "0.8rem" }}>
        This creates a new pending version — v{po.currentVersionNumber} stays the effective one until this is approved.
      </p>
      {error && <div className="error-banner">{error}</div>}

      <div className="field-row">
        <div className="field"><label>Supplier</label><input value={supplierName} onChange={(e) => setSupplierName(e.target.value)} /></div>
        <div className="field"><label>Delivery date</label><input type="date" value={deliveryDate} onChange={(e) => setDeliveryDate(e.target.value)} /></div>
      </div>
      <div className="field" style={{ marginBottom: "0.9rem" }}>
        <label>Change reason</label>
        <input value={changeReason} onChange={(e) => setChangeReason(e.target.value)} placeholder="e.g. Supplier price increase" required />
      </div>

      <table className="line-table">
        <thead><tr><th>Item</th><th style={{ width: 90 }}>Qty</th><th style={{ width: 90 }}>UOM</th><th style={{ width: 130 }}>Unit price</th><th style={{ width: 110 }} className="num">Line value</th><th></th></tr></thead>
        <tbody>
          {lines.map((l, i) => (
            <tr key={i}>
              <td><input value={l.itemDescription} onChange={(e) => updateLine(i, { itemDescription: e.target.value })} /></td>
              <td><input type="number" min={0} step="0.01" value={l.quantity} onChange={(e) => updateLine(i, { quantity: Number(e.target.value) })} /></td>
              <td><input value={l.uom} onChange={(e) => updateLine(i, { uom: e.target.value })} /></td>
              <td><input type="number" min={0} step="0.01" value={l.unitPrice} onChange={(e) => updateLine(i, { unitPrice: Number(e.target.value) })} /></td>
              <td className="num">{(l.quantity * l.unitPrice).toLocaleString()}</td>
              <td>{lines.length > 1 && <button type="button" className="small danger" onClick={() => setLines((prev) => prev.filter((_, idx) => idx !== i))}>Remove</button>}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <button type="button" className="small" onClick={() => setLines((prev) => [...prev, { itemDescription: "", quantity: 1, uom: "EA", unitPrice: 0 }])}>+ Add line</button>

      <div className="summary-list" style={{ marginTop: "1rem" }}>
        <div className="row"><span className="k">Proposed total</span><b>{po.currency} {total.toLocaleString()}</b></div>
      </div>

      <div className="form-actions">
        <button type="submit" className="primary" disabled={saving}>{saving ? "Submitting…" : "Submit Amendment for Approval"}</button>
      </div>
    </form>
  );
}
