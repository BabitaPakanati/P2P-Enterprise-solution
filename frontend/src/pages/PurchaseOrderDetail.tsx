import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Send, PackageCheck, PenSquare } from "lucide-react";
import { useSession } from "../context/SessionContext";
import { getOrder, getOrderVersions, submitOrder, sendOrder, amendOrder } from "../api/procurement";
import { ApiError } from "../api/client";
import type { OrderDetail, DocumentVersion, CreateOrderLineInput } from "../api/types";
import { StatusBadge } from "../components/StatusBadge";
import { DynamicFields, type CustomFieldValues } from "../components/DynamicFields";

interface PoSnapshotLine { ItemDescription: string; Quantity: number; Uom: string; UnitPrice: number }
interface PoSnapshot { SupplierName: string; DeliveryDate: string | null; TotalValue: number; Currency: string; Lines: PoSnapshotLine[] }

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
          <h1 className="mono">{po.poNumber} <StatusBadge status={po.status} /></h1>
          <p>{po.supplierName} · version {po.currentVersionNumber}</p>
        </div>
        <div className="actions">
          {po.status === "Draft" && <button className="primary" disabled={busy} onClick={() => act(() => submitOrder(api, po.id))}><Send size={14} strokeWidth={2.25} />Submit for Approval</button>}
          {po.status === "Approved" && <button className="primary" disabled={busy} onClick={() => act(() => sendOrder(api, po.id))}><PackageCheck size={14} strokeWidth={2.25} />Send to Supplier</button>}
          {(po.status === "Approved" || po.status === "SentToSupplier") && (
            <button disabled={busy} onClick={() => setTab("amend")}><PenSquare size={14} strokeWidth={2.25} />Amend</button>
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
            <div className="table-scroll">
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
          </div>
          <div className="card">
            <h3>Summary</h3>
            <div className="summary-list">
              <div className="row"><span className="k">Supplier</span><span>{po.supplierName}</span></div>
              <div className="row"><span className="k">PO date</span><span className="num">{po.poDate}</span></div>
              <div className="row"><span className="k">Delivery date</span><span className="num">{po.deliveryDate ?? "—"}</span></div>
              <div className="row"><span className="k">Source requisition</span><Link to={`/requisitions/${po.sourceRequisitionId}`}>View</Link></div>
              <div className="row"><span className="k">Total value</span><span className="v-strong">{po.currency} {po.totalValue.toLocaleString()}</span></div>
            </div>
          </div>
        </div>
      )}

      {tab === "history" && (
        <div className="version-list">
          {versions.map((v) => {
            const snap = JSON.parse(v.payloadJson) as PoSnapshot;
            return (
              <div className={`version-card${v.versionStatus === "Active" ? " is-active" : ""}`} key={v.id}>
                <div className="vbody">
                  <div className="vhead">
                    <span className="vnum">Version {v.versionNumber}</span>
                    <StatusBadge status={v.versionStatus} />
                  </div>
                  <div className="version-kv">
                    <div><div className="k">Effective from</div><div className="v">{new Date(v.effectiveFrom).toLocaleString()}</div></div>
                    <div><div className="k">Effective to</div><div className="v">{v.effectiveTo ? new Date(v.effectiveTo).toLocaleString() : "current"}</div></div>
                    <div><div className="k">Supplier</div><div className="v">{snap.SupplierName}</div></div>
                    <div><div className="k">Total value</div><div className="v num">{snap.Currency} {snap.TotalValue.toLocaleString()}</div></div>
                    {v.changeReason && <div style={{ gridColumn: "1 / -1" }}><div className="k">Change reason</div><div className="v">{v.changeReason}</div></div>}
                  </div>
                  <div className="version-lines">
                    <table>
                      <thead><tr><th>Item</th><th className="num">Qty</th><th>UOM</th><th className="num">Unit price</th></tr></thead>
                      <tbody>
                        {snap.Lines.map((l, i) => (
                          <tr key={i}><td>{l.ItemDescription}</td><td className="num">{l.Quantity}</td><td>{l.Uom}</td><td className="num">{l.UnitPrice.toLocaleString()}</td></tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {tab === "amend" && <AmendForm po={po} onDone={() => { setTab("overview"); reload(); }} onCancel={() => setTab("overview")} />}
    </>
  );
}

function AmendForm({ po, onDone, onCancel }: { po: OrderDetail; onDone: () => void; onCancel: () => void }) {
  const { api } = useSession();
  const [supplierName, setSupplierName] = useState(po.supplierName);
  const [deliveryDate, setDeliveryDate] = useState(po.deliveryDate ?? "");
  const [changeReason, setChangeReason] = useState("");
  const [lines, setLines] = useState<CreateOrderLineInput[]>(po.lines.map((l) => ({ itemDescription: l.itemDescription, quantity: l.quantity, uom: l.uom, unitPrice: l.unitPrice })));
  const [customFields, setCustomFields] = useState<CustomFieldValues>(po.customFields);
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
      await amendOrder(api, po.id, { supplierName, deliveryDate: deliveryDate || undefined, changeReason, lines, customFields });
      onDone();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Amendment failed.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <form className="card" onSubmit={submit}>
      <p className="hint" style={{ marginBottom: "1rem" }}>
        This creates a new pending version — version {po.currentVersionNumber} stays the effective one until this is approved.
      </p>
      {error && <div className="error-banner">{error}</div>}

      <div className="field-row">
        <div className="field"><label>Supplier</label><input value={supplierName} onChange={(e) => setSupplierName(e.target.value)} /></div>
        <div className="field"><label>Delivery date</label><input type="date" value={deliveryDate} onChange={(e) => setDeliveryDate(e.target.value)} /></div>
      </div>
      <div className="field" style={{ marginBottom: "1rem" }}>
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
      <button type="button" className="small" style={{ marginTop: "0.6rem" }} onClick={() => setLines((prev) => [...prev, { itemDescription: "", quantity: 1, uom: "EA", unitPrice: 0 }])}>+ Add line</button>

      <DynamicFields entityType="PurchaseOrder" values={customFields} onChange={setCustomFields} />

      <div className="summary-list" style={{ marginTop: "1.1rem" }}>
        <div className="row"><span className="k">Proposed total</span><span className="v-strong">{po.currency} {total.toLocaleString()}</span></div>
      </div>

      <div className="form-actions">
        <button type="submit" className="primary" disabled={saving}>{saving ? "Submitting…" : "Submit Amendment for Approval"}</button>
        <button type="button" disabled={saving} onClick={onCancel}>Cancel</button>
      </div>
    </form>
  );
}
